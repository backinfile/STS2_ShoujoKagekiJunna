using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_013
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await PowerCmd.Apply<JunnaCagedPower>(context, player.Creature, 1, player.Creature, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
