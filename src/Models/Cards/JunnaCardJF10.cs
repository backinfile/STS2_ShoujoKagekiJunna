using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJF10
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        var debuffTargets = play.Target?.IsAlive == true ? new[] { play.Target } : [];
        foreach (var enemy in debuffTargets)
        {
            await PowerCmd.Apply<VulnerablePower>(context, enemy, Value("Debuff"), player.Creature, this);
            await PowerCmd.Apply<WeakPower>(context, enemy, Value("Debuff"), player.Creature, this);
        }
            if (player.Creature.GetPower<VulnerablePower>() is { } vulnerable) await PowerCmd.Remove(vulnerable);
            if (player.Creature.GetPower<WeakPower>() is { } weak) await PowerCmd.Remove(weak);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
