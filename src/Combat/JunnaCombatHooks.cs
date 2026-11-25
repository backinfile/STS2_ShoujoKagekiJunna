using System.Runtime.CompilerServices;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal interface IArrowPlayFollower
{
    Task Follow(PlayerChoiceContext context, CardPlay play);
}

[CustomID("JUNNA_COMBAT_HOOKS")]
#if STS2_MAIN || STS2_BETA
public sealed class JunnaCombatHooks : AbstractModel
#else
public sealed class JunnaCombatHooks : SingletonModel
#endif
{

    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> ArrowPlays = new();
    public static int CountArrowPlays(Player player) => player.PlayerCombatState is { } state && ArrowPlays.TryGetValue(state, out var count) ? count.Value : 0;
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> MaxEnergyBonuses = new();
    public static void AddMaxEnergy(PlayerCombatState combat, int amount) => MaxEnergyBonuses.GetOrCreateValue(combat).Value += amount;
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
        => amount + (player.PlayerCombatState is { } combat && MaxEnergyBonuses.TryGetValue(combat, out var bonus) ? bonus.Value : 0);
    public override bool ShouldReceiveCombatHooks => true;
    public override Task BeforeCombatStart()
    {
        ArrowPlays.Clear();
        MaxEnergyBonuses.Clear();
        KnowledgeProgress.ResetCombat();
        ArrowCombat.Clear();
        foreach (var player in MegaCrit.Sts2.Core.Runs.RunManager.Instance.DebugOnlyGetState()?.Players ?? []) DynamicKnowledge.Normalize(player);
        return Task.CompletedTask;
    }
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
        => KnowledgeProgress.OpenCombat(player, choiceContext);
    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext context, Player player) => ArrowCombat.Advance(context, player);
    public override Task BeforeDeath(Creature creature)
    {
        ArrowCombat.RememberDeathOrder(creature);
        return Task.CompletedTask;
    }
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (!play.IsFirstInSeries) return Task.CompletedTask;
        if (CardCatalog.IsArrow(play.Card))
            ArrowPlays.GetOrCreateValue(play.Card.Owner.PlayerCombatState!).Value++;
        return Task.CompletedTask;
    }
    private static readonly HashSet<Player> FollowingPlayers = [];
    public override async Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        bool wasArrow = CardCatalog.IsArrow(play.Card);
        await ArrowCombat.AfterPlayed(context, play);
        if (!play.IsFirstInSeries || !wasArrow || !FollowingPlayers.Add(play.Card.Owner)) return;
        try
        {
            foreach (var follower in CardActions.Available(play.Card.Owner).OfType<IArrowPlayFollower>().ToArray())
                await follower.Follow(context, play);
        }
        finally { FollowingPlayers.Remove(play.Card.Owner); }
    }
    public override Task AfterCombatVictory(CombatRoom room) => KnowledgeProgress.WonCombat();
    public override Task AfterCombatEnd(CombatRoom room)
    {
        DynamicKnowledge.ClearCombatGroups();
        ArrowPlays.Clear();
        MaxEnergyBonuses.Clear();
        ArrowCombat.Clear();
        foreach (var player in MegaCrit.Sts2.Core.Runs.RunManager.Instance.DebugOnlyGetState()?.Players ?? []) DynamicKnowledge.Normalize(player);
        return Task.CompletedTask;
    }
}
