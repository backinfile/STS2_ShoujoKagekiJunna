using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA43
{
    internal override ArrowAcceleration Acceleration => new(ArrowAccelerationTargets.All, Value("Turns"));

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        await ArrowCombat.AccelerateFromCard(context, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
