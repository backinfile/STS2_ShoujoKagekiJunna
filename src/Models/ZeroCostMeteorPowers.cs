using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaPowerJK400
{
    protected override string IconId => "JK67";

    protected override Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        // EnergyValue preserves effective cost even when native AutoPlay pays no energy.
        if (play.Card.EnergyCost.CostsX || play.Resources.EnergyValue != 0) return Task.CompletedTask;
        var arrows = ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player && !a.Resolving)
            .Select(a => a.Card).Distinct().ToArray();
        DamageModifiers.AddMany(arrows, grant.V("Bonus"));
        return Task.CompletedTask;
    }
}

public sealed partial class JunnaPowerJK400B
{
    protected override string IconId => "JZ05";

    protected override Task TurnEnding(Grant grant, PlayerChoiceContext context)
        => CardActions.Fly(context, Owner.Player!, grant.V("Damage"));
}
