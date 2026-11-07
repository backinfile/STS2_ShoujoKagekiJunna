using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Additional payment buys one payload; arrows keep all payloads in one deployment.</summary>
internal static class PaidReplay
{
    private sealed record Payment(int[] XValues);
    private static readonly ConditionalWeakTable<CardModel, Payment> Payments = new();
    internal static int[] Capture(CardModel card) => Payments.TryGetValue(card, out var payment) ? payment.XValues : [];
    internal static void PrepareEffect(CardModel card, int index)
    {
        var values = Capture(card);
        if (card.EnergyCost.CostsX && index < values.Length) card.EnergyCost.CapturedXValue = values[index];
    }
    private static async Task<int> AddPayments(Task<int> generated, CardModel card)
    {
        int count = await generated;
        Payments.Remove(card);
        if (HandArrow.OriginalType(card) == CardType.Power) return count;
        var power = card.Owner.Creature.Powers.OfType<CatalogPower>().FirstOrDefault(p => p.DesignId == "JH19");
        if (power == null) return count;
        int grants = power.ReplayGrants;
        await PowerCmd.Remove(power);
        int originalX = card.EnergyCost.CostsX ? card.EnergyCost.CapturedXValue : 0;
        var values = Enumerable.Repeat(originalX, count).ToList();
        for (int i = 0; i < grants; i++)
        {
            if (!card.Owner.PlayerCombatState!.HasEnoughResourcesFor(card, out _)) break;
            var (energy, _) = await card.SpendResources();
            values.Add(card.EnergyCost.CostsX ? energy : originalX);
        }
        if (card.EnergyCost.CostsX) card.EnergyCost.CapturedXValue = originalX;
        Payments.Add(card, new(values.ToArray()));
        return values.Count;
    }
    [HarmonyPatch]
    private static class GenerateCount
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(CardModel), "GeneratePlayCount");
        private static void Postfix(CardModel __instance, ref Task<int> __result) => __result = AddPayments(__result, __instance);
    }
}
