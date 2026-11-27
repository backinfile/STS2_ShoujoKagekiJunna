using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_178
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Attack(context, CardActions.ResolveTarget(play));
        if (CombatManager.Instance.IsOverOrEnding) return;
        var discard = Owner.PlayerCombatState!.DiscardPile;
        var selected = await ChooseCards(context, discard.Cards, Value("Cards"));
        foreach (var card in selected)
            if (card.Pile == discard)
                await HandArrow.Play(context, card, Value("ArrowTurns"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
