using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK45B
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await ArrowCombat.AccelerateAll(context, player, Value("Turns"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
