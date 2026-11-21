using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_079
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        await Attack(context, play);
        if (play.Target is { } arrowTarget)
            await ArrowCombat.AccelerateTarget(context, player, arrowTarget, Value("Turns"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
