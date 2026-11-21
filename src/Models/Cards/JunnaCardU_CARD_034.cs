using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_034
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await CardActions.DrawWithArrowOverflow(context, player, Value("DrawCount"), Value("HandArrowTurns"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
