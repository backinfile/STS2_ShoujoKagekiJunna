using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA81
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        var linked = await CardEffects.DealDamage(this, context, play, Value("Damage"), 1);
        await CardActions.Fly(context, player, linked.Sum(r => r.TotalDamage), play.Target);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
