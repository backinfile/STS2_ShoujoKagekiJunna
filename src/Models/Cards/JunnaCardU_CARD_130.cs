using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_130
{
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
        => Attack(context, CardActions.ResolveTarget(play));

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
