using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_045
{
    private int _lastPlayTurn = -1;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card != this || !play.IsFirstInSeries ||
            Owner.PlayerCombatState is not { } combat || _lastPlayTurn == combat.TurnNumber)
            return Task.CompletedTask;

        _lastPlayTurn = combat.TurnNumber;
        return PlayerCmd.GainEnergy(Value("Energy"), Owner);
    }

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Block(play);

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _lastPlayTurn = -1;
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
