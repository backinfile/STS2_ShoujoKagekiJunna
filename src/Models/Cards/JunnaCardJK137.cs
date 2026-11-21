using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK137
{
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardChoices.Notes(context, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
