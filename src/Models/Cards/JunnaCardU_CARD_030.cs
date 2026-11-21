using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_030
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        int priorArrows = JunnaCombatHooks.CountArrowPlays(player);
        for (int i = 0; i < priorArrows && !CombatManager.Instance.IsOverOrEnding; i++)
            await CardActions.Fly(context, player, Value("Damage"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
