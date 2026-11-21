using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK44B
{
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
