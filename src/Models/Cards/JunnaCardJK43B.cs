using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK43B
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await CardActions.DrawMatching(context, player, c => CardCatalog.DesignId(c) == "JK43", 1);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
