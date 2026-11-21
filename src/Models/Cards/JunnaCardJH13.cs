using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJH13
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        var player = Owner;
        await Attack(context, play, hits: Value("Repeat") + player.PlayerCombatState!.Energy / Value("Hits") * Value("Hits4"));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
