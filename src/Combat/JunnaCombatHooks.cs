using System.Runtime.CompilerServices;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

[CustomID("JUNNA_COMBAT_HOOKS")]
public sealed class JunnaCombatHooks : SingletonModel
{
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> MaxEnergyBonuses = new();
    public static void AddMaxEnergy(PlayerCombatState combat, int amount) => MaxEnergyBonuses.GetOrCreateValue(combat).Value += amount;
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
        => amount + (player.PlayerCombatState is { } combat && MaxEnergyBonuses.TryGetValue(combat, out var bonus) ? bonus.Value : 0);
    public override bool ShouldReceiveCombatHooks => true;
    public override Task BeforeCombatStart()
    {
        MaxEnergyBonuses.Clear();
        KnowledgeProgress.ResetCombat();
        ArrowCombat.Clear();
        DamageModifiers.ClearCombat();
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
        if (play.IsFirstInSeries && CardCatalog.IsArrow(play.Card))
            foreach (var arrow in ArrowCombat.Deployed.Where(a => a.Card.Owner == play.Card.Owner && a.Card != play.Card && !a.Resolving))
                arrow.AttacksWhileWaiting++;
        return Task.CompletedTask;
    }
    private static readonly HashSet<Player> FollowingPlayers = [];
    public override async Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        await ArrowCombat.AfterPlayed(context, play);
        if (!play.IsFirstInSeries || !CardCatalog.IsArrow(play.Card) || !FollowingPlayers.Add(play.Card.Owner)) return;
        try
        {
            var followers = CardActions.Named(play.Card.Owner, "U_CARD_012").Where(card => card != play.Card).ToArray();
            foreach (var follower in followers)
            {
                if (follower.HasBeenRemovedFromState || follower.Pile == null || follower.Pile.Type == PileType.Play
                    || ArrowCombat.Find(follower)?.Resolving == true) continue;
                var target = CardTargets.IsValid(follower, play.Target) ? play.Target : null;
                ArrowCombat.Cancel(follower);
                // Native AutoPlay chooses a target when absent and handles blocked plays' destination.
                await MegaCrit.Sts2.Core.Commands.CardCmd.AutoPlay(context, follower, target);
            }
        }
        finally { FollowingPlayers.Remove(play.Card.Owner); }
    }
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        bool free = CardCatalog.DesignId(card) == "JK45B" && ArrowCombat.Deployed.Any(a => a.Card.Owner == card.Owner && CardCatalog.DesignId(a.Card) == "JK45");
        modifiedCost = free ? 0 : originalCost;
        return free;
    }
    public override Task AfterCombatVictory(CombatRoom room) => KnowledgeProgress.WonCombat();
    public override Task AfterCombatEnd(CombatRoom room)
    {
        MaxEnergyBonuses.Clear();
        ArrowCombat.Clear();
        DamageModifiers.ClearCombat();
        foreach (var player in MegaCrit.Sts2.Core.Runs.RunManager.Instance.DebugOnlyGetState()?.Players ?? []) DynamicKnowledge.Normalize(player);
        return Task.CompletedTask;
    }
}
