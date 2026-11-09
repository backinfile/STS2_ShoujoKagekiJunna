using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_NEXT_CARD_BONUS")]
public sealed class JunnaNextCardBonus : CustomPowerModel
{
    internal static decimal PreviewBonus(CardModel card) => card.IsMutable &&
        card.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust &&
        DamageModifiers.CanDealDamage(card) ? card.Owner.Creature.GetPower<JunnaNextCardBonus>()?.Amount ?? 0 : 0;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + "images/powers/NEXT_CARD_BONUS.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || !play.IsFirstInSeries) return;
        DamageModifiers.Add(play.Card, Amount);
        await PowerCmd.Remove(this);
    }
}
