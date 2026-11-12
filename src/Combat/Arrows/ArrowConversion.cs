using System.Runtime.CompilerServices;
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
    internal static void Copy(CardModel source, CardModel copy)
    {
        if (IsConverted(source)) Converted.GetOrCreateValue(copy);
    }
}
