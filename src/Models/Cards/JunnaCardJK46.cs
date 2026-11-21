using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK46
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CatalogPower.Install<JunnaPowerJK46>(this, context, play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
