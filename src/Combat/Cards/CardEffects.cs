using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal static class CardEffects
{
    public static void SetNamedFree(MegaCrit.Sts2.Core.Entities.Players.Player player, params string[] ids)
    {
        foreach (var c in CardActions.All(player).Where(c => ids.Contains(CardCatalog.DesignId(c))))
        { c.EnergyCost.SetThisCombat(0); c.InvokeEnergyCostChanged(); }
    }

    public static Task<DamageResult[]> DealDamage(CatalogCard card, PlayerChoiceContext context, CardPlay play, int damage, int hits)
        => DealDamage(card, context, play, DamageCmd.Attack(damage), hits);

    internal static async Task<DamageResult[]> DealDamage(CatalogCard card, PlayerChoiceContext context, CardPlay play,
        AttackCommand attack, int hits)
    {
        if (hits <= 0 || (card.TargetType == TargetType.AnyEnemy && play.Target?.IsAlive != true)) return [];
        attack.FromCard(card, play).WithHitCount(hits).WithHitFx(VfxCmd.slashPath);
        if (card.TargetType == TargetType.AllEnemies) attack.TargetingAllOpponents(card.Owner.Creature.CombatState!);
        else attack.Targeting(play.Target!);
        await attack.Execute(context);
        return attack.Results.SelectMany(r => r).ToArray();
    }
}
