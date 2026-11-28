using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_186
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        for (int i = 0; i < Value("Cards"); i++)
            await CardActions.Fly(context, Owner, Value("Damage"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
