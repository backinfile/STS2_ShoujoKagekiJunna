using System.Runtime.CompilerServices;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
    private static readonly ConditionalWeakTable<CardModel, CardModel[]> Pending = new();
    private static readonly ConditionalWeakTable<CardModel, CardReward> Offers = new();
    public static void Initialize() => KnowledgeRelations.Initialize();
    public static bool IsLinked(CardModel card) => KnowledgeRelations.IsLinked(card);
    public static IReadOnlyList<CardModel> Members(CardModel source) => KnowledgeRelations.Members(source);
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
        string group = KnowledgeRelations.CreateGroup(acquired.Owner);
        KnowledgeRelations.Link(acquired, [group]);
        foreach (var partner in partners)
        {
            Offers.Remove(partner);
            Pending.Remove(partner);
            CardModel actual = partner;
            using (KnowledgeCombat.SingleCard())
            {
                if (destination == PileType.Deck)
                {
                    var result = await CardPileCmd.Add(partner, PileType.Deck);
                    if (!result.success) continue;
                    actual = result.cardAdded;
                }
                else await CardPileCmd.AddGeneratedCardToCombat(partner, destination, acquired.Owner);
            }
            if (actual.Pile?.Type != destination) continue;
            foreach (var member in await KnowledgeCombat.GrantPartners(actual, destination))
                KnowledgeRelations.Link(member, [group]);
        }
        KnowledgeRelations.Normalize(acquired.Owner);
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
            if (__instance is not CardModel original || __result is not CardModel copy || !IsLinked(original) || original.Id != copy.Id) return;
            KnowledgeRelations.Copy(original, copy);
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
            var tip = new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_GROUP"),
                string.Join("\n", members.Select(c => "[gold]" + c.Title + "[/gold]")))
                { Id = "junna_dynamic_knowledge_group" };
            __result = __result.Append(tip);
        }
    }
}
