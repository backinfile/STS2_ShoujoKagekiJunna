using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK82
{
    public override int MaxUpgradeLevel => int.MaxValue;
    protected override void OnUpgrade() => DynamicVars[CardValueNames.Resolve(DesignId, "Energy")].UpgradeValueBy(1);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await PlayerCmd.GainEnergy(Value("Energy"), Owner);
    }
}
