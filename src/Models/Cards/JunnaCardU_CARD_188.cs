using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_188
{
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
        => CatalogPower.Install<JunnaPowerU_CARD_188>(this, context, play);

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
