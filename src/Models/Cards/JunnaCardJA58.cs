using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA58
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        var debuffTargets = Owner.Creature.CombatState!.HittableEnemies.Where(enemy => enemy.IsAlive).ToArray();
        foreach (var enemy in debuffTargets)
        {
            await PowerCmd.Apply<VulnerablePower>(context, enemy, Value("Debuff"), player.Creature, this);
            await PowerCmd.Apply<WeakPower>(context, enemy, Value("Debuff"), player.Creature, this);
        }
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
