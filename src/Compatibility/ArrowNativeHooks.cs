using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna;

internal static class ArrowNativeHooks
{
    internal static async Task Complete(PlayerChoiceContext context, CardPlay play)
    {
        var combat = play.Card.Owner.Creature.CombatState;
        if (combat == null || CombatManager.Instance.IsOverOrEnding) return;
        // Curl Up waits for the damaging card to finish. A deferred arrow has already
        // finished its native play, so complete this power without replaying card hooks.
        foreach (var power in combat.Creatures.SelectMany(creature => creature.Powers).OfType<CurlUpPower>().ToArray())
            await power.AfterCardPlayed(context, play);
    }
}
