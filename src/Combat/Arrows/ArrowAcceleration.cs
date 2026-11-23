using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal enum ArrowAccelerationTargets { None, All, Self, SelfOrUntargeted, Enemies, Target }

internal readonly record struct ArrowAcceleration(ArrowAccelerationTargets Targets, int Turns)
{
    internal static ArrowAcceleration ForCard(CatalogCard card)
    {
        var targets = card.DesignId switch
        {
            "JA43" or "JA71" or "JA177" or "JK45B" or "JK184B" => ArrowAccelerationTargets.All,
            "U_CARD_063" => ArrowAccelerationTargets.Self,
            "JA94" => ArrowAccelerationTargets.SelfOrUntargeted,
            "JK84B" => ArrowAccelerationTargets.Enemies,
            "U_CARD_079" => ArrowAccelerationTargets.Target,
            _ => ArrowAccelerationTargets.None
        };
        return new(targets, targets == ArrowAccelerationTargets.None ? 0
            : card.DesignId == "JA71" ? int.MaxValue : card.Value("Turns"));
    }

    internal bool Matches(ArrowDeployment arrow, Player player, Creature? target = null)
        => arrow.Card.Owner == player && (Targets switch
        {
            ArrowAccelerationTargets.All => true,
            ArrowAccelerationTargets.Self => !arrow.TargetsGroup && arrow.Target == player.Creature,
            ArrowAccelerationTargets.SelfOrUntargeted => !arrow.TargetsGroup && (arrow.Target == player.Creature || arrow.Target == null),
            ArrowAccelerationTargets.Enemies => arrow.TargetsAllEnemies || arrow.Target?.IsEnemy == true,
            ArrowAccelerationTargets.Target => target != null && arrow.Targets.Contains(target),
            _ => false
        });
}
