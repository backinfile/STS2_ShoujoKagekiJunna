using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_052
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var results = await CardEffects.DealDamage(this, context, play, Value("Damage"), 1);
        if (results.Any(result => result.Receiver == play.Target && result.WasTargetKilled))
            await PlayerCmd.GainEnergy(Value("Energy"), Owner);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
