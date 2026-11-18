using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using ShoujoKagekiJunna.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_CAGED_POWER")]
public sealed class JunnaCagedPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + "images/powers/JA205.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource
#if STS2_MAIN || STS2_BETA
        , CardPlay? cardPlay
#endif
        )
    {
        // ColossusPower checks the current attack and attacker at damage resolution.
        if (target != Owner || !props.IsPoweredAttack() || dealer == null) return 1m;
        return ArrowCombat.Deployed.Any(arrow => arrow.Card.Owner == Owner.Player && arrow.Targets.Contains(dealer)) ? 0.5m : 1m;
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
        => side == CombatSide.Enemy ? PowerCmd.Remove(this) : Task.CompletedTask;
}
