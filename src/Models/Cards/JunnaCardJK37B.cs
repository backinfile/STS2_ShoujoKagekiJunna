using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK37B
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        await Attack(context, play);
        if (play.Target is { IsAlive: true } redirectedTarget) ArrowCombat.RetargetEnemies(player, redirectedTarget);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
