using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJH20
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await GainRetainedEnergy(context, Value("Energy"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
