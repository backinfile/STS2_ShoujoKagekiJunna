using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA145
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        await Attack(context, play);
        await CardActions.Generate(player, "JA119", Value("Cards"), PileType.Hand, CardPilePosition.Bottom, IsUpgraded);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
