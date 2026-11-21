using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardB
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardPileCmd.Draw(context, Value("DrawCount"), Owner);
        await PlayerCmd.GainEnergy(Value("Energy"), Owner);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
