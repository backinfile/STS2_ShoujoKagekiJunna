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
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play) => ArrowCombat.AfterPlayed(context, play);
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
        return Task.CompletedTask;
    }
}
