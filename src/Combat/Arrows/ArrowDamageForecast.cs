using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Applies the current combat state to direct attacks from each batch of deployed arrows.</summary>
internal static class ArrowDamageForecast
{
    internal readonly record struct Batch(int Turns, int Loss);
    private readonly record struct Attack(decimal Damage, ValueProp Props, bool FromOsty = false);

    internal static Batch[] ForTarget(Creature target, IReadOnlyList<IGrouping<int, ArrowDeployment>> waiting)
    {
        if (!target.IsEnemy || !target.IsAlive || !target.IsHittable || target.MaxHp <= 0 ||
            target.HpDisplay.IsInfinite() || target.CombatState is not { } combat) return [];

        List<Batch> result = [];
        int hp = target.CurrentHp;
        foreach (var batch in waiting)
        {
            if (ForBatch(target, combat, batch, hp) is not { } loss) continue;
            result.Add(new(batch.Key, loss));
            hp -= loss;
            if (hp == 0) break;
        }
        return result.ToArray();
    }

    private static int? ForBatch(Creature target, ICombatState combat, IEnumerable<ArrowDeployment> due, int hp)
    {
        int remainingHp = hp;
        bool hasAttack = false;
        foreach (var arrow in due)
        {
            if (remainingHp == 0) break;
            if (!arrow.Targets.Contains(target) || GetAttack(arrow, target) is not { } attack) continue;

            var card = arrow.Card;
            var dealer = attack.FromOsty ? card.Owner.Osty : card.Owner.Creature;
            if (dealer is not { IsAlive: true }) continue;
            var hitCount = AttackHitCount.Read(card, target);
            if (hitCount <= 0) continue;
            hasAttack = true;

            var command = DamageCmd.Attack(attack.Damage).WithHitCount(hitCount);
            if (attack.FromOsty) command.FromOsty(dealer, card, arrow.Play);
            else command.FromCard(card, arrow.Play);
            if (arrow.TargetsAllEnemies) command.TargetingAllOpponents(combat);
            else command.Targeting(target);

            int hits = (int)Math.Ceiling(Hook.ModifyAttackHitCount(combat, command, hitCount));
            var props = attack.Props;
            var run = card.Owner.RunState;
            decimal damage = Hook.ModifyDamage(run, combat, target, dealer, attack.Damage, props, card,
#if STS2_MAIN || STS2_BETA
                arrow.Play,
#endif
                ModifyDamageHookType.All, CardPreviewMode.Normal, out _);
            for (int replay = 0; replay < arrow.PlayCount && remainingHp > 0; replay++)
            for (int hit = 0; hit < hits && remainingHp > 0; hit++)
                remainingHp -= Math.Clamp((int)damage, 0, remainingHp);
        }
        return hasAttack ? hp - remainingHp : null;
    }

    private static Attack? GetAttack(ArrowDeployment arrow, Creature target)
    {
        var variables = arrow.Card.DynamicVars;
        var calculated = variables.Select(pair => pair.Value).OfType<CalculatedDamageVar>().Take(2).ToArray();
        if (calculated.Length > 0)
        {
            if (calculated is not [var damage] || !damage.Props.IsPoweredAttack()) return null;
            return new(damage.Calculate(arrow.TargetsAllEnemies ? null : target), damage.Props, damage.IsFromOsty);
        }

        Attack? attack = null;
        foreach (var (_, variable) in variables)
        {
            Attack? direct = variable switch
            {
                DamageVar damage when damage.Props.IsPoweredAttack() => new(damage.BaseValue, damage.Props),
                OstyDamageVar damage when damage.Props.IsPoweredAttack() => new(damage.BaseValue, damage.Props, FromOsty: true),
                _ => null
            };
            if (direct == null) continue;
            if (attack != null) return null;
            attack = direct;
        }
        return attack;
    }
}
