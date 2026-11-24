using System.Runtime.CompilerServices;
using System.Text.Json;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Permanent and combat-only model-ID relations, including cards from other characters.</summary>
public static class DynamicKnowledge
{
    // Retained only to migrate groups saved by the former per-card implementation.
    private static readonly SavedSpireField<CardModel, string> Links = new(() => null, "JunnaKnowledgeLinks");
    private static readonly SavedSpireField<Player, string> Relations = new(() => null, "JunnaKnowledgeRelations");
    private sealed class RelationCache
    {
        public string? Serialized;
        public List<KnowledgeRelation>? Groups;
    }
    private static readonly ConditionalWeakTable<Player, RelationCache> Cache = new();
    private static readonly ConditionalWeakTable<PlayerCombatState, List<KnowledgeRelation>> CombatGroups = new();
    private static readonly ConditionalWeakTable<CardModel, CardModel[]> Pending = new();
    private sealed record Offer(CardReward Reward, CardModel Original);
    private static readonly ConditionalWeakTable<CardModel, Offer> Offers = new();
    private static string PreferredEntrance => CardCatalog.Model("JK126").Id.ToString();
    private static List<KnowledgeRelation> Groups(Player player)
    {
        var cache = Cache.GetOrCreateValue(player);
        string? serialized = Relations.Get(player);
        if (cache.Groups == null || cache.Serialized != serialized)
        {
            cache.Groups = string.IsNullOrEmpty(serialized) ? [] : JsonSerializer.Deserialize<List<KnowledgeRelation>>(serialized)!;
            cache.Serialized = serialized;
            cache.Groups.RemoveAll(group => RemovedKnowledgeCards.Contains(ModelId.Deserialize(group.EntranceId)));
            foreach (var group in cache.Groups)
                foreach (string member in group.CardCounts.Keys.Where(key => RemovedKnowledgeCards.Contains(ModelId.Deserialize(key))).ToArray())
                    group.CardCounts.Remove(member);
            foreach (var group in CardCatalog.Groups)
            {
                string entrance = CardCatalog.Model(group.MainCardId).Id.ToString();
                if (cache.Groups.Any(existing => existing.EntranceId == entrance)) continue;
                var counts = group.Members.ToDictionary(member => CardCatalog.Model(member.Id).Id.ToString(),
                    member => member.Count);
                cache.Groups.Add(new(entrance, counts));
            }
        }
        return cache.Groups;
    }
    internal static KnowledgeRelation? Group(CardModel card, bool inCombat = false)
    {
        if (!card.IsMutable || card.Owner is not { } owner) return null;
        string id = card.Id.ToString();
        if ((inCombat || card.CombatState != null || owner.Creature.CombatState?.ContainsCard(card) == true)
            && owner.PlayerCombatState is { } state && CombatGroups.TryGetValue(state, out var groups)
            && groups.FirstOrDefault(group => group.EntranceId == id) is { } current)
            return current;
        return Groups(owner).FirstOrDefault(group => group.EntranceId == id);
    }

    internal static void ClearCombatGroups() => CombatGroups.Clear();
    public static bool IsLinked(CardModel card) => Group(card) != null;
    public static bool IsRewardEntrance(Player player, CardModel card)
        => card.Pool is not JunnaCardPool || CardCatalog.IsRewardCard(card);
    internal static IEnumerable<(CardModel Model, int Count)>? Definition(CardModel card, bool inCombat) => Group(card, inCombat)?.Members
        .Select(member => (ModelDb.GetById<CardModel>(ModelId.Deserialize(member.Id)), member.Count));
    public static IReadOnlyList<CardModel> Members(CardModel source)
    {
        var definition = KnowledgeMembers.Definition(source)?.ToArray();
        if (definition == null) return [];
        var cards = !source.IsMutable || source.Pile == null ? Enumerable.Empty<CardModel>()
            : source.Pile.IsCombatPile && source.Owner.PlayerCombatState != null
                ? source.Owner.PlayerCombatState.AllCards : source.Owner.Deck.Cards;
        var available = cards.Where(card => card != source).Prepend(source).ToLookup(card => card.Id);
        IEnumerable<(CardModel Model, int Count)> members = definition;
        if (Group(source) is { } relation && relation.CardCounts.GetValueOrDefault(source.Id.ToString()) == 0)
            members = members.Prepend((source, 1));
        return members.SelectMany(member =>
        {
            var existing = available[member.Model.Id].Take(member.Count).ToList();
            while (existing.Count < member.Count)
            {
                var preview = (CardModel)member.Model.MutableClone();
                if (source.IsMutable) preview.Owner = source.Owner;
                existing.Add(preview);
            }
            return existing;
        }).ToArray();
    }
    private static void Merge(Player player, IEnumerable<CardModel> cards)
    {
        MergeInto(Groups(player), cards);
        Save(player);
    }

