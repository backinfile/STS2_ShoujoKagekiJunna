using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

public sealed class ArrowDeployment(CardModel card, CardPlay play, int remaining, int round,
    Func<PlayerChoiceContext, CardPlay, Task> resolve)
{
    public CardModel Card { get; } = card;
    public CardPlay Play { get; private set; } = play;
    public bool TargetsAllEnemies => Card.TargetType == TargetType.AllEnemies;
    public bool TargetsAllPlayers => Card.TargetType == TargetType.AllAllies;
    public bool TargetsGroup => TargetsAllEnemies || TargetsAllPlayers;
    public Creature? Target => Play.Target ?? (Card.TargetType is TargetType.Self or TargetType.AnyPlayer ? Card.Owner.Creature : null);
    public int TargetCategory => TargetsAllPlayers ? 0 : TargetsAllEnemies ? 2 : Target?.IsEnemy == true ? 3 : 1;
    public long Sequence { get; internal init; }
    public IEnumerable<Creature> Targets => TargetsAllPlayers ? Card.CombatState?.PlayerCreatures ?? []
        : TargetsAllEnemies ? Card.CombatState?.GetOpponentsOf(Card.Owner.Creature) ?? []
        : Target is { } target ? [target] : [];
    internal void Retarget(Creature target) => Play = new CardPlay
    {
        Card = Play.Card, Target = target, ResultPile = Play.ResultPile,
#if STS2_BETA
        Player = Play.Card.Owner,
#endif
        Resources = Play.Resources, IsAutoPlay = Play.IsAutoPlay, PlayIndex = Play.PlayIndex, PlayCount = Play.PlayCount
    };
    public int Remaining { get; internal set; } = remaining;
    public int DeployedRound { get; } = round;
    public int AttacksWhileWaiting { get; internal set; }
    public int PlayCount { get; internal set; } = play.PlayCount;
    private readonly int[] _xValues = PaidReplay.Capture(card);
    internal async Task Resolve(PlayerChoiceContext context, int index)
    {
        using var resolution = DamageExecution.BeginCardResolution(Card);
        if (Card.EnergyCost.CostsX && index < _xValues.Length) Card.EnergyCost.CapturedXValue = _xValues[index];
        var actualPlay = new CardPlay
        {
            Card = Card, Target = Play.Target, ResultPile = Play.ResultPile,
#if STS2_BETA
            Player = Card.Owner,
#endif
            Resources = Play.Resources, IsAutoPlay = Play.IsAutoPlay,
            PlayIndex = index, PlayCount = PlayCount
        };
        await Shine.ResolveAsync(Card, context, actualPlay, () => resolve(context, actualPlay), CardResolutionKind.Arrow);
    }
    internal bool Resolving;
}
