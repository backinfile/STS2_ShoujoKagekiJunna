using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaPowerJK400B
{
    protected override string IconId => "JZ05";

    protected override Task TurnEnding(Grant grant, PlayerChoiceContext context)
        => CardActions.Fly(context, Owner.Player!, grant.V("Damage"));
}
