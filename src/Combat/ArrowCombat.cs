using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using BaseLib.Utils.Attributes;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKagekiJunna.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

public sealed class ArrowPile() : CustomPile(PileId)
{
    [CustomEnum] public static PileType PileId;
    [CustomEnum] public static CardType ArrowType;
    public override bool CardShouldBeVisible(CardModel card) => false;
    public override bool NeedsCustomTransitionVisual => true;
    public override bool CustomTween(Tween tween, CardModel card, NCard cardNode, CardPile? oldPile)
        => NCombatRoom.Instance?.GetNodeOrNull<ArrowHud>("JunnaArrows")?.AnimateDeployment(tween, card, cardNode) == true;
    public override Vector2 GetTargetPosition(CardModel model, Vector2 size) => new(size.X * .5f, 90);
}

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

public static partial class ArrowCombat
{
    private static readonly List<ArrowDeployment> Arrows = [];
    private static readonly Dictionary<ulong, int> LastTick = [];
    private static long _sequence;
    public static IReadOnlyList<ArrowDeployment> Deployed => Arrows;
    public static ArrowDeployment[] DisplayOrder() => Arrows.OrderBy(a => a.TargetCategory)
        .ThenBy(a => !a.TargetsGroup && a.Target is { } target
            ? NCombatRoom.Instance?.GetCreatureNode(target)?.VfxSpawnPosition.X
                ?? Array.IndexOf(a.Card.CombatState?.Creatures.ToArray() ?? [], target) : 0)
        .ThenBy(a => a.Sequence).ToArray();
    public static ArrowDeployment? Find(CardModel card) => Arrows.FirstOrDefault(a => a.Card == card);
    public static void Cancel(CardModel card)
    {
        Arrows.RemoveAll(a => a.Card == card);
        HandArrow.Restore(card);
    }

    public static void Deploy(JunnaFullDraw card, CardPlay play, int turns)
        => Deploy(card, play, turns, card.ResolveArrow);

    public static void Deploy(CardModel card, CardPlay play, int turns,
        Func<PlayerChoiceContext, CardPlay, Task> resolve)
    {
        if (Find(card) is { } existing && !existing.Resolving)
            existing.PlayCount += play.PlayCount;
        else
        {
            Arrows.RemoveAll(a => a.Card == card);
            Arrows.Add(new(card, play, turns, card.Owner.PlayerCombatState!.TurnNumber, resolve) { Sequence = _sequence++ });
        }
        var room = NCombatRoom.Instance;
        if (room != null && room.GetNodeOrNull<ArrowHud>("JunnaArrows") == null)
            room.AddChild(new ArrowHud { Name = "JunnaArrows" });
    }

    internal static async Task Advance(PlayerChoiceContext context, Player player)
    {
        int round = player.PlayerCombatState!.TurnNumber;
        if (LastTick.TryGetValue(player.NetId, out var previous) && previous == round) return;
        LastTick[player.NetId] = round;
        foreach (var arrow in DisplayOrder().Where(a => a.Card.Owner == player && a.DeployedRound < round))
        {
            await Accelerate(context, arrow, 1);
        }
    }

    public static async Task AccelerateAll(PlayerChoiceContext context, Player player, int turns)
    {
        foreach (var arrow in DisplayOrder().Where(a => a.Card.Owner == player))
            await Accelerate(context, arrow, turns);
    }

    public static async Task AccelerateSelf(PlayerChoiceContext context, Player player, int turns)
    {
        foreach (var arrow in DisplayOrder().Where(a => a.Card.Owner == player && !a.TargetsGroup &&
                     (a.Target == player.Creature || a.Target == null)))
            await Accelerate(context, arrow, turns);
    }

