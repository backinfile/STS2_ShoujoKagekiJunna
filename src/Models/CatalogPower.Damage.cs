using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (DesignId != "JZ05" || dealer != Owner || target.Side == Owner.Side || result.TotalDamage <= 0 || !props.IsPoweredAttack()) return;
        int ordinal = AttackEventHistory.Ordinal(result);
        foreach (var grant in _grants.ToArray())
            if (ordinal > 0 && ordinal <= grant.V("Limit"))
                await CardActions.Fly(context, Owner.Player!, result.TotalDamage, target, grant.Card.IsUpgraded);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (DesignId != "JA96" || target != Owner || result.UnblockedDamage <= 0 || !props.IsPoweredAttack()) return;
        foreach (var grant in _grants.ToArray()) await ArrowCombat.AccelerateEnemies(context, Owner.Player!, grant.V("Turns"));
    }

    public override Task AfterEnergySpent(CardModel card, int amount) => card.Owner == Owner.Player && amount > 0 && DesignId == "JH30"
        ? EnergyDamage(new ThrowingPlayerChoiceContext(), amount) : Task.CompletedTask;

    public static async Task EnergyChanged(Player player, int amount)
    {
        if (amount <= 0) return;
        foreach (var power in player.Creature.Powers.OfType<CatalogPower>().Where(p => p.DesignId == "JH30").ToArray())
            await power.EnergyDamage(new ThrowingPlayerChoiceContext(), amount);
    }
    private async Task EnergyDamage(PlayerChoiceContext context, int amount)
    {
        foreach (var grant in _grants.ToArray())
        {
            var targets = Owner.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
            if (targets.Length == 0) return;
            var target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(targets);
            using (DamageExecution.BeginCardResolution(grant.Card))
                await GameApi.Damage(context, [target!], amount, ValueProp.Move, Owner, grant.Card, grant.Play);
        }
    }
}
