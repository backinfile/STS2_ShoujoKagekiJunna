using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_047
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await PowerCmd.Apply<StrengthPower>(context, player.Creature, Value("Amount"), player.Creature, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
