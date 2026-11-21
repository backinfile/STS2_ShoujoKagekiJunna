using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA82
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        int x = ResolveEnergyXValue();
        for (int i = 0; i < x; i++) await CardActions.Fly(context, player, Value("Damage"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
