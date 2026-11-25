using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK37 : ICombatOpeningCard
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
    }

    Task ICombatOpeningCard.PlayAtCombatOpening(PlayerChoiceContext context)
    {
        var target = Owner.Creature.CombatState!.HittableEnemies.FirstOrDefault(enemy => enemy.IsAlive && IsValidTarget(enemy));
        return target == null ? Task.CompletedTask : CardCmd.AutoPlay(context, this, target);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
