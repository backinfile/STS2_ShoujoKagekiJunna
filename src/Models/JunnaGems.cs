using BaseLib.Utils;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract class JunnaGem(bool everyTurn, string icon) : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => JunnaMod.ResourceRoot + $"images/relics/{icon}.svg";
    protected override string PackedIconOutlinePath => JunnaMod.ResourceRoot + $"images/relics/{icon}_outline.svg";
    protected override string BigIconPath => PackedIconPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new CardHoverTip(CardCatalog.Model("JA119"))];

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || (!everyTurn && player.PlayerCombatState!.TurnNumber != 1)) return;
        Flash();
        await CardActions.Generate(player, "JA119", 1, MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand);
    }
}

[CustomID("JUNNA_GREEN_GEM")]
[Pool(typeof(JunnaRelicPool))]
public sealed class JunnaGreenGem() : JunnaGem(false, "green_gem")
{
    public override RelicModel GetUpgradeReplacement() => ModelDb.Relic<JunnaBurningGem>();
}

[CustomID("JUNNA_BURNING_GEM")]
[Pool(typeof(JunnaRelicPool))]
public sealed class JunnaBurningGem() : JunnaGem(true, "burning_gem");
