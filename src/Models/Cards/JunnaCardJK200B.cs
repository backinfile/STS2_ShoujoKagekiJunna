using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK200B
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
