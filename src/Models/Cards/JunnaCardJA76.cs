using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJA76
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        var swept = await CardEffects.DealDamage(this, context, play, Value("Damage"), 1);
        foreach (var group in swept.GroupBy(r => r.Receiver))
            await CardActions.Fly(context, player, group.Sum(r => r.TotalDamage), group.Key);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
