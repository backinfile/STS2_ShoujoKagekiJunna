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
    protected override CardLocation GetResultLocationForCardPlay() => new(Owner, ArrowPile.PileId, CardPilePosition.Bottom);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsFirstInSeries) ArrowCombat.Deploy(this, play, DynamicVars["Turns"].IntValue);
        return Task.CompletedTask;
    }
    internal async Task ResolveArrow(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target?.IsAlive == true)
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target)
                .WithHitFx(VfxCmd.slashPath).Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(7);
}
