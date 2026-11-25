using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK84B
{
    internal override ArrowAcceleration Acceleration => new(ArrowAccelerationTargets.Enemies, Value("Turns"));

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
        await ArrowCombat.AccelerateFromCard(context, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
