using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_031
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        if (ArrowCombat.Find(this) is { ExhaustsWhenResolved: false })
            await CardActions.FreePlay(context, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
