using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK314
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [.. base.CanonicalVars, .. AttackHitCount.DeclareCalculated(static (card, _) => ((JunnaCardJK314)card).ExtraHits())];

    private int ExtraHits() => DepletedPartners() / Value("CardsPerExtraHit") * Value("ExtraHits");

    private int DepletedPartners()
    {
        if (!IsMutable || Owner is not { } owner) return 0;
        string partner = ModelDb.Card<JunnaCardJK122B>().Id.ToString();
        return Shine.GetDepletedCards(owner).Count(entry => entry.ModelId == partner);
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
