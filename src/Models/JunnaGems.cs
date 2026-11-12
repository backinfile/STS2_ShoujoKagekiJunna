using BaseLib.Utils;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Models;

public abstract class JunnaGem(int healing, string icon) : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => JunnaMod.ResourceRoot + $"images/relics/{icon}.svg";
    protected override string PackedIconOutlinePath => JunnaMod.ResourceRoot + $"images/relics/{icon}_outline.svg";
    protected override string BigIconPath => PackedIconPath;

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck) return;
        Flash();
        // A deck transformation replaces one card and adds its replacement.
        await CreatureCmd.Heal(Owner.Creature, healing * (oldPileType == PileType.Deck ? 2 : 1));
    }

    public override async Task BeforeCardRemoved(CardModel card)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck) return;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, healing);
    }
}

[CustomID("JUNNA_GREEN_GEM")]
[Pool(typeof(JunnaRelicPool))]
public sealed class JunnaGreenGem() : JunnaGem(1, "green_gem")
{
    public override RelicModel GetUpgradeReplacement() => ModelDb.Relic<JunnaBurningGem>();
}

[CustomID("JUNNA_BURNING_GEM")]
[Pool(typeof(JunnaRelicPool))]
public sealed class JunnaBurningGem() : JunnaGem(2, "burning_gem");
