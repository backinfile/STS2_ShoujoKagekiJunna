using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_130
{
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
        => Attack(context, CardActions.ResolveTarget(play));

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource
#if STS2_MAIN || STS2_BETA
        , CardPlay? cardPlay
#endif
        )
    {
        if (!props.IsPoweredAttack() || cardSource != this)
            return 0m;
        return Value("PerArrow") * ArrowCombat.WaitingFor(Owner).Count(arrow => arrow.Card != this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
