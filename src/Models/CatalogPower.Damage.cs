using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    public override Task AfterEnergySpent(CardModel card, int amount)
        => card.Owner == Owner.Player && amount > 0 && DesignId == "JH30"
            ? EnergyDamage(new ThrowingPlayerChoiceContext(), amount) : Task.CompletedTask;

    public static async Task EnergyChanged(Player player, int amount)
    {
        if (amount <= 0) return;
        foreach (var power in player.Creature.Powers.OfType<CatalogPower>().Where(power => power.DesignId == "JH30").ToArray())
            await power.EnergyDamage(new ThrowingPlayerChoiceContext(), amount);
    }
    protected async Task EnergyDamage(PlayerChoiceContext context, int amount)
    {
        foreach (var grant in _grants.ToArray())
        {
            var targets = Owner.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
            if (targets.Length == 0) return;
            var target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(targets);
            using (DamageExecution.BeginCardResolution(grant.Card))
                await GameApi.Damage(context, [target!], amount, ValueProp.Unpowered, Owner, grant.Card, grant.Play);
        }
    }

}
