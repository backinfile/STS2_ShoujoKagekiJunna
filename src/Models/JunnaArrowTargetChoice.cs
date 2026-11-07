using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace ShoujoKagekiJunna.Models;

[Pool(typeof(JunnaCardPool))]
public abstract class JunnaArrowTargetChoice(bool self) : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    public override string CustomPortraitPath => JunnaMod.ResourceRoot + "images/cards/" + CardCatalog.Get(self ? "JH27" : "JA38").Portrait;
}
[CustomID("JUNNA_ARROW_TARGET_SELF")]
public sealed class JunnaArrowTargetSelf() : JunnaArrowTargetChoice(true);
[CustomID("JUNNA_ARROW_TARGET_ENEMY")]
public sealed class JunnaArrowTargetEnemy() : JunnaArrowTargetChoice(false);
