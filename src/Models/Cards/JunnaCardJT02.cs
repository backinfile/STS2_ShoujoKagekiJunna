using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJT02
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = Owner.PlayerCombatState!.Hand;
        foreach (var selected in await ChooseCards(context, hand.Cards))
            HandArrow.Adapt(selected);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
