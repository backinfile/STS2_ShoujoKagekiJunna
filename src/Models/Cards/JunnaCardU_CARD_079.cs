using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_079
{
    internal override ArrowAcceleration Acceleration => new(ArrowAccelerationTargets.Target, Value("Turns"));

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
        if (play.Target is { } arrowTarget)
            await ArrowCombat.AccelerateFromCard(context, this, arrowTarget);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
