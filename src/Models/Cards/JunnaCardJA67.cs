using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA67
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
        if (ArrowCombat.Find(this)?.ExhaustsWhenResolved != true)
            await CardActions.FreePlay(context, this, play.Target, keepTarget: true);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
