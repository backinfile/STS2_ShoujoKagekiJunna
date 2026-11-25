using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

public sealed class ArrowDeployment(CardModel card, CardPlay play, int remaining, int round,
    Func<PlayerChoiceContext, CardPlay, Task> resolve, int[]? xValues = null)
{
    public CardModel Card { get; } = card;
    internal ICombatState Combat { get; } = card.Owner.Creature.CombatState!;
    internal ArrowDestination Destination { get; init; } = ArrowDestination.Take(card, play.ResultPile);
    private ICardEffectPayload? _payload;
    internal ICardEffectPayload Payload
    {
        get => _payload ?? CoreApi.Get<ICardPayloadRouting>().Native;
        init => _payload = value;
    }
    public CardPlay Play { get; private set; } = play;
    public bool TargetsAllEnemies => Card.TargetType == TargetType.AllEnemies;
    public bool TargetsAllPlayers => Card.TargetType == TargetType.AllAllies;
    public bool TargetsGroup => TargetsAllEnemies || TargetsAllPlayers;
    public Creature? Target => Play.Target ?? (Card.TargetType is TargetType.Self or TargetType.AnyPlayer ? Card.Owner.Creature : null);
    public int TargetCategory => TargetsAllPlayers ? 0 : TargetsAllEnemies ? 2 : Target?.IsEnemy == true ? 3 : 1;
    public long Sequence { get; internal set; }
    public IEnumerable<Creature> Targets => TargetsAllPlayers ? Card.CombatState?.PlayerCreatures ?? []
        : TargetsAllEnemies ? Card.CombatState?.GetOpponentsOf(Card.Owner.Creature) ?? []
        : Target is { } target ? [target] : [];
    internal void Retarget(Creature target) => Play = GameApi.WithTarget(Play, target);
    public int Remaining { get; internal set; } = remaining;
    public int DeployedRound { get; } = round;
    public int PlayCount { get; internal set; } = play.PlayCount;
    private readonly int[] _xValues = xValues ?? PaidReplay.Capture(card);
    internal int[] CopyXValues() => _xValues.ToArray();
    internal ArrowDeployment Renew(int turns, int round) => new(Card, Play, turns, round, resolve, _xValues)
    {
        Destination = Destination,
        Payload = Payload,
        PlayCount = PlayCount
    };

    internal async Task Resolve(PlayerChoiceContext context, int index)
    {
        // Release animation and prior replays can end combat before this payload begins.
        var combat = Card.Owner.Creature.CombatState;
        if (combat != Combat || CombatManager.Instance.DebugOnlyGetState() != Combat
            || CombatManager.Instance.IsOverOrEnding || Card.Owner.Creature.IsDead) return;
        if (Card.TargetType == TargetType.AnyEnemy)
        {
            var target = Play.Target == null ? null : ArrowCombat.ResolveEffectTarget(Play.Target, combat);
            if (target == null) return;
            Retarget(target);
        }
        else if (TargetsAllEnemies && !combat.GetOpponentsOf(Card.Owner.Creature).Any(target => target.IsAlive && target.IsHittable)) return;
        if (Card.EnergyCost.CostsX && index < _xValues.Length) Card.EnergyCost.CapturedXValue = _xValues[index];
        var actualPlay = new CardPlay
        {
            Card = Card, Target = Play.Target, ResultPile = Play.ResultPile,
#if STS2_MAIN || STS2_BETA
            Player = Card.Owner,
#endif
            Resources = Play.Resources, IsAutoPlay = Play.IsAutoPlay,
            PlayIndex = index, PlayCount = PlayCount
        };
        await ArrowNativeHooks.Resolve(context, actualPlay,
            () => Shine.ResolveAsync(Card, context, actualPlay, () => resolve(context, actualPlay), CardResolutionKind.Arrow));
    }
    public bool ExhaustAfterResolve { get; internal set; }
    internal bool ExhaustsWhenResolved => Destination.Pile == PileType.Exhaust || ExhaustAfterResolve || Card.ExhaustOnNextPlay ||
        Card.Keywords.Contains(CardKeyword.Exhaust);
    internal bool Resolving;
}
