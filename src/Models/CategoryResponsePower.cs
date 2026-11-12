using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaPowerU_CARD_016
{
    protected override string IconId => "JK127";

    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        int category = new[] { 1, 0, 2 }[grant.CycleIndex];
        if (!CardTutors.Matches(play.Card, category)) return;
        // Advance once for this play, including a full hand or a blocked draw.
        // Each installed grant retains its own position across turns.
        grant.CycleIndex = (grant.CycleIndex + 1) % 3;
        await CardPileCmd.Draw(context, grant.V("DrawCount"), Owner.Player!);
    }
}
