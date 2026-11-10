using System.Runtime.CompilerServices;
using System.Text.Json;
using BaseLib.Patches.Saves;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
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

/// <summary>Run-wide model-ID relations, including cards from other characters.</summary>
public static class DynamicKnowledge
{
    // Retained only to migrate groups saved by the former per-card implementation.
    private static readonly SavedSpireField<CardModel, string> Links = new(() => null, "JunnaKnowledgeLinks");
    private static readonly SavedSpireField<Player, string> Relations = new(() => null, "JunnaKnowledgeRelations");
    private sealed class RelationCache
    {
        public string? Serialized;
        public List<KnowledgeRelation> Groups = [];
    }
    private static readonly ConditionalWeakTable<Player, RelationCache> Cache = new();
    private static readonly ConditionalWeakTable<CardModel, CardModel[]> Pending = new();
    private static readonly ConditionalWeakTable<CardModel, CardReward> Offers = new();
    public static void Initialize() { }
    private static string PreferredEntrance => CardCatalog.Model("JK126").Id.ToString();
    private static List<KnowledgeRelation> Groups(Player player)
    {
        var cache = Cache.GetOrCreateValue(player);
        string? serialized = Relations.Get(player);
        if (cache.Serialized != serialized)
        {
            cache.Groups = string.IsNullOrEmpty(serialized) ? [] : JsonSerializer.Deserialize<List<KnowledgeRelation>>(serialized)!;
            cache.Serialized = serialized;
        }
        return cache.Groups;
    }
    internal static KnowledgeRelation? Group(CardModel card) => card.IsMutable && card.Owner is { } owner
        ? Groups(owner).FirstOrDefault(group => group.EntranceId == card.Id.ToString()) : null;
    public static bool IsLinked(CardModel card) => Group(card) != null;
    public static bool IsRewardEntrance(Player player, CardModel card)
        => card.Pool is not JunnaCardPool || CardCatalog.IsRewardCard(card);
    internal static IEnumerable<(CardModel Model, int Count)>? Definition(CardModel card) => Group(card)?.Members
        .Select(member => (ModelDb.GetById<CardModel>(ModelId.Deserialize(member.Id)), member.Count));
    public static IReadOnlyList<CardModel> Members(CardModel source)
    {
        var group = Group(source);
        if (group == null) return [];
        var cards = source.Pile?.IsCombatPile == true && source.Owner.PlayerCombatState != null
            ? source.Owner.PlayerCombatState.AllCards : source.Owner.Deck.Cards;
        var available = cards.Where(card => card != source).Prepend(source).ToLookup(card => card.Id.ToString());
        return group.Members.SelectMany(member =>
        {
            var existing = available[member.Id].Take(member.Count).ToList();
            while (existing.Count < member.Count)
            {
                var preview = (CardModel)ModelDb.GetById<CardModel>(ModelId.Deserialize(member.Id)).MutableClone();
                preview.Owner = source.Owner;
                existing.Add(preview);
            }
            return existing;
        }).ToArray();
    }
    private static void Merge(Player player, IEnumerable<CardModel> cards)
    {
        var groups = Groups(player);
        var counts = cards.GroupBy(card => card.Id.ToString()).ToDictionary(group => group.Key, group => group.Count());
        counts.TryAdd(PreferredEntrance, 1);
        KnowledgeRelation.Merge(groups, new(PreferredEntrance, counts), PreferredEntrance);
        Relations.Set(player, JsonSerializer.Serialize(groups));
    }
    internal static void Normalize(Player player)
    {
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
    [HarmonyPatch(typeof(Player), nameof(Player.FromSerializable))]
    private static class RestoreGroups
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(Player __result) => Normalize(__result);
    }
    [HarmonyPatch(typeof(Player), nameof(Player.SyncWithSerializedPlayer))]
    private static class SyncGroups
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(Player __instance, SerializablePlayer player)
        {
            // Native player synchronization does not import BaseLib's extended player fields.
            var saved = ExtendedSaveHandlers<Player, SerializablePlayer>.ExtendedData.Get(player).DictForType<string>();
            Relations.Set(__instance, saved.GetValueOrDefault("spirefield_" + Relations.Name)!);
            Normalize(__instance);
        }
    }
    public static Task Prepare(PlayerChoiceContext context, CardModel selected, IEnumerable<CardModel> options)
    {
        if (CardCatalog.DesignId(selected) != "JK126") return Task.CompletedTask;
        var others = options.Where(c => c != selected && c.Pile == null).Distinct().ToArray();
        if (others.Length > 0) Pending.AddOrUpdate(selected, others);
        return Task.CompletedTask;
    }
    public static async Task<bool> Acquire(CardModel acquired, PileType destination)
    {
        if (Offers.TryGetValue(acquired, out var reward))
        {
            Offers.Remove(acquired);
            await Prepare(new BlockingPlayerChoiceContext(), acquired, reward.Cards);
        }
        if (!Pending.TryGetValue(acquired, out var partners)) return false;
        Pending.Remove(acquired);
        Merge(acquired.Owner, partners.Prepend(acquired));
        foreach (var partner in partners)
        {
            Offers.Remove(partner);
            Pending.Remove(partner);
        }
        Normalize(acquired.Owner);
        await KnowledgeCombat.GrantPartners(acquired, destination, partners);
        return true;
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
            foreach (var card in __instance.Cards.Where(c => CardCatalog.DesignId(c) == "JK126"))
                Offers.AddOrUpdate(card, __instance);
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
#if STS2_BETA
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
            var members = Members(__instance);
            if (members.Count <= 1) return;
            var names = members.Select(card => "[gold]" + (card is JunnaCard junna
                ? CardCatalog.Model(junna.DesignId).Title : ((CardModel)card.CanonicalInstance).Title) + "[/gold]");
            var tip = new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_GROUP"),
                string.Join("\n", names))
                { Id = "junna_dynamic_knowledge_group" };
            __result = __result.Where(t => t is not HoverTip existing ||
                existing.Id?.StartsWith("junna_knowledge_group_") != true && existing.Id != "junna_dynamic_knowledge_group").Append(tip);
        }
    }
}
