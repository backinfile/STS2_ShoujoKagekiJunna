using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

[CustomID("JUNNA_ENERGY_RESERVE")]
public sealed class EnergyReserve : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + "images/powers/ENERGY_RESERVE.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
    private bool _consumed;
    private static readonly AsyncLocal<bool> SettingEnergy = new();
    public static Task Add(PlayerChoiceContext context, CardModel card, int amount)
        => Add(context, card.Owner, amount, card);
    public static async Task Add(PlayerChoiceContext context, Player player, int amount, CardModel? cardSource = null)
    {
        if (amount > 0) await PowerCmd.Apply<EnergyReserve>(context, player.Creature, amount, player.Creature, cardSource);
    }
    public override Task AfterEnergyReset(Player player) => player == Owner.Player && _consumed ? PowerCmd.Remove(this) : Task.CompletedTask;

    [HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.ResetEnergy))]
    private static class PreserveEnergy
    {
        private static void Prefix(PlayerCombatState __instance, Player ____player, out int __state)
        {
            var power = ____player.Creature.GetPower<EnergyReserve>();
            __state = power == null || power._consumed ? 0 : Math.Min(__instance.Energy, power.Amount);
            if (power != null) power._consumed = true;
        }
        private static void Postfix(PlayerCombatState __instance, int __state) => __instance.Energy += __state;
    }
    [HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.AddMaxEnergyToCurrent))]
    private static class ExistingRetention
    {
        private static void Prefix(Player ____player)
        {
            if (____player.Creature.GetPower<EnergyReserve>() is { } power) power._consumed = true;
        }
    }
    [HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy))]
    private static class GainEvent
    {
        private static void Prefix(Player player, out int __state) => __state = player.PlayerCombatState?.Energy ?? 0;
        private static void Postfix(Player player, int __state, ref Task __result) => __result = Complete(__result, player, __state);
        private static async Task Complete(Task task, Player player, int before)
        {
            await task;
            if (!SettingEnergy.Value && player.PlayerCombatState?.Energy > before)
                await CatalogPower.EnergyChanged(player, player.PlayerCombatState.Energy - before);
        }
    }
    [HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseEnergy))]
    private static class LossEvent
    {
        private static void Prefix(Player player, out int __state) => __state = player.PlayerCombatState?.Energy ?? 0;
        private static void Postfix(Player player, int __state, ref Task __result) => __result = Complete(__result, player, __state);
        private static async Task Complete(Task task, Player player, int before)
        {
            await task;
            if (!SettingEnergy.Value && player.PlayerCombatState?.Energy < before)
                await CatalogPower.EnergyChanged(player, before - player.PlayerCombatState.Energy);
        }
    }
    [HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.SetEnergy))]
    private static class SetEvent
    {
        private static bool Prefix(decimal amount, Player player, ref Task __result)
        {
            if (SettingEnergy.Value) return true;
            __result = Set(amount, player);
            return false;
        }
        private static async Task Set(decimal amount, Player player)
        {
            SettingEnergy.Value = true;
            try { await PlayerCmd.SetEnergy(amount, player); }
            finally { SettingEnergy.Value = false; }
        }
    }
}
