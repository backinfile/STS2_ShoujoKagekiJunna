using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_185
{
    internal override bool CanBeGeneratedAsKnowledgeMember => false;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
        => await Attack(context, CardActions.ResolveTarget(play));

    internal override Task OnPermanentShineDepleted(ShineDepletionContext context)
        => KnowledgeProgress.GrantFriends(context.Player, Value("Cards"));

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
