using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract class JunnaGem(int energy, string icon) : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => JunnaMod.ResourceRoot + $"images/relics/{icon}.svg";
    protected override string PackedIconOutlinePath => JunnaMod.ResourceRoot + $"images/relics/{icon}_outline.svg";
    protected override string BigIconPath => PackedIconPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(energy)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.ForEnergy(this)];

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || player.PlayerCombatState!.TurnNumber != 1) return;
        // First-turn energy has already refreshed; the reserve survives until the next refresh.
        Flash();
        int before = player.PlayerCombatState.Energy;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
        await EnergyReserve.Add(choiceContext, player, Math.Max(0, player.PlayerCombatState.Energy - before));
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
public sealed class JunnaBurningGem() : JunnaGem(3, "burning_gem");
