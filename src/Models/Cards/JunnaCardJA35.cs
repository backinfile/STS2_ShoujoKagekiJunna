using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA35
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CatalogPower.Install<JunnaPowerJA35>(this, context, play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
