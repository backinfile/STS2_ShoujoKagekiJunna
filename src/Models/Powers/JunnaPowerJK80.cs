using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_JK80_POWER")]
public sealed class JunnaPowerJK80() : CatalogPower("JK80")
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!CardCatalog.IsKnowledge(play.Card)) return;
        if (_grants.Remove(grant)) await Block(grant.V("Amount"));
    }
}
