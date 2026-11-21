using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_036
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        if (play.Target is { IsAlive: true } debuffTarget)
        {
            await PowerCmd.Apply<VulnerablePower>(context, debuffTarget, Value("Vulnerable"), player.Creature, this);
            await PowerCmd.Apply<WeakPower>(context, debuffTarget, Value("Weak"), player.Creature, this);
        }
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
