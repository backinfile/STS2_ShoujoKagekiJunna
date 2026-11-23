using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Applies the current combat state to direct attacks from each batch of deployed arrows.</summary>
internal static class ArrowDamageForecast
{
    internal readonly record struct Batch(int Turns, int Loss);
    private readonly record struct Attack(decimal Damage, int Hits, ValueProp Props = ValueProp.Move, bool FixedHits = false);

    internal static Batch[] ForTarget(Creature target, IReadOnlyList<IGrouping<int, ArrowDeployment>> waiting)
    {
        if (!target.IsEnemy || !target.IsAlive || !target.IsHittable || target.MaxHp <= 0 ||
            target.HpDisplay.IsInfinite() || target.CombatState is not { } combat) return [];

        List<Batch> result = [];
        foreach (var batch in waiting)
            if (ForBatch(target, combat, batch) is { } loss) result.Add(new(batch.Key, loss));
        return result.ToArray();
    }

    private static int? ForBatch(Creature target, ICombatState combat, IEnumerable<ArrowDeployment> due)
    {
        int hp = target.CurrentHp;
        bool hasAttack = false;
        foreach (var arrow in due)
        {
            if (!arrow.Targets.Contains(target) || GetAttack(arrow, target) is not { Hits: > 0 } attack) continue;
            hasAttack = true;

            var card = arrow.Card;
            var dealer = card.Owner.Creature;
            var command = DamageCmd.Attack(attack.Damage).FromCard(card, arrow.Play).WithHitCount(attack.Hits);
            if (arrow.TargetsAllEnemies) command.TargetingAllOpponents(combat);
            else command.Targeting(target);

            int hits = attack.FixedHits ? attack.Hits : (int)Math.Ceiling(Hook.ModifyAttackHitCount(combat, command, attack.Hits));
            var props = attack.Props;
            var run = card.Owner.RunState;
            decimal damage = Hook.ModifyDamage(run, combat, target, dealer, attack.Damage, props, card,
#if STS2_MAIN || STS2_BETA
                arrow.Play,
#endif
                ModifyDamageHookType.All, CardPreviewMode.Normal, out _);
            for (int replay = 0; replay < arrow.PlayCount && hp > 0; replay++)
            for (int hit = 0; hit < hits && hp > 0; hit++)
                hp -= Math.Clamp((int)damage, 0, hp);
        }
        return hasAttack ? target.CurrentHp - hp : null;
    }

    private static Attack? GetAttack(ArrowDeployment arrow, Creature target)
    {
        switch (arrow.Card)
        {
            case JunnaFullDraw or JunnaStrike or JunnaGuidingStar or JunnaBreakEncirclement:
                return new(arrow.Card.DynamicVars.Damage.BaseValue, 1);
            case JunnaTurningSlash slash:
                return new(slash.DynamicVars.Damage.BaseValue, slash.DynamicVars.Repeat.IntValue);
            case CatalogCard { DesignId: "JK314" } spotlight:
                return new(spotlight.Value("Damage"), spotlight.HitsWithDepletedPartners);
            case JunnaCardU_CARD_131 ashes:
                int extraHits = ArrowCombat.SingleEnemyTargets(ashes.Owner).Contains(target) ? ashes.Value("ExtraHits") : 0;
                return new(ashes.Value("Damage"), 1 + extraHits, FixedHits: true);
            case CatalogCard card:
                var effect = CardRules.Get(card.DesignId).Effect;
                if (effect is SimpleCardEffect.Attack or SimpleCardEffect.AttackAndBlock or SimpleCardEffect.AttackAndDraw ||
                    card.DesignId is "JA67" or "JK32B" or "U_CARD_005" or "U_CARD_032")
                {
                    var (damage, hits) = CardEffects.PrintedAttack(card);
                    return new(damage, hits);
                }
                if (card.DesignId == "JT03")
                    return new(card.Value("Damage"), card.Value("Repeat", 2) + arrow.AttacksWhileWaiting);
                return null;
            default:
                if (HandArrow.OriginalType(arrow.Card) != CardType.Attack) return null;
                var variables = arrow.Card.DynamicVars.Select(pair => pair.Value).ToArray();
                int count = variables.OfType<RepeatVar>().FirstOrDefault()?.IntValue ?? 1;
                var calculated = variables.OfType<CalculatedDamageVar>().Take(2).ToArray();
                if (calculated.Length == 1 && calculated[0].Props.IsPoweredAttack())
                    return new(calculated[0].Calculate(target), count, calculated[0].Props);
                var direct = variables.OfType<DamageVar>().Where(value => value.Props.IsPoweredAttack()).Take(2).ToArray();
                return direct.Length == 1 ? new(direct[0].BaseValue, count, direct[0].Props) : null;
        }
    }
}
