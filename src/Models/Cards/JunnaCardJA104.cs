using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA104
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await Block(play);
        await CardActions.Generate(player, "JA119", Value("Cards"), PileType.Hand, CardPilePosition.Bottom);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
