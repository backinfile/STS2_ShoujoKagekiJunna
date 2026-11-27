using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_017
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [.. base.CanonicalVars, new CountedDamageVar(CardTutors.CountHandCategories,
            CardRules.Get("U_CARD_017").Field("CategoryDamage"))];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        play = CardActions.ResolveTarget(play);
        await Attack(context, play);
    }

    protected override void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("SatisfiedCategories", CardTutors.CountHandCategories(this));
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
