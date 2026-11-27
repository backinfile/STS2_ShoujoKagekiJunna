using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_184
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
        => await Attack(context, CardActions.ResolveTarget(play));

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
