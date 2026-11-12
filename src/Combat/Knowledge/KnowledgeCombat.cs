using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static class KnowledgeCombat
{
    private static readonly AsyncLocal<int> SingleDepth = new();
    private static readonly AsyncLocal<HashSet<string>?> Drawing = new();
    private sealed class Scope : IDisposable
    {
        private bool _disposed;
        public Scope() => SingleDepth.Value++;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SingleDepth.Value--;
        }
    }

    private static async Task CompleteAcquisition(IEnumerable<CardPileAddResult> results, IReadOnlySet<CardModel> allowed)
    {
        // A dynamic group can already contain other selected roots from this same native batch.
        var expanded = new HashSet<CardModel>();
        foreach (var result in results)
        {
            var card = result.cardAdded;
            if (result.success && result.oldPile == null) KnowledgeProgress.Acquired(card);
            if (!allowed.Contains(card) || !result.success || result.oldPile != null || !CardCatalog.IsKnowledge(card) || !expanded.Add(card)) continue;
            if (!await DynamicKnowledge.Acquire(card, card.Pile!.Type, expanded))
                await GrantPartners(card, card.Pile!.Type);
        }
    }
    public static IDisposable SingleCard() => new Scope();
    public static KnowledgeGroup? Group(CardModel card)
    {
        string? id = CardCatalog.DesignId(card);
        return id == null ? null : CardCatalog.Groups.FirstOrDefault(g => g.MainCardId == id);
    }
    public static bool HasGroup(CardModel card) => KnowledgeMembers.Definition(card) != null;
    internal static CardModel[] CapturePartners(CardModel card)
    {
        var ids = KnowledgeMembers.PartnerIds(card);
        var hand = card.Owner.PlayerCombatState!.Hand;
        return hand.Cards.Where(c => c != card && ids.Contains(c.Id)).ToArray();
    }
    public static Task PlayPartners(PlayerChoiceContext context, CardModel card)
        => PlayCapturedPartners(context, card, CapturePartners(card));
    internal static async Task PlayCapturedPartners(PlayerChoiceContext context, CardModel card, IReadOnlyList<CardModel> partners)
    {
        var hand = card.Owner.PlayerCombatState!.Hand;
        foreach (var partner in partners)
            if (partner.Pile == hand) await CardActions.FreePlay(context, partner);
    }
    public static async Task DrawPartners(PlayerChoiceContext context, CardModel card)
    {
        if (!HasGroup(card)) return;
        var active = Drawing.Value ??= [];
        string key = card.Owner.NetId + ":" + card.Id;
        if (!active.Add(key)) return;
        try
        {
            var ids = KnowledgeMembers.PartnerIds(card);
            await CardActions.DrawMatching(context, card.Owner,
                c => c != card && ids.Contains(c.Id), CardPile.MaxCardsInHand);
        }
        finally { active.Remove(key); }
    }
    internal static async Task<IReadOnlyList<CardModel>> GrantPartners(CardModel acquired, PileType destination,
        IReadOnlyList<CardModel>? selected = null)
    {
        var members = KnowledgeMembers.Definition(acquired)?.ToArray();
        if (members == null) return [acquired];
        using var single = SingleCard();
        var gained = new List<CardModel> { acquired };
        var added = new List<CardPileAddResult>();
        var offered = (selected ?? []).ToList();
        foreach (var (model, count) in members)
            for (int i = model.Id == acquired.Id ? 1 : 0; i < count; i++)
            {
                var card = offered.FirstOrDefault(card => card.Id == model.Id);
                if (card != null) offered.Remove(card);
                else card = destination == PileType.Deck
                    ? acquired.Owner.RunState.CreateCard(model, acquired.Owner)
                    : acquired.Owner.Creature.CombatState!.CreateCard(model, acquired.Owner);
                if (destination == PileType.Deck)
                {
                    var result = await CardPileCmd.Add(card, PileType.Deck);
                    added.Add(result);
                    if (result.success && result.cardAdded.Pile == acquired.Pile) gained.Add(result.cardAdded);
                }
                else
                {
                    added.Add(await CardPileCmd.AddGeneratedCardToCombat(card, destination, acquired.Owner,
                        destination == PileType.Draw ? CardPilePosition.Random : CardPilePosition.Bottom));
                    if (card.Pile?.Type == destination) gained.Add(card);
                }
            }
        if (destination == PileType.Deck) KnowledgeProgress.BindResearch(gained);
        DynamicKnowledge.Normalize(acquired.Owner);
        if (destination == PileType.Deck && acquired.Pile is { Type: PileType.Deck } deck)
        {
            // Native batches (e.g. Hefty Tablet: chosen card + Injury) finish before partners
            // are granted. Keep just this newly gained group adjacent, without acquiring twice.
            int index = deck.Cards.ToList().IndexOf(acquired);
            var order = members.Select(member => member.Model.Id).Distinct().ToList();
            var ordered = gained.Where(c => c.Pile == deck).OrderBy(c => order.IndexOf(c.Id)).ToArray();
            foreach (var card in ordered) deck.RemoveInternal(card, silent: true);
            foreach (var card in ordered) deck.AddInternal(card, index++, silent: true);
            deck.InvokeContentsChanged();
        }
        await CardActions.PreviewAdded(added, destination);
        return gained;
    }

    [HarmonyPatch]
    private static class AcquireGroup
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
            [typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)
