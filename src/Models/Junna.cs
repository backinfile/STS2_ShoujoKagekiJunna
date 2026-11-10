using BaseLib.Abstracts;
using BaseLib.Patches.UI;
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
    public override int StartingHp => CardCatalog.StartingHp;
    public override int StartingGold => CardCatalog.StartingGold;
    public override Color NameColor => new("74BDEB");
    public override Color EnergyLabelOutlineColor => new("173C2F");
    public override Color DialogueColor => new("74BDEB");
    public override Color MapDrawingColor => new("74BDEB");
    public override Color RemoteTargetingLineColor => new("74BDEB");
    public override Color RemoteTargetingLineOutline => new("234B75");
    public override CardPoolModel CardPool => ModelDb.CardPool<JunnaCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<JunnaRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SilentPotionPool>();
    public override IEnumerable<CardModel> StartingDeck => CardCatalog.StartingDeck;
    public override IReadOnlyList<RelicModel> StartingRelics => CardCatalog.StartingRelics;
    public override RelicIconData CustomYummyCookie => new(
        JunnaMod.ResourceRoot + "images/relics/yummy_cookie_junna_big.png",
        JunnaMod.ResourceRoot + "images/relics/yummy_cookie_junna.png",
        JunnaMod.ResourceRoot + "images/relics/yummy_cookie_junna_outline.png");
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
    public override Color EnergyOutlineColor => new("173C2F");
    public override string BigEnergyIconPath => JunnaMod.ResourceRoot + "images/energy_counter/energy_icon.png";
    public override string TextEnergyIconPath => JunnaMod.ResourceRoot + "images/energy_counter/energy_icon_small.png";
    protected override IEnumerable<CardModel> FilterThroughEpochs(MegaCrit.Sts2.Core.Unlocks.UnlockState unlockState, IEnumerable<CardModel> cards)
        => base.FilterThroughEpochs(unlockState, cards).Where(CardCatalog.IsRewardCard);
}

[CustomID("JUNNA_RELIC_POOL")]
public sealed class JunnaRelicPool : CustomRelicPoolModel
{
    public override string BigEnergyIconPath => JunnaMod.ResourceRoot + "images/energy_counter/energy_icon.png";
    public override string TextEnergyIconPath => JunnaMod.ResourceRoot + "images/energy_counter/energy_icon_small.png";
}
