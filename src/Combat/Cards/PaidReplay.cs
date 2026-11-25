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
        var power = card.Owner.Creature.GetPower<JunnaPowerJH19>();
        if (power == null) return count;
        var values = await power.BuyReplays(card, count);
        Payments.Add(card, new(values));
        return values.Length;
    }
    [HarmonyPatch]
    private static class GenerateCount
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(CardModel), "GeneratePlayCount");
        private static void Postfix(CardModel __instance, ref Task<int> __result) => __result = AddPayments(__result, __instance);
    }
}
