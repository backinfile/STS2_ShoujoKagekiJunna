using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_033
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        // Native double damage lasts for one turn per stack. Replaying this this
        // during the same turn leaves the current duration in place.
        if (player.Creature.GetPower<DoubleDamagePower>() == null)
            await PowerCmd.Apply<DoubleDamagePower>(context, player.Creature, 1, player.Creature, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
