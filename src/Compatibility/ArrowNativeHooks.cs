using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna;

internal static class ArrowNativeHooks
{
    internal static async Task Resolve(PlayerChoiceContext context, CardPlay play, Func<Task> effect)
    {
        var player = play.Card.Owner;
        var manager = CombatManager.Instance;
        var combat = player.Creature.CombatState!;
#if STS2_MAIN || STS2_BETA
        var combatId = manager.BeginCardOrPotionEffect(player);
#else
        manager.BeginCardOrPotionEffect(player);
#endif
        try
        {
            var rupture = player.Creature.GetPower<RupturePower>();
            if (rupture != null) await rupture.BeforeCardPlayed(play);
            await effect();
            if (manager.DebugOnlyGetState() != combat || !manager.IsInProgress) return;
            if (rupture != null && player.Creature.GetPower<RupturePower>() == rupture)
                await rupture.AfterCardPlayed(context, play);
            foreach (var power in combat.Creatures.SelectMany(creature => creature.Powers).OfType<CurlUpPower>().ToArray())
                await power.AfterCardPlayed(context, play);
        }
        finally
        {
#if STS2_MAIN || STS2_BETA
            await manager.EndCardOrPotionEffect(combatId, player);
#else
            if (manager.DebugOnlyGetState() == combat) manager.EndCardOrPotionEffect(player);
#endif
        }
    }
}
