using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK206
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [.. base.CanonicalVars, new ExtraDamageVar(1), new CountedDamageVar(static card => card.Owner.Deck.Cards.Count)];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
