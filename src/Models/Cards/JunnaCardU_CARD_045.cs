using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_045
{
    private int _lastPlayTurn = -1;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (_lastPlayTurn != Owner.PlayerCombatState!.TurnNumber)
        {
            _lastPlayTurn = Owner.PlayerCombatState.TurnNumber;
            await PlayerCmd.GainEnergy(Value("Energy"), Owner);
        }
        await Block(play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
