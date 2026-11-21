using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK272B
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var player = Owner;
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies.Where(e => e.IsAlive).ToArray())
            await PowerCmd.Apply<VulnerablePower>(context, enemy, Value("Amount"), player.Creature, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
