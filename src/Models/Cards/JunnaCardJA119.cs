using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA119
{
    public override TargetType TargetType => TargetType.None;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardChoices.AccelerateTargets(context, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
