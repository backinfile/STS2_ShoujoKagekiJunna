using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA88
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
        if (!CombatManager.Instance.IsOverOrEnding) await HandArrow.Select(context, this, CardPile.MaxCardsInHand, optional: true);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
