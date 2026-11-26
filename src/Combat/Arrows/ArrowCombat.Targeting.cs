using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;
using HarmonyLib;

namespace ShoujoKagekiJunna.Combat;

public static partial class ArrowCombat
{
    private static bool IsSingleEnemyArrow(CardModel card)
        => card.TargetType == TargetType.AnyEnemy && CardCatalog.IsArrow(card);

    public static void RetargetEnemies(MegaCrit.Sts2.Core.Entities.Players.Player player, Creature target)
    {
        foreach (var arrow in Deployed.Where(a => a.Card.Owner == player && !a.Resolving &&
            a.Card.TargetType == TargetType.AnyEnemy && a.Card.IsValidTarget(target)).ToArray())
            arrow.Retarget(target);
    }

    internal static void RememberDeathOrder(Creature creature)
    {
        if (creature.IsEnemy && creature.CombatState is { } state)
            For(state).DeathOrders[creature] = state.Enemies.ToArray();
    }

    internal static bool AwaitingReturn(ArrowDeployment arrow)
    {
        if (arrow.Card.CombatState is not { } state) return false;
        if (arrow.TargetsEnemies &&
            !state.Enemies.Any(target => target.IsAlive && target.IsHittable) &&
            !CombatManager.Instance.IsOverOrEnding) return true;
        var targets = arrow.Targets.ToArray();
        return targets.Length > 0 && !targets.Any(t => t.IsHittable) && targets.Any(target =>
            state.ContainsCreature(target) && (target.IsAlive || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, target)));
    }

    internal static Creature? ResolveEffectTarget(Creature? target, ICombatState state)
    {
        bool Available(Creature creature) => state.ContainsCreature(creature) && creature.IsAlive && creature.IsHittable;
        if (target == null) return state.Enemies.FirstOrDefault(Available);
        if (Available(target)) return target;
        // A damage hook may create its arrow before the pending death notification.
        // Keep that target until the normal death handler can retarget the deployment.
        if (!For(state).DeathOrders.TryGetValue(target, out var order)) return state.Enemies.FirstOrDefault(Available);
        int index = Array.IndexOf(order, target);
        return index < 0 ? null : NextTarget(state, order, index);
    }

    private static Creature? NextTarget(ICombatState state, Creature[] order, int index)
    {
        bool Available(Creature target) => state.ContainsCreature(target) && target.IsAlive && target.IsHittable;
        return order.Skip(index + 1).FirstOrDefault(Available)
            ?? order.Take(index).Reverse().FirstOrDefault(Available)
            ?? state.Enemies.FirstOrDefault(Available);
    }

    internal static Task AfterTargetDeath(Creature creature, ICombatState? state)
    {
        // Keep the death-time order until combat ends: effects can create their arrow after
        // the damage command has already removed the original target.
        // These predicates also cover modded rebirth and transformation powers.
        if (!creature.IsEnemy || creature.IsAlive || state == null) return Task.CompletedTask;
        if (!Battles.TryGetValue(state, out var battle)) return Task.CompletedTask;
        battle.DeathOrders.TryGetValue(creature, out var order);
        order ??= state.Enemies.ToArray();
        int index = Array.IndexOf(order, creature);
        if (index < 0) return Task.CompletedTask;
        var replacement = NextTarget(state, order, index);
        foreach (var arrow in battle.Arrows.Where(a => !a.TargetsGroup && a.Target == creature).ToArray())
            if (replacement != null) arrow.Retarget(replacement);
        // A later death listener or wave transition may still create enemies. Combat end owns cleanup.
        return Task.CompletedTask;
    }

    [HarmonyPatch]
    private static class AutoPlayTargeting
    {
        private static MethodBase TargetMethod() => AccessTools.AsyncMoveNext(
            AccessTools.Method(typeof(CardCmd), nameof(CardCmd.AutoPlay)));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => instructions
                .MethodReplacer(AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.TargetType)),
                    AccessTools.Method(typeof(AutoPlayTargeting), nameof(TargetTypeForAutoPlay)))
                .MethodReplacer(AccessTools.Method(typeof(CardModel), nameof(CardModel.OnEnqueuePlayVfx)),
                    AccessTools.Method(typeof(AutoPlayTargeting), nameof(EnqueuePlayVfx)));

        private static TargetType TargetTypeForAutoPlay(CardModel card)
            // An arrow can deploy now and find a hittable enemy when it resolves.
            => IsSingleEnemyArrow(card) && card.CombatState is { } combat &&
                !combat.Enemies.Any(enemy => enemy.IsAlive && enemy.IsHittable)
                ? TargetType.None
                : card.TargetType;

        private static Task EnqueuePlayVfx(CardModel card, Creature? target)
            => target == null && IsSingleEnemyArrow(card)
                ? Task.CompletedTask
                : card.OnEnqueuePlayVfx(target);
    }

    [HarmonyPatch]
    private static class PlayEnchantment
    {
        private static MethodBase TargetMethod() => AccessTools.AsyncMoveNext(
            AccessTools.Method(typeof(CardModel), nameof(CardModel.OnPlayWrapper)));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => instructions
                .MethodReplacer(AccessTools.Method(typeof(EnchantmentModel), nameof(EnchantmentModel.OnPlay)),
                    AccessTools.Method(typeof(PlayEnchantment), nameof(PlayOrDefer)))
                .MethodReplacer(AccessTools.Method(typeof(AbstractModel), nameof(AbstractModel.InvokeExecutionFinished)),
                    AccessTools.Method(typeof(PlayEnchantment), nameof(FinishExecution)));

        private static Task PlayOrDefer(EnchantmentModel enchantment, PlayerChoiceContext context, CardPlay play)
        {
            if (play.Target != null || !IsSingleEnemyArrow(play.Card)) return enchantment.OnPlay(context, play);
            ArrowCombat.Find(play.Card)?.DeferEnchantment(play.PlayIndex, enchantment);
            return Task.CompletedTask;
        }

        private static void FinishExecution(AbstractModel model)
        {
            if (model is EnchantmentModel { Card: { } card } && IsSingleEnemyArrow(card) && card.CurrentTarget == null)
                return;
            model.InvokeExecutionFinished();
        }
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
