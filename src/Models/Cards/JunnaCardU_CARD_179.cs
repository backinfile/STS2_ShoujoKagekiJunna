using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_179
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
        => await Attack(context, CardActions.ResolveTarget(play));

    internal override Task OnPermanentShineDepleted(ShineDepletionContext context)
        => KnowledgeProgress.UpgradeRandomKnowledge(context.Player, Value("Upgrades"));

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
