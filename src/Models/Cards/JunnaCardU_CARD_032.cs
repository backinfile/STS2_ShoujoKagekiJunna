using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_032
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await PlayerCmd.GainEnergy(Value("Energy"), Owner);
        await Attack(context, play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
