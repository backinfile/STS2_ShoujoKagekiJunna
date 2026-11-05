#if !STS2_BETA
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Combat;

internal static class StableCardApi
{
    public static AttackCommand FromCard(this AttackCommand command, CardModel card, CardPlay? play) => command.FromCard(card);
    public static CardModel CreateCloneForPlayer(this CardModel card, Player player) => card.CreateClone();
}
#endif
