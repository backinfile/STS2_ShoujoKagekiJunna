using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_012 : IArrowPlayFollower
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
    }

    Task IArrowPlayFollower.Follow(PlayerChoiceContext context, CardPlay play)
    {
        if (this == play.Card || HasBeenRemovedFromState || Pile == null || Pile.Type == PileType.Play
            || ArrowCombat.Find(this) != null) return Task.CompletedTask;
        var target = CardTargets.IsValid(this, play.Target) ? play.Target : null;
        HandArrow.Restore(this);
        return CardCmd.AutoPlay(context, this, target);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
