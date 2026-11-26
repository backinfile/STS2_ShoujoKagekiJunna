using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ShoujoKagekiJunna.Combat;

internal enum ArrowAccelerationTargets { None, All, Self, SelfOrUntargeted, Enemies, Target }

internal readonly record struct ArrowAcceleration(ArrowAccelerationTargets Targets, int Turns)
{
    internal bool Matches(ArrowDeployment arrow, Player player, Creature? target = null)
        => arrow.Card.Owner == player && (Targets switch
        {
            ArrowAccelerationTargets.All => true,
            ArrowAccelerationTargets.Self => !arrow.TargetsGroup && arrow.Target == player.Creature,
            ArrowAccelerationTargets.SelfOrUntargeted => !arrow.TargetsGroup && !arrow.TargetsEnemies &&
                (arrow.Target == player.Creature || arrow.Target == null),
            ArrowAccelerationTargets.Enemies => arrow.TargetsEnemies,
            ArrowAccelerationTargets.Target => target != null && arrow.Targets.Contains(target),
            _ => false
        });
}
