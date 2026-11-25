using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK122B
{
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;

    internal override Task OnPermanentShineDepleted(ShineDepletionContext context)
    {
        context.Player.PlayerCombatState?.RecalculateCardValues();
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
