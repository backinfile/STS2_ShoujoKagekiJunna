using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    protected virtual Task TurnStarted(Grant grant, PlayerChoiceContext context) => Task.CompletedTask;
    protected virtual Task TurnEnding(Grant grant, PlayerChoiceContext context) => Task.CompletedTask;
    protected virtual Task BeforeHandFlush(PlayerChoiceContext context, Player player) => Task.CompletedTask;
    protected virtual bool EndsWithTurn => false;
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner.Player) return;
        foreach (var grant in _grants.ToArray()) await TurnStarted(grant, context);
        if (RemainingTriggers != null) Synchronize();
    }
    public override Task BeforeFlush(PlayerChoiceContext context, Player player)
        => player == Owner.Player ? BeforeHandFlush(context, player) : Task.CompletedTask;
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;
        foreach (var grant in _grants.ToArray()) await TurnEnding(grant, context);
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
        => EndsWithTurn && side == CombatSide.Player && participants.Contains(Owner)
            ? PowerCmd.Remove(this) : Task.CompletedTask;
}
