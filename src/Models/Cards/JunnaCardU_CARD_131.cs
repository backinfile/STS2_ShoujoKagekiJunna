using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_131
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var marked = ArrowCombat.SingleEnemyTargets(Owner).ToHashSet();
        var combat = CombatState!;
        int damage = Value("Damage");
        await using var attack = await AttackCommand.CreateContextAsync(combat, context,
#if STS2_MAIN || STS2_BETA
            play
#else
            this
#endif
        );
        Task<IEnumerable<DamageResult>> Hit(IEnumerable<Creature> enemies) =>
            CreatureCmd.Damage(context, enemies, damage, ValueProp.Move, Owner.Creature, this
#if STS2_MAIN || STS2_BETA
                , play
#endif
            );

        attack.AddHit(await Hit(combat.HittableEnemies.ToArray()));
        for (int i = 0; i < Value("ExtraHits"); i++)
        {
            var targets = combat.HittableEnemies.Where(marked.Contains).ToArray();
            if (targets.Length == 0) break;
            attack.AddHit(await Hit(targets));
        }
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
