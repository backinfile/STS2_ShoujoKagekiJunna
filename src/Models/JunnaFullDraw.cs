using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_FULL_DRAW")]
public sealed class JunnaFullDraw() : JunnaCard(1, ArrowPile.ArrowType, CardRarity.Basic, TargetType.AnyEnemy, "JB03")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(25, ValueProp.Move), new DynamicVar("Turns", 3)];
    public override CardType Type => ArrowConversion.IsConverted(this) ? CardType.Attack : ArrowPile.ArrowType;
#if STS2_BETA
    protected override CardLocation GetResultLocationForCardPlay() => CardCatalog.IsArrow(this)
        ? new(Owner, ArrowPile.PileId, CardPilePosition.Bottom) : base.GetResultLocationForCardPlay();
#else
    protected override PileType GetResultPileTypeForCardPlay() => CardCatalog.IsArrow(this)
        ? ArrowPile.PileId : base.GetResultPileTypeForCardPlay();
#endif
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!CardCatalog.IsArrow(this)) { await ResolveArrow(context, play); return; }
        if (!play.IsFirstInSeries) return;
        ArrowCombat.Deploy(this, play, DynamicVars["Turns"].IntValue);
        await CardPileCmd.Add(this, ArrowPile.PileId);
    }
    internal async Task ResolveArrow(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target?.IsAlive == true)
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target)
                .WithHitFx(VfxCmd.slashPath).Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(7);
}
