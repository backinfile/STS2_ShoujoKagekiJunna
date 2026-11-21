using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJH24
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await Block(play); JunnaCombatHooks.AddMaxEnergy(player.PlayerCombatState!, Value("Count"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
