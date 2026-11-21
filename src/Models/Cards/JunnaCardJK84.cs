using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK84
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await HandArrow.Select(context, this, Value("Cards"), optional: true);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
