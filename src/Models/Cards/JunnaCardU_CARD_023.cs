using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_023
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        await CardPileCmd.Draw(context, Value("DrawCount"), Owner);
        await HandArrow.Select(context, this, Value("Cards"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
