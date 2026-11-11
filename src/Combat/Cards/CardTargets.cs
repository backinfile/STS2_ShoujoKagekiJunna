using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Combat;

internal static class CardTargets
{
    internal static bool IsValid(CardModel card, Creature? target) =>
        card.TargetType is not (TargetType.AnyEnemy or TargetType.AnyAlly)
        || target is { IsAlive: true } && card.IsValidTarget(target);

    internal static Creature[] Enemies(CardModel card) => card.CombatState!.HittableEnemies
        .Where(target => IsValid(card, target)).ToArray();

    internal static Creature? First(CardModel card) => card.TargetType switch
    {
        TargetType.AnyEnemy => Enemies(card).FirstOrDefault(),
        TargetType.AnyAlly => card.CombatState!.PlayerCreatures
            .FirstOrDefault(target => target != card.Owner.Creature && IsValid(card, target)),
        TargetType.Self or TargetType.AnyPlayer => card.Owner.Creature,
        _ => null
    };
}
