using BaseLib.Utils;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Models;

public abstract class JunnaGem(int healing, string icon) : CustomRelicModel, IShineDepletionListener
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => JunnaMod.ResourceRoot + $"images/relics/{icon}.svg";
    protected override string PackedIconOutlinePath => JunnaMod.ResourceRoot + $"images/relics/{icon}_outline.svg";
    protected override string BigIconPath => PackedIconPath;

    protected virtual Task DeckChanged(int changes) => CreatureCmd.Heal(Owner.Creature, healing * changes);

    public Task OnShineDepleted(ShineDepletionContext context)
    {
        if (context.Player != Owner || context.PermanentCard == null) return Task.CompletedTask;
        Flash();
        return DeckChanged(1);
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck) return;
        Flash();
        // A deck transformation replaces one card and adds its replacement.
        await DeckChanged(oldPileType == PileType.Deck ? 2 : 1);
    }

    public override async Task BeforeCardRemoved(CardModel card)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck) return;
        Flash();
        await DeckChanged(1);
    }
}

[CustomID("JUNNA_GREEN_GEM")]
[Pool(typeof(JunnaRelicPool))]
public sealed class JunnaGreenGem() : JunnaGem(2, "green_gem")
{
    public override RelicModel GetUpgradeReplacement() => ModelDb.Relic<JunnaBurningGem>();
}

[CustomID("JUNNA_BURNING_GEM")]
[Pool(typeof(JunnaRelicPool))]
public sealed class JunnaBurningGem() : JunnaGem(0, "burning_gem")
{
    protected override Task DeckChanged(int changes) => CreatureCmd.GainMaxHp(Owner.Creature, changes * 2);
}
