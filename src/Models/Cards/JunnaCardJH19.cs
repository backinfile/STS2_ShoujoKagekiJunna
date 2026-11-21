using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJH19
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CatalogPower.Install<JunnaPowerJH19>(this, context, play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
