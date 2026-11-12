using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>An X-cost exchanged for a number still resolves its X payload from the amount actually paid.</summary>
public static class CostChanges
{
    private sealed class FormerX { public int Captured; }
    private static readonly ConditionalWeakTable<CardModel, FormerX> FixedX = new();
    internal static void CaptureFreePayment(CardModel card)
    { if (FixedX.TryGetValue(card, out var state)) state.Captured = 0; }
    public static void SetFixed(CardModel card, int amount)
    {
        if (card.EnergyCost.CostsX)
        {
            FixedX.GetOrCreateValue(card).Captured = card.EnergyCost.CapturedXValue;
            var cost = new CardEnergyCost(card, card.EnergyCost.Canonical, false);
            AccessTools.Field(typeof(CardModel), "_energyCost").SetValue(card, cost);
        }
        card.EnergyCost.SetThisCombat(amount);
        card.InvokeEnergyCostChanged();
    }
    [HarmonyPatch(typeof(CardModel), "SpendEnergy")]
    private static class CapturePayment
    {
        private static void Prefix(CardModel __instance, int amount)
        { if (FixedX.TryGetValue(__instance, out var state)) state.Captured = amount; }
    }
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.ResolveEnergyXValue))]
    private static class ResolveFixedX
    {
        private static bool Prefix(CardModel __instance, ref int __result)
        {
            if (!FixedX.TryGetValue(__instance, out var state)) return true;
            __result = Hook.ModifyXValue(__instance.CombatState!, __instance, state.Captured);
            return false;
        }
    }
    internal static void Copy(CardModel source, CardModel copy)
    {
        if (!FixedX.TryGetValue(source, out var state)) return;
        FixedX.GetOrCreateValue(copy).Captured = state.Captured;
        var cost = new CardEnergyCost(copy, copy.EnergyCost.Canonical, false);
        cost.SetThisCombat(source.EnergyCost.GetWithModifiers(CostModifiers.Local));
        AccessTools.Field(typeof(CardModel), "_energyCost").SetValue(copy, cost);
    }
}