    public static async Task Accelerate(PlayerChoiceContext context, ArrowDeployment arrow, int turns)
    {
        if (arrow.Resolving || !Arrows.Contains(arrow)) return;
        if (arrow.Card.Pile?.Type != ArrowPile.PileId) { Arrows.Remove(arrow); return; }
        int previousRemaining = arrow.Remaining;
        arrow.Remaining = Math.Max(0, arrow.Remaining - Math.Max(0, turns));
        var hud = NCombatRoom.Instance?.GetNodeOrNull<ArrowHud>("JunnaArrows");
        if (arrow.Remaining != previousRemaining) hud?.CountdownChanged(arrow);
        if (arrow.Remaining > 0) return;
        if (AwaitingReturn(arrow)) return;
        arrow.Resolving = true;
        context.PushModel(arrow.Card);
        try
        {
            await using var resolutionBatch = Shine.BeginResolutionBatch();
            using var attack = ArrowAttackAnimation.Begin(arrow.Card.Owner);
            if (hud != null) await hud.ReleaseArrow(arrow);
            for (int replay = 0; replay < arrow.PlayCount; replay++)
                await arrow.Resolve(context, replay);
            // A resolving arrow may have deployed itself again. Keep that new deployment intact.
            Arrows.Remove(arrow);
            if (Find(arrow.Card) != null) return;
            if (await HandArrow.Finish(context, arrow.Card)) return;
            if (arrow.Card.Pile?.Type != ArrowPile.PileId) return;
            using var pileExit = new ArrowPileExit(hud?.PileExitPosition(arrow.Card));
            if (arrow.Card.ExhaustOnNextPlay || arrow.Card.Keywords.Contains(CardKeyword.Exhaust))
            {
                arrow.Card.ExhaustOnNextPlay = false;
                await CardCmd.Exhaust(context, arrow.Card, skipVisuals: true);
                // Silent transfers skip the native completion event used by the pile counter.
                if (arrow.Card.Pile?.Type == PileType.Exhaust) PileType.Exhaust.GetPile(arrow.Card.Owner).InvokeCardAddFinished();
            }
            else if (CatalogPower.Has(arrow.Card.Owner, "JA22")) await CardPileCmd.Add(arrow.Card, PileType.Hand);
            else await CardPileCmd.Add(arrow.Card, PileType.Discard);
        }
        finally { context.PopModel(arrow.Card); arrow.Resolving = false; }
    }

    internal static async Task AfterPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsLastInSeries && Find(play.Card) is { Remaining: 0 } ready) await Accelerate(context, ready, 0);
    }

    internal static void Clear() { Arrows.Clear(); HandArrow.Clear(); LastTick.Clear(); DeathOrders.Clear(); _sequence = 0; }
}

[CustomID("JUNNA_COMBAT_HOOKS")]
public sealed class JunnaCombatHooks : SingletonModel
{
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> MaxEnergyBonuses = new();
    public static void AddMaxEnergy(PlayerCombatState combat, int amount) => MaxEnergyBonuses.GetOrCreateValue(combat).Value += amount;
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
        => amount + (player.PlayerCombatState is { } combat && MaxEnergyBonuses.TryGetValue(combat, out var bonus) ? bonus.Value : 0);
    public override bool ShouldReceiveCombatHooks => true;
    public override Task BeforeCombatStart()
    {
        MaxEnergyBonuses.Clear();
        DamageSourceScopes.Install();
        ArrowCombat.Clear();
        DamageModifiers.ClearCombat();
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext context, Player player) => ArrowCombat.Advance(context, player);
    public override Task BeforeDeath(Creature creature)
    {
        ArrowCombat.RememberDeathOrder(creature);
        return Task.CompletedTask;
    }
    public override Task AfterDeath(PlayerChoiceContext context, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
        => ArrowCombat.AfterTargetDeath(creature, wasRemovalPrevented);
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.IsFirstInSeries && CardCatalog.IsArrow(play.Card))
            foreach (var arrow in ArrowCombat.Deployed.Where(a => a.Card.Owner == play.Card.Owner && a.Card != play.Card && !a.Resolving))
                arrow.AttacksWhileWaiting++;
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play) => ArrowCombat.AfterPlayed(context, play);
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        bool free = CardCatalog.DesignId(card) == "JK45B" && ArrowCombat.Deployed.Any(a => a.Card.Owner == card.Owner && CardCatalog.DesignId(a.Card) == "JK45");
        modifiedCost = free ? 0 : originalCost;
        return free;
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        MaxEnergyBonuses.Clear();
        ArrowCombat.Clear();
        DamageModifiers.ClearCombat();
        return Task.CompletedTask;
    }
}
