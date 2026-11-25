using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA145
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
        var rng = Owner.RunState.Rng.CombatCardSelection;
        for (int i = 0; i < Value("Accelerations"); i++)
        {
            var arrows = ArrowCombat.WaitingFor(Owner).ToArray();
            if (arrows.Length == 0) break;
            await ArrowCombat.Accelerate(context, rng.NextItem(arrows)!, Value("Turns"));
        }
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
