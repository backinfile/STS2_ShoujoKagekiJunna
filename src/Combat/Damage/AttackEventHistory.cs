using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Turn damage history exists before a trigger power is installed.</summary>
internal static class AttackEventHistory
{
    private sealed class Counter { public int Turn = -1; public int Count; }
    private sealed record Event(int Ordinal);
    private static readonly ConditionalWeakTable<PlayerCombatState, Counter> Players = new();
    private static readonly ConditionalWeakTable<DamageResult, Event> Events = new();
    public static int Ordinal(DamageResult result) => Events.TryGetValue(result, out var entry) ? entry.Ordinal : 0;

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
    private static class RecordDamage
    {
        private static void Prefix(Creature? dealer, DamageResult results, ValueProp props, Creature target, CardModel? cardSource)
        {
            if (dealer?.Player?.PlayerCombatState is not { } player || target.Side == dealer.Side ||
                results.TotalDamage <= 0 || !props.IsPoweredAttack() || Events.TryGetValue(results, out _)) return;
            var counter = Players.GetOrCreateValue(player);
            if (counter.Turn != player.TurnNumber) { counter.Turn = player.TurnNumber; counter.Count = 0; }
            Events.Add(results, new(++counter.Count));
        }
    }
}
