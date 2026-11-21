using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJH01
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = Owner.PlayerCombatState!.Hand;
        if (hand.Cards.Count < 2) return;
        var swapped = await ChooseCards(context, hand.Cards, 2);
        if (swapped.Length == 2)
        {
            int first = CardActions.Cost(swapped[0]), second = CardActions.Cost(swapped[1]);
            CostChanges.SetFixed(swapped[0], second); CostChanges.SetFixed(swapped[1], first);
        }
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
