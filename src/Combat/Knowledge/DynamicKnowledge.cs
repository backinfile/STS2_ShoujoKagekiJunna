using System.Runtime.CompilerServices;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Links identify physical knowledge groups, including cards from other characters.</summary>
public static class DynamicKnowledge
{
    private static readonly SavedSpireField<CardModel, string> Links = new(() => null, "JunnaKnowledgeLinks");
    private static readonly SavedSpireField<Player, int> NextGroup = new(() => 0, "JunnaKnowledgeGroupSequence");
    private static readonly ConditionalWeakTable<CardModel, CardModel[]> Pending = new();
    private static readonly ConditionalWeakTable<CardModel, CardReward> Offers = new();
    public static void Initialize() { }
    public static bool IsLinked(CardModel card) => !string.IsNullOrEmpty(Links.Get(card));
    private static string[] Groups(CardModel card) => Links.Get(card)?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];
    public static IReadOnlyList<CardModel> Members(CardModel source)
    {
        if (!source.IsMutable || source.Owner == null || !IsLinked(source)) return [];
        var groups = Groups(source).ToHashSet();
        var cards = source.Pile?.IsCombatPile == true && source.Owner.PlayerCombatState != null
            ? source.Owner.PlayerCombatState.AllCards : source.Owner.Deck.Cards;
        // Older acquisitions linked the paper but omitted its physical research companions.
        var research = cards.Where(c => Groups(c).Any(groups.Contains)).Select(KnowledgeProgress.ResearchGroup)
            .Where(link => !string.IsNullOrEmpty(link)).ToHashSet();
        foreach (var member in cards.Where(c => research.Contains(KnowledgeProgress.ResearchGroup(c))))
            if (KnowledgeProgress.ResearchGroup(member) != null)
                Links.Set(member, string.Join("\n", Groups(member).Concat(groups).Distinct()));
        var members = cards.Where(c => Groups(c).Any(groups.Contains)).ToArray();
        return members.Length > 0 ? members : [source];
    }
    public static async Task Prepare(PlayerChoiceContext context, CardModel selected, IEnumerable<CardModel> options)
    {
        if (CardCatalog.DesignId(selected) != "JK126" || IsLinked(selected)) return;
        var others = options.Where(c => c != selected && c.Pile == null).Distinct().ToArray();
        var partners = others.Length <= 2 ? others : await CardActions.Choose(context, selected, others, 2);
        if (partners.Length > 0) Pending.AddOrUpdate(selected, partners);
    }
    public static async Task Acquire(CardModel acquired, PileType destination)
    {
        if (Offers.TryGetValue(acquired, out var reward))
        {
            Offers.Remove(acquired);
            await Prepare(new BlockingPlayerChoiceContext(), acquired, reward.Cards);
        }
        if (!Pending.TryGetValue(acquired, out var partners)) return;
        Pending.Remove(acquired);
        int sequence = checked(NextGroup.Get(acquired.Owner) + 1);
        NextGroup.Set(acquired.Owner, sequence);
        string group = acquired.Owner.NetId + "/" + sequence;
        void Link(CardModel member)
        {
            Links.Set(member, string.Join("\n", Groups(member).Append(group).Distinct()));
            member.AddKeyword(JunnaKeywords.Knowledge);
        }
        Link(acquired);
        foreach (var partner in partners)
        {
            // Ordinary knowledge entrances still acquire their existing static companions.
            // No new choice context is created for an attached dynamic entrance.
            Offers.Remove(partner);
            Pending.Remove(partner);
            using (KnowledgeCombat.SingleCard())
            {
                if (destination == PileType.Deck) await CardPileCmd.Add(partner, PileType.Deck);
                else await CardPileCmd.AddGeneratedCardToCombat(partner, destination, acquired.Owner);
            }
            if (partner.Pile?.Type == destination)
                foreach (var member in await KnowledgeCombat.GrantPartners(partner, destination)) Link(member);
        }
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
    [HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
    private static class CopyLinks
    {
        private static void Postfix(AbstractModel __instance, AbstractModel __result)
        {
            if (__instance is not CardModel original || __result is not CardModel copy || !IsLinked(original)) return;
            Links.Set(copy, Links.Get(original));
            copy.AddKeyword(JunnaKeywords.Knowledge);
        }
    }
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable))]
    private static class RestoreLinks
    {
        private static void Postfix(CardModel __result)
        {
            if (IsLinked(__result)) __result.AddKeyword(JunnaKeywords.Knowledge);
        }
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