    private static void MergeCombat(Player player, IEnumerable<CardModel> cards)
    {
        var groups = CombatGroups.GetOrCreateValue(player.PlayerCombatState!);
        if (groups.Count == 0 && Groups(player).FirstOrDefault(group => group.EntranceId == PreferredEntrance) is { } permanent)
            groups.Add(permanent);
        MergeInto(groups, cards);
    }

    private static void MergeInto(List<KnowledgeRelation> groups, IEnumerable<CardModel> cards)
    {
        var counts = cards.GroupBy(card => card.Id.ToString()).ToDictionary(group => group.Key, group => group.Count());
        counts.TryAdd(PreferredEntrance, 1);
        KnowledgeRelation.Merge(groups, new(PreferredEntrance, counts), PreferredEntrance);
    }
    private static void Save(Player player)
    {
        var groups = Groups(player);
        var cache = Cache.GetOrCreateValue(player);
        string serialized = JsonSerializer.Serialize(groups);
        if (cache.Serialized == serialized) return;
        Relations.Set(player, cache.Serialized = serialized);
    }
    internal static void Normalize(Player player)
    {
        Save(player);
        var cards = player.Deck.Cards.Concat(player.PlayerCombatState?.AllCards ?? []).Distinct().ToArray();
        var legacy = cards.SelectMany(card => (Links.Get(card)?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Select(link => (Card: card, Link: link))).GroupBy(pair => pair.Link);
        foreach (var group in legacy) Merge(player, group.Select(pair => pair.Card));
        foreach (var card in cards)
        {
            if (!string.IsNullOrEmpty(Links.Get(card))) Links.Set(card, null!);
            if (IsLinked(card)) card.AddKeyword(JunnaKeywords.Knowledge);
        }
    }
    public static Task Prepare(PlayerChoiceContext context, CardModel selected, IEnumerable<CardModel> options)
    {
        if (CardCatalog.DesignId(selected) != "JK126") return Task.CompletedTask;
        var others = options.Where(c => c != selected && c.Pile == null).Distinct().ToArray();
        if (others.Length > 0) Pending.AddOrUpdate(selected, others);
        return Task.CompletedTask;
    }
    internal static bool HasPreparedSelection(CardModel card) => Pending.TryGetValue(card, out _);

    internal static int CombatPartnerCount(CardModel selected) => Pending.TryGetValue(selected, out var partners)
        ? partners.Count(CanGrantCombatPartner) : 0;

    private static bool CanGrantCombatPartner(CardModel card) => card.Pile == null
        && !card.HasBeenRemovedFromState && CardCatalog.CanGenerateAsKnowledgeMember(card);

    public static Task<bool> Acquire(CardModel acquired, PileType destination) => Acquire(acquired, destination, null);
    internal static async Task<bool> Acquire(CardModel acquired, PileType destination, ISet<CardModel>? expanded,
        CardPileAddResult? sourceResult = null)
    {
        if (Offers.TryGetValue(acquired, out var reward))
        {
            Offers.Remove(acquired);
            await Prepare(new BlockingPlayerChoiceContext(), acquired,
                reward.Reward.Cards.Where(card => card != reward.Original).Prepend(acquired));
        }
        if (!Pending.TryGetValue(acquired, out var partners)) return false;
        Pending.Remove(acquired);
        expanded?.UnionWith(partners);
        if (destination.IsCombatPile())
        {
            partners = partners.Where(CanGrantCombatPartner).ToArray();
            MergeCombat(acquired.Owner, partners.Prepend(acquired));
        }
        else Merge(acquired.Owner, partners.Prepend(acquired));
        foreach (var partner in partners)
        {
            Offers.Remove(partner);
            Pending.Remove(partner);
        }
        Normalize(acquired.Owner);
        await KnowledgeCombat.GrantPartnersWithPreview(acquired, destination, partners, sourceResult);
        return true;
    }

    internal static void CopyAcquisition(CardModel source, CardModel copy)
    {
        if (Offers.TryGetValue(source, out var offer)) Offers.AddOrUpdate(copy, offer);
        if (Pending.TryGetValue(source, out var partners)) Pending.AddOrUpdate(copy, partners);
    }

    internal static void Describe(CardModel card, Creature? target, ref string description)
    {
        if (!IsLinked(card) || card is JunnaCard junna && CardCatalog.Get(junna.DesignId).Knowledge) return;
        string keyword = new LocString("card_keywords", "SHOUJOKAGEKIJUNNA-KNOWLEDGE.title").GetFormattedText();
        description = $"[gold]{keyword}[/gold]。\n" + description;
    }

    [HarmonyPatch(typeof(CardReward), "OnSelect")]
    private static class RewardOptions
    {
        private static void Prefix(CardReward __instance)
        {
            if (!KnowledgeAcquisitionSources.IsKnownReward(__instance)) return;
            foreach (var card in __instance.Cards.Where(c => CardCatalog.DesignId(c) == "JK126"))
                Offers.AddOrUpdate(card, new(__instance, card));
        }
    }
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.GetKeywordsWithSources))]
    private static class LinkedKeyword
    {
        private static void Postfix(CardModel __instance, KeywordSources sources, ref IReadOnlySet<CardKeyword> __result)
        {
            if (sources.HasFlag(KeywordSources.Local) && IsLinked(__instance) && !__result.Contains(JunnaKeywords.Knowledge))
                __result = __result.Append(JunnaKeywords.Knowledge).ToHashSet();
        }
    }
    [HarmonyPatch(typeof(CardFactory), "CreateForReward", [typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardCreationOptions)])]
    private static class RewardEntrances
    {
        private static void Prefix(Player player, ref CardCreationOptions options)
        {
            options = options with { };
#if STS2_MAIN || STS2_BETA
            var originalFilter = options.CardPoolFilter;
            options.WithFilter(card => (originalFilter?.Invoke(card) ?? true) && IsRewardEntrance(player, card));
#else
            options = options.WithCustomPool(options.GetPossibleCards(player).Where(card => IsRewardEntrance(player, card)).ToArray());
#endif
        }
    }
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMerchantCardPool))]
    private static class MerchantEntrances
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(Player player, ref IEnumerable<CardModel> __result)
            => __result = __result.Where(card => IsRewardEntrance(player, card));
    }
    [HarmonyPatch(typeof(CardFactory), "GetFilteredTransformationOptions")]
    private static class TransformationEntrances
    {
        private static void Prefix(CardModel original, ref IEnumerable<CardModel> originalOptions)
            => originalOptions = originalOptions.Where(card => IsRewardEntrance(original.Owner, card));
    }
    [HarmonyPatch(typeof(CardModel), "get_HoverTips")]
    private static class RelationTip
    {
        private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
        {
            if (!IsLinked(__instance)) return;
#if STS2_MAIN || STS2_BETA
            string groupTipId = new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_GROUP"), "").Id;
#endif
            __result = __result.Where(t => t is not HoverTip existing ||
#if STS2_MAIN || STS2_BETA
                existing.Id != groupTipId);
#else
                existing.Id?.StartsWith("junna_knowledge_group_") != true && existing.Id != "junna_dynamic_knowledge_group");
#endif
            var names = Members(__instance)
                .Select(card => "[gold]" + (card is JunnaCard junna
                    ? CardCatalog.Model(junna.DesignId).Title : ((CardModel)card.CanonicalInstance).Title) + "[/gold]").ToList();
            if (names.Count == 0) return;
            var tip = new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_GROUP"),
                string.Join("\n", names))
                {
#if !STS2_MAIN && !STS2_BETA
                    Id = "junna_dynamic_knowledge_group",
#endif
                };
            var tips = __result.ToList();
            string knowledgeId = HoverTipFactory.FromKeyword(CardCatalog.DesignId(__instance) == "JK126"
                ? JunnaKeywords.ChainKnowledge : JunnaKeywords.Knowledge).Id;
            int knowledgeIndex = tips.FindIndex(existing => existing.Id == knowledgeId);
            tips.Insert(knowledgeIndex + 1, tip);
            __result = tips;
        }
    }
}
