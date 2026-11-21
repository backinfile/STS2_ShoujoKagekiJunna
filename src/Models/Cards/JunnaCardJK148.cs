using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK148
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play, hits: Value("Repeat", 1) + KnowledgeProgress.DepletedPartners(this));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
