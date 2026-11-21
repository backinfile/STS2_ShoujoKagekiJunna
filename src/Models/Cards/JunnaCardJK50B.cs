using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK50B
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = Owner.PlayerCombatState!.Hand;
        var eloquence = hand.Cards.Where(c => CardCatalog.DesignId(c) == "JK50").ToArray();
        foreach (var selected in eloquence)
            await CardActions.FreePlay(context, selected);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
