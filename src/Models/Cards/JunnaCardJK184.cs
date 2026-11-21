using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK184
{
    protected override void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("DepletedPartners", KnowledgeProgress.DepletedPartners(this));
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play, hits: Value("Repeat", 1) + KnowledgeProgress.DepletedPartners(this));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
