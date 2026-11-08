using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Rooms;

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
            DeathOrders[creature] = state.Enemies.Select((enemy, index) => (enemy,
                x: NCombatRoom.Instance?.GetCreatureNode(enemy)?.VfxSpawnPosition.X ?? index))
                .OrderBy(pair => pair.x).Select(pair => pair.enemy).ToArray();
    }

    internal static bool AwaitingReturn(ArrowDeployment arrow)
    {
        if (arrow.Card.CombatState is not { } state) return false;
        var targets = arrow.Targets.ToArray();
        return targets.Length > 0 && !targets.Any(t => t.IsHittable) && targets.Any(target =>
            state.ContainsCreature(target) && (target.IsAlive || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, target)));
    }

    internal static Creature? ResolveEffectTarget(Creature target, ICombatState state)
    {
        bool Available(Creature creature) => state.ContainsCreature(creature) &&
            (creature.IsAlive || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, creature));
        if (Available(target)) return target;
        // A damage hook may create its arrow before the pending death notification.
        // Keep that target until the normal death handler can retarget the deployment.
        if (!DeathOrders.TryGetValue(target, out var order)) return state.ContainsCreature(target) ? target : null;
        int index = Array.IndexOf(order, target);
        return index < 0 ? null : order.Skip(index + 1).FirstOrDefault(Available)
            ?? order.Take(index).Reverse().FirstOrDefault(Available);
    }

    internal static async Task AfterTargetDeath(Creature creature, bool wasRemovalPrevented)
    {
        // Keep the death-time order until combat ends: effects can create their arrow after
        // the damage command has already removed the original target.
        DeathOrders.TryGetValue(creature, out var order);
        var state = creature.CombatState;
        // These predicates also cover modded rebirth and transformation powers.
        if (!creature.IsEnemy || wasRemovalPrevented || creature.IsAlive || state == null ||
            !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, creature)) return;
        order ??= state.Enemies.ToArray();
        int index = Array.IndexOf(order, creature);
        if (index < 0) return;
        bool Available(Creature target) => state.ContainsCreature(target) &&
            (target.IsAlive || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, target));
        var replacement = order.Skip(index + 1).FirstOrDefault(Available)
            ?? order.Take(index).Reverse().FirstOrDefault(Available);
        bool anyEnemyRemains = order.Any(Available);
        foreach (var arrow in Arrows.Where(a => !a.TargetsGroup && a.Target == creature || a.TargetsAllEnemies && !anyEnemyRemains).ToArray())
        {
            if (replacement != null) arrow.Retarget(replacement);
            else if (!arrow.Resolving)
            {
                using var pileExit = new ArrowPileExit(NCombatRoom.Instance?.GetNodeOrNull<ArrowHud>("JunnaArrows")?.PileExitPosition(arrow.Card));
                Arrows.Remove(arrow);
                HandArrow.Restore(arrow.Card);
                if (arrow.Card.Pile?.Type == ArrowPile.PileId) await CardPileCmd.Add(arrow.Card, PileType.Discard);
            }
        }
    }
}
