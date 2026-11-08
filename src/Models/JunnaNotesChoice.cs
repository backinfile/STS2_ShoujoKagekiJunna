using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShoujoKagekiJunna.Models;

[Pool(typeof(JunnaCardPool))]
public abstract class JunnaNotesChoice() : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    public override string CustomPortraitPath => JunnaMod.ResourceRoot + "images/cards/" + CardCatalog.Get("JK137").Portrait;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Amount", 2)];
}
[CustomID("JUNNA_NOTES_DRAW")]
public sealed class JunnaNotesDraw : JunnaNotesChoice;
[CustomID("JUNNA_NOTES_ENERGY")]
public sealed class JunnaNotesEnergy : JunnaNotesChoice;

[CustomID("JUNNA_ENERGY_NOW")]
public sealed class JunnaEnergyNow : JunnaNotesChoice
{
    public override string CustomPortraitPath => JunnaMod.ResourceRoot + "images/cards/" + CardCatalog.Get("JH60").Portrait;
}
[CustomID("JUNNA_ENERGY_LATER")]
public sealed class JunnaEnergyLater : JunnaNotesChoice
{
    public override string CustomPortraitPath => JunnaMod.ResourceRoot + "images/cards/" + CardCatalog.Get("JH60").Portrait;
}
