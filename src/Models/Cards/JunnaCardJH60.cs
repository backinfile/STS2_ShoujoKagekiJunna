using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJH60
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardChoices.EnergyTiming(context, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