#if STS2_BETA
             , typeof(bool)
#endif
            ]);
        private static void Prefix(ref IEnumerable<CardModel> cards, CardPile newPile, AbstractModel? clonedBy, out HashSet<CardModel> __state)
        {
            var originals = cards.ToArray();
            cards = originals;
            // Core can defer and reorder movement results, so eligibility follows the physical card.
            __state = originals.Where(card => SingleDepth.Value == 0 && clonedBy == null && card.Pile == null
                && KnowledgeAcquisitionSources.Allows(card, newPile.Type)).ToHashSet();
        }
        private static void Postfix(ref Task<IReadOnlyList<CardPileAddResult>> __result, HashSet<CardModel> __state)
        { __result = Complete(__result, __state); }
        private static async Task<IReadOnlyList<CardPileAddResult>> Complete(Task<IReadOnlyList<CardPileAddResult>> task, HashSet<CardModel> allowed)
        {
            var results = await task;
            await CompleteAcquisition(results, allowed);
            return results;
        }
    }
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Transform), [typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle)])]
    private static class TransformGroup
    {
        private static void Prefix(out bool __state) => __state = SingleDepth.Value == 0;
        private static void Postfix(ref Task<IEnumerable<CardPileAddResult>> __result, bool __state)
            => __result = Complete(__result, __state);
        private static async Task<IEnumerable<CardPileAddResult>> Complete(Task<IEnumerable<CardPileAddResult>> task, bool enabled)
        {
            var results = (await task).ToArray();
            await CompleteAcquisition(results, results.Where(result => enabled && result.cardAdded.Pile?.Type == PileType.Deck
                && KnowledgeAcquisitionSources.Allows(result.cardAdded, PileType.Deck, transforming: true))
                .Select(result => result.cardAdded).ToHashSet());
            return results;
        }
    }
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
    private static class DiscoverGroup
    {
        private static void Postfix(PlayerChoiceContext context, IReadOnlyList<CardModel> cards, ref Task<CardModel?> __result)
            => __result = Complete(__result, context, cards);
        private static async Task<CardModel?> Complete(Task<CardModel?> task, PlayerChoiceContext context, IReadOnlyList<CardModel> cards)
        {
            var card = await task;
            if (card?.Pile == null && card != null && KnowledgeAcquisitionSources.AllowsDiscovery)
            {
                KnowledgeAcquisitionSources.Selected(card);
                if (CardCatalog.IsKnowledge(card)) await DynamicKnowledge.Prepare(context, card, cards);
            }
            return card;
        }
    }
    [HarmonyPatch]
    private static class CopyAcquisitionMetadata
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.CreateClone));
            yield return AccessTools.Method(typeof(RunState), nameof(RunState.CloneCard));
        }
        private static void Postfix(object __instance, object[] __args, CardModel __result)
        {
            var source = __instance as CardModel ?? (CardModel)__args[0];
            DynamicKnowledge.CopyAcquisition(source, __result);
        }
    }
    private static IDisposable? _upgrades;
    internal static void Initialize()
    {
        KnowledgeAcquisitionSources.Initialize();
        _upgrades ??= ShoujoKageki.Core.V1.CoreApi.Get<ShoujoKageki.Core.V1.ICardUpgrades>().Register(
            card => CardCatalog.DesignId(card) == "JK82", GrantUpgrade);
    }
    private static async Task GrantUpgrade(CardModel card, int previous, int current)
    {
        using var single = SingleCard();
        var added = new List<CardPileAddResult>();
        for (int i = previous; i < current; i++)
        {
            if (card.Pile?.Type == PileType.Deck)
                added.Add(await CardPileCmd.Add(card.Owner.RunState.CreateCard(CardCatalog.Model("JK82B"), card.Owner), PileType.Deck));
            else if (card.Owner.PlayerCombatState != null && !card.HasBeenRemovedFromState)
                await CardActions.Generate(card.Owner, "JK82B", 1, PileType.Draw, CardPilePosition.Random);
        }
        await CardActions.PreviewAdded(added, PileType.Deck);
    }
}
