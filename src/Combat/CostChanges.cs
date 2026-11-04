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
    [HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.Clone))]
    private static class CloneFixedX
    {
        private static void Postfix(CardModel ____card, CardModel newCard, ref CardEnergyCost __result)
        {
            if (!FixedX.TryGetValue(____card, out var state)) return;
            FixedX.GetOrCreateValue(newCard).Captured = state.Captured;
            var fixedCost = new CardEnergyCost(newCard, __result.Canonical, false);
            fixedCost.SetThisCombat(____card.EnergyCost.GetWithModifiers(CostModifiers.Local));
            __result = fixedCost;
        }
    }
}
