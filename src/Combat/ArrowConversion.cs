using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static class ArrowConversion
{
    private static readonly ConditionalWeakTable<CardModel, object> Converted = new();
    public static bool IsConverted(CardModel card) => Converted.TryGetValue(card, out _);
    public static void Convert(CardModel card)
    {
        if (!CardCatalog.IsArrow(card)) return;
        if (card is CatalogCard catalog) catalog.ConvertArrow();
        else
        {
            Converted.GetOrCreateValue(card);
            card.RemoveKeyword(JunnaKeywords.Arrow);
            card.EnergyCost.AddThisCombat(1);
        }
        card.InvokeEnergyCostChanged();
    }
    internal static void Describe(CardModel card, Creature? target, ref string description)
    {
        // Printed turns have their own plaque; converted cards omit that plaque as well.
        if (card is JunnaCard junna && CardCatalog.Get(junna.DesignId).Type == "箭矢")
            description = Regex.Replace(description, @"(?m)^\[blue\][^\r\n]*回合(?:后)?\[/blue\]。?\r?\n", "");
    }
    [HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
    private static class CloneConversion
    {
        private static void Postfix(AbstractModel __instance, AbstractModel __result)
        {
            if (__instance is CardModel card && __result is CardModel copied && IsConverted(card))
                Converted.GetOrCreateValue(copied);
        }
    }
}
