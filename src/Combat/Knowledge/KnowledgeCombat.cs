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
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static class KnowledgeCombat
{
    private static readonly AsyncLocal<int> SingleDepth = new();
    private static readonly AsyncLocal<HashSet<string>?> Drawing = new();
    private static readonly ConditionalWeakTable<CardModel, object> Discovered = new();
    private static readonly ConditionalWeakTable<CardModel, object> Copies = new();
    private sealed class Scope : IDisposable
    {
        public Scope() => SingleDepth.Value++;
        public void Dispose() => SingleDepth.Value--;
    }

    private static async Task CompleteAcquisition(IEnumerable<CardPileAddResult> results, bool single)
    {
        foreach (var result in results)
        {
            var card = result.cardAdded;
            if (result.success && result.oldPile == null) KnowledgeProgress.Acquired(card);
            if (single || !result.success || result.oldPile != null || !CardCatalog.IsKnowledge(card) || Copies.TryGetValue(card, out _)) continue;
            bool discovered = Discovered.Remove(card);
            bool generated = card.Pile!.IsCombatPile && card.Pile.Type != PileType.Play && card.DeckVersion == null;
            if (card.Pile.Type == PileType.Deck || discovered || generated)
            {
                if (!await DynamicKnowledge.Acquire(card, card.Pile!.Type))
                    await GrantPartners(card, card.Pile!.Type);
            }
        }
    }
    public static IDisposable SingleCard() => new Scope();
    public static KnowledgeGroup? Group(CardModel card)
    {
        string? id = CardCatalog.DesignId(card);
        return id == null ? null : CardCatalog.Groups.FirstOrDefault(g => g.MainCardId == id || g.GrantedCardIds.Contains(id));
    }
    public static bool HasGroup(CardModel card) => Group(card) != null || DynamicKnowledge.IsLinked(card);
    public static async Task PlayPartners(PlayerChoiceContext context, CardModel card)
    {
        var relation = DynamicKnowledge.Group(card);
        var group = relation == null ? Group(card) : null;
        var ids = relation?.CardCounts.Keys.ToHashSet() ?? group?.Members
            .Where(member => member.Id != CardCatalog.DesignId(card) || member.Count > 1)
            .Select(member => CardCatalog.Model(member.Id).Id.ToString()).ToHashSet() ?? [];
        var hand = card.Owner.PlayerCombatState!.Hand;
        var partners = hand.Cards.Where(c => c != card && ids.Contains(c.Id.ToString())).ToArray();
        foreach (var partner in partners)
            if (partner.Pile == hand) await CardActions.FreePlay(context, partner);
    }
    public static async Task DrawPartners(PlayerChoiceContext context, CardModel card)
    {
        var relation = DynamicKnowledge.Group(card);
        var group = relation == null ? Group(card) : null;
        if (group == null && relation == null) return;
        var active = Drawing.Value ??= [];
        string key = card.Owner.NetId + ":" + (relation?.EntranceId ?? group!.MainCardId);
        if (!active.Add(key)) return;
        try
        {
            var ids = relation?.CardCounts.Keys.ToHashSet() ?? group!.Members
                .Where(member => member.Id != CardCatalog.DesignId(card) || member.Count > 1)
                .Select(member => CardCatalog.Model(member.Id).Id.ToString()).ToHashSet();
            await CardActions.DrawMatching(context, card.Owner,
                c => c != card && ids.Contains(c.Id.ToString()), CardPile.MaxCardsInHand);
        }
        finally { active.Remove(key); }
    }
    internal static async Task<IReadOnlyList<CardModel>> GrantPartners(CardModel acquired, PileType destination,
        IReadOnlyList<CardModel>? selected = null)
    {
        var members = (DynamicKnowledge.Definition(acquired) ?? Group(acquired)?.Members
            .Select(member => (Model: CardCatalog.Model(member.Id), member.Count)))?.ToArray();
        if (members == null) return [acquired];
        using var single = SingleCard();
        var gained = new List<CardModel> { acquired };
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
                    if (result.success && result.cardAdded.Pile == acquired.Pile) gained.Add(result.cardAdded);
                }
                else
                {
                    await CardPileCmd.AddGeneratedCardToCombat(card, destination, acquired.Owner);
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
        private static void Prefix(AbstractModel? clonedBy, out bool __state) => __state = SingleDepth.Value > 0 || clonedBy != null;
        private static void Postfix(ref Task<IReadOnlyList<CardPileAddResult>> __result, bool __state)
        { __result = Complete(__result, __state); }
        private static async Task<IReadOnlyList<CardPileAddResult>> Complete(Task<IReadOnlyList<CardPileAddResult>> task, bool single)
        {
            var results = await task;
            await CompleteAcquisition(results, single);
            return results;
        }
    }
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Transform), [typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle)])]
    private static class TransformGroup
    {
        private static void Prefix(out bool __state) => __state = SingleDepth.Value > 0;
        private static void Postfix(ref Task<IEnumerable<CardPileAddResult>> __result, bool __state)
            => __result = Complete(__result, __state);
        private static async Task<IEnumerable<CardPileAddResult>> Complete(Task<IEnumerable<CardPileAddResult>> task, bool single)
        {
            var results = (await task).ToArray();
            await CompleteAcquisition(results, single);
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
            if (card?.Pile == null && card != null && CardCatalog.IsKnowledge(card))
            {
                Discovered.GetOrCreateValue(card);
                await DynamicKnowledge.Prepare(context, card, cards);
            }
            return card;
        }
    }
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.CreateClone))]
    private static class MarkCopies
    {
        private static void Postfix(CardModel __result) => Copies.GetOrCreateValue(__result);
    }
    private static IDisposable? _upgrades;
    internal static void Initialize()
    {
        _upgrades ??= ShoujoKageki.Core.V1.CoreApi.Get<ShoujoKageki.Core.V1.ICardUpgrades>().Register(
            card => CardCatalog.DesignId(card) == "JK82", GrantUpgrade);
    }
    private static async Task GrantUpgrade(CardModel card, int previous, int current)
    {
        using var single = SingleCard();
        for (int i = previous; i < current; i++)
        {
            if (card.Pile?.Type == PileType.Deck)
                await CardPileCmd.Add(card.Owner.RunState.CreateCard(CardCatalog.Model("JK82B"), card.Owner), PileType.Deck);
            else if (card.Owner.PlayerCombatState != null && !card.HasBeenRemovedFromState)
                await CardActions.Generate(card.Owner, "JK82B", 1, PileType.Draw);
        }
    }
}
