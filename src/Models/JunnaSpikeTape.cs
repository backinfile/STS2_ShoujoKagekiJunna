using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_YOUR_KNOWLEDGE_SHINES_FOREVER")]
[Pool(typeof(JunnaRelicPool))]
public sealed class JunnaSpikeTape : CustomRelicModel
{
    private const string EnergyThreshold = "EnergyThreshold";
    private const string IconBasePath = JunnaMod.ResourceRoot + "images/relics/spike_tape";

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override string PackedIconPath => IconBasePath + ".png";
    protected override string PackedIconOutlinePath => IconBasePath + "_outline.png";
    protected override string BigIconPath => IconBasePath + "_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(EnergyThreshold, 2), new CardsVar(1)];

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.Owner != Owner || !play.IsFirstInSeries
            || play.Resources.EnergyValue < DynamicVars[EnergyThreshold].IntValue) return;

        Flash();
        await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
    }
}
