using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK127 : ITomeCard
{
    internal override bool CanBeGeneratedAsKnowledgeMember => false;

    CharacterModel ITomeCard.TomeCharacter => ModelDb.Character<Junna>();

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CatalogPower.Install<JunnaPowerJK127>(this, context, play);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
