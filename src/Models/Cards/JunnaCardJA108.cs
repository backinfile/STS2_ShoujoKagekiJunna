using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA108
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        await CardActions.PlayDrawPileArrows(context, player);
    }

    protected override void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("ArrowCount", IsMutable && Owner?.PlayerCombatState is { } combat
            ? combat.DrawPile.Cards.Count(CardCatalog.IsArrow) : 0);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
