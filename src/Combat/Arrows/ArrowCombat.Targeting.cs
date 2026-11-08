using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using HarmonyLib;

namespace ShoujoKagekiJunna.Combat;

public static partial class ArrowCombat
{
    private static readonly Dictionary<Creature, Creature[]> DeathOrders = [];

    public static void RetargetEnemies(MegaCrit.Sts2.Core.Entities.Players.Player player, Creature target)
    {
        foreach (var arrow in Arrows.Where(a => a.Card.Owner == player && !a.Resolving &&
            a.Card.TargetType == TargetType.AnyEnemy && a.Card.IsValidTarget(target)).ToArray())
            arrow.Retarget(target);
    }

    internal static void RememberDeathOrder(Creature creature)
    {
        if (creature.IsEnemy && creature.CombatState is { } state)
            DeathOrders[creature] = state.Enemies.ToArray();
    }

    internal static bool AwaitingReturn(ArrowDeployment arrow)
    {
        if (arrow.Card.CombatState is not { } state) return false;
        if ((arrow.TargetsAllEnemies || arrow.Target?.IsEnemy == true) &&
            !state.Enemies.Any(target => target.IsAlive && target.IsHittable) &&
            !CombatManager.Instance.IsOverOrEnding) return true;
        var targets = arrow.Targets.ToArray();
        return targets.Length > 0 && !targets.Any(t => t.IsHittable) && targets.Any(target =>
            state.ContainsCreature(target) && (target.IsAlive || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, target)));
    }

    internal static Creature? ResolveEffectTarget(Creature target, ICombatState state)
    {
        bool Available(Creature creature) => state.ContainsCreature(creature) && creature.IsAlive && creature.IsHittable;
        if (Available(target)) return target;
        // A damage hook may create its arrow before the pending death notification.
        // Keep that target until the normal death handler can retarget the deployment.
        if (!DeathOrders.TryGetValue(target, out var order)) return state.Enemies.FirstOrDefault(Available);
        int index = Array.IndexOf(order, target);
        return index < 0 ? null : order.Skip(index + 1).FirstOrDefault(Available)
            ?? order.Take(index).Reverse().FirstOrDefault(Available)
            ?? state.Enemies.FirstOrDefault(Available);
    }

    internal static Task AfterTargetDeath(Creature creature, ICombatState? state)
    {
        // Keep the death-time order until combat ends: effects can create their arrow after
        // the damage command has already removed the original target.
        DeathOrders.TryGetValue(creature, out var order);
        // These predicates also cover modded rebirth and transformation powers.
        if (!creature.IsEnemy || creature.IsAlive || state == null) return Task.CompletedTask;
        order ??= state.Enemies.ToArray();
        int index = Array.IndexOf(order, creature);
        if (index < 0) return Task.CompletedTask;
        bool Available(Creature target) => state.ContainsCreature(target) && target.IsAlive && target.IsHittable;
        var replacement = order.Skip(index + 1).FirstOrDefault(Available)
            ?? order.Take(index).Reverse().FirstOrDefault(Available)
            ?? state.Enemies.FirstOrDefault(Available);
        foreach (var arrow in Arrows.Where(a => !a.TargetsGroup && a.Target == creature).ToArray())
            if (replacement != null) arrow.Retarget(replacement);
        // A later death listener or wave transition may still create enemies. Combat end owns cleanup.
        return Task.CompletedTask;
    }
}

[HarmonyPatch(typeof(CreatureCmd), "KillWithoutCheckingWinCondition")]
internal static class ArrowDeathSettlementPatch
{
    private static void Prefix(Creature creature, out ICombatState? __state) => __state = creature.CombatState;
    private static void Postfix(Creature creature, ICombatState? __state, ref Task __result)
        => __result = Complete(__result, creature, __state);
    private static async Task Complete(Task task, Creature creature, ICombatState? state)
    {
        await task;
        await ArrowCombat.AfterTargetDeath(creature, state);
    }
}
