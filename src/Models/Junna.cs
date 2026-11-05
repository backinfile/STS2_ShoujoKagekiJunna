using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA")]
public sealed class Junna : PlaceholderCharacterModel
{
    public override string PlaceholderID => "silent";
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 70;
    public override Color NameColor => new("74BDEB");
    public override Color EnergyLabelOutlineColor => new("234B75");
    public override Color DialogueColor => new("74BDEB");
    public override Color MapDrawingColor => new("74BDEB");
    public override Color RemoteTargetingLineColor => new("74BDEB");
    public override Color RemoteTargetingLineOutline => new("234B75");
    public override CardPoolModel CardPool => ModelDb.CardPool<JunnaCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<JunnaRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SilentPotionPool>();
    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<JunnaStrike>(), ModelDb.Card<JunnaStrike>(), ModelDb.Card<JunnaStrike>(),
        ModelDb.Card<JunnaStrike>(), ModelDb.Card<JunnaStrike>(),
        ModelDb.Card<JunnaDefend>(), ModelDb.Card<JunnaDefend>(), ModelDb.Card<JunnaDefend>(), ModelDb.Card<JunnaDefend>(),
        ModelDb.Card<JunnaFullDraw>()
    ];
    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<JunnaGreenGem>()];
    public override string CustomVisualPath => JunnaMod.ResourceRoot + "scenes/junna_combat.tscn";
    public override string CustomCharacterSelectBg => JunnaMod.ResourceRoot + "scenes/junna_select.tscn";
    public override string CustomCharacterSelectIconPath => JunnaMod.ResourceRoot + "images/character_select_icon.png";
    public override string CustomCharacterSelectLockedIconPath => CustomCharacterSelectIconPath;
    public override string CustomIconTexturePath => JunnaMod.ResourceRoot + "images/portrait_icon.tres";
    public override string CustomIconOutlineTexturePath => JunnaMod.ResourceRoot + "images/portrait_icon_outline.png";
    public override string CustomMapMarkerPath => JunnaMod.ResourceRoot + "images/map_marker.png";
    public override string CustomIconPath => JunnaMod.ResourceRoot + "scenes/junna_icon.tscn";
    public override string CustomRestSiteAnimPath => JunnaMod.ResourceRoot + "scenes/junna_rest.tscn";
    public override string CustomMerchantAnimPath => JunnaMod.ResourceRoot + "scenes/junna_merchant.tscn";
    public override string CustomEnergyCounterPath => JunnaMod.ResourceRoot + "scenes/junna_energy.tscn";
}

[CustomID("JUNNA_CARD_POOL")]
public sealed class JunnaCardPool : CustomCardPoolModel
{
    public override string Title => "junna";
    public override bool IsColorless => false;
    public override Color ShaderColor => new("65B3E8");
    public override Color DeckEntryCardColor => new("65B3E8");
    public override Color EnergyOutlineColor => new("234B75");
    public override string BigEnergyIconPath => JunnaMod.ResourceRoot + "images/energy.png";
    public override string TextEnergyIconPath => "res://images/packed/sprite_fonts/defect_energy_icon.png";
    protected override IEnumerable<CardModel> FilterThroughEpochs(MegaCrit.Sts2.Core.Unlocks.UnlockState unlockState, IEnumerable<CardModel> cards)
        => base.FilterThroughEpochs(unlockState, cards).Where(c => c is not JunnaCard junna ||
            (CardCatalog.Get(junna.DesignId).Pool && c.Rarity is MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Common
                or MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Uncommon or MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Rare));
}

[CustomID("JUNNA_RELIC_POOL")]
public sealed class JunnaRelicPool : CustomRelicPoolModel
{
    public override string BigEnergyIconPath => JunnaMod.ResourceRoot + "images/energy.png";
    public override string TextEnergyIconPath => "res://images/packed/sprite_fonts/defect_energy_icon.png";
}
