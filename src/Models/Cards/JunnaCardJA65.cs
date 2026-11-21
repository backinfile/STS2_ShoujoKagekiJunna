using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA65
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        foreach (var selected in await ChooseCards(context, player.PlayerCombatState!.DrawPile.Cards.Where(CardCatalog.IsArrow)))
            await CardActions.FreePlay(context, selected);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
