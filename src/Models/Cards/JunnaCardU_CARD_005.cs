using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_005
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        await Attack(context, play);
        for (int i = 0; i < Value("Cards"); i++)
        {
            using var single = KnowledgeCombat.SingleCard();
            var copy = CardCopies.ForCombat(this, player, CardCopyPurpose.TemporaryCopy);
            await GeneratedArrowPresentation.Play(copy,
                () => CardActions.FreePlay(context, copy, play.Target, keepTarget: true));
        }
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
