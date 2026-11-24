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
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

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
        {
            ArrowPlays.GetOrCreateValue(play.Card.Owner.PlayerCombatState!).Value++;
            foreach (var arrow in ArrowCombat.Deployed.Where(a => a.Card.Owner == play.Card.Owner && a.Card != play.Card && !a.Resolving))
                arrow.AttacksWhileWaiting++;
        }
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
            var followers = CardActions.Named(play.Card.Owner, "U_CARD_012").Where(card => card != play.Card).ToArray();
            foreach (var follower in followers)
            {
                if (follower.HasBeenRemovedFromState || follower.Pile == null || follower.Pile.Type == PileType.Play
                    || ArrowCombat.Find(follower) != null) continue;
                var target = CardTargets.IsValid(follower, play.Target) ? play.Target : null;
                HandArrow.Restore(follower);
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
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource
#if STS2_MAIN || STS2_BETA
        , CardPlay? cardPlay
#endif
        )
    {
        if (target == null || !props.IsPoweredAttack() ||
            cardSource is not JunnaCardU_CARD_130 { IsMutable: true } card)
            return 0m;
        return card.Value("PerArrow") * ArrowCombat.SingleEnemyTargets(card.Owner).Count(enemy => enemy == target);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource
#if STS2_MAIN || STS2_BETA
        , CardPlay? cardPlay
#endif
        )
    {
        if (target == null || !props.IsPoweredAttack() ||
            cardSource is not CatalogCard { DesignId: "U_CARD_051", IsMutable: true } card)
            return 1m;
        return ArrowCombat.Deployed.Any(arrow => arrow.Card.Owner == card.Owner && arrow.Targets.Contains(target))
            ? 2m : 1m;
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
