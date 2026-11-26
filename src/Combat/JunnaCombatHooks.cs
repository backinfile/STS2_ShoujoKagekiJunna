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
    private static readonly ConditionalWeakTable<PlayerCombatState, HashSet<int>> PlayedEnergyCosts = new();
    private static readonly ConditionalWeakTable<CardPlay, ArrowDeployment[]> ZeroCostFollowers = new();

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not JunnaCardU_CARD_184 attack || card.Owner?.PlayerCombatState is not { } combat ||
            !PlayedEnergyCosts.TryGetValue(combat, out var costs)) return false;
        modifiedCost = Math.Max(0, originalCost - costs.Count * attack.Value("Discount"));
        return true;
    }
    public static void AddMaxEnergy(PlayerCombatState combat, int amount) => MaxEnergyBonuses.GetOrCreateValue(combat).Value += amount;
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
        => amount + (player.PlayerCombatState is { } combat && MaxEnergyBonuses.TryGetValue(combat, out var bonus) ? bonus.Value : 0);
    public override bool ShouldReceiveCombatHooks => true;
    public override Task BeforeCombatStart()
    {
        ArrowPlays.Clear();
        MaxEnergyBonuses.Clear();
        PlayedEnergyCosts.Clear();
        ZeroCostFollowers.Clear();
        KnowledgeProgress.ResetCombat();
        ArrowCombat.Clear();
        foreach (var player in MegaCrit.Sts2.Core.Runs.RunManager.Instance.DebugOnlyGetState()?.Players ?? []) DynamicKnowledge.Normalize(player);
        return Task.CompletedTask;
    }
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
        => KnowledgeProgress.OpenCombat(player, choiceContext);
    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext context, Player player)
    {
        JunnaIdleGesture.Attach(player);
        return ArrowCombat.Advance(context, player);
    }
    public override Task BeforeDeath(Creature creature)
    {
        ArrowCombat.RememberDeathOrder(creature);
        return Task.CompletedTask;
    }
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (!play.IsFirstInSeries) return Task.CompletedTask;
        JunnaIdleGesture.ResumeFromAction(play.Card.Owner);
        var combat = play.Card.Owner.PlayerCombatState!;
        int cost = play.Card.EnergyCost.GetResolved();
        if (!play.Card.EnergyCost.CostsX && cost == 0)
        {
            var followers = ArrowCombat.WaitingFor(play.Card.Owner)
                .Where(arrow => arrow.Card is JunnaCardU_CARD_181 && arrow.Card != play.Card).ToArray();
            if (followers.Length > 0) ZeroCostFollowers.Add(play, followers);
        }
        if (PlayedEnergyCosts.GetOrCreateValue(combat).Add(cost))
            foreach (var attack in combat.AllCards.OfType<JunnaCardU_CARD_184>())
                attack.InvokeEnergyCostChanged();
        if (CardCatalog.IsArrow(play.Card)) ArrowPlays.GetOrCreateValue(combat).Value++;
        return Task.CompletedTask;
    }
    public override Task BeforePotionUsed(PotionModel potion, Creature? target)
    {
        JunnaIdleGesture.ResumeFromAction(potion.Owner);
        return Task.CompletedTask;
    }
    private static readonly HashSet<Player> FollowingPlayers = [];
    public override async Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        bool wasArrow = CardCatalog.IsArrow(play.Card);
        await ArrowCombat.AfterPlayed(context, play);
        if (ZeroCostFollowers.TryGetValue(play, out var followers))
        {
            ZeroCostFollowers.Remove(play);
            foreach (var arrow in followers)
                await ArrowCombat.Accelerate(context, arrow, ((JunnaCardU_CARD_181)arrow.Card).Value("Acceleration"));
        }
        if (!play.IsFirstInSeries || !wasArrow || !FollowingPlayers.Add(play.Card.Owner)) return;
        try
        {
            foreach (var follower in CardActions.Available(play.Card.Owner).OfType<IArrowPlayFollower>().ToArray())
                await follower.Follow(context, play);
        }
        finally { FollowingPlayers.Remove(play.Card.Owner); }
    }
    public override Task AfterCombatVictory(CombatRoom room)
    {
        foreach (var player in room.CombatState.Players)
            if (player.Character is Junna) ArrowAttackAnimation.Relax(player);
        return KnowledgeProgress.WonCombat();
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        DynamicKnowledge.ClearCombatGroups();
        ArrowPlays.Clear();
        MaxEnergyBonuses.Clear();
        PlayedEnergyCosts.Clear();
        ZeroCostFollowers.Clear();
        ArrowCombat.Clear();
        foreach (var player in MegaCrit.Sts2.Core.Runs.RunManager.Instance.DebugOnlyGetState()?.Players ?? []) DynamicKnowledge.Normalize(player);
        return Task.CompletedTask;
    }
}
