using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA53
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await Block(play);
        await CardActions.DrawMatching(context, player, c => CardActions.HasNumericCost(c) && CardActions.Cost(c) == 0, Value("Cards"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
