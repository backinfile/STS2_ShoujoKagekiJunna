using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

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
        int haste = CatalogPower.ArrowHaste(card.Owner);
        turns = Math.Max(0, turns - haste);
        if (haste > 0) card.AddKeyword(CardKeyword.Exhaust);
        if (Find(card) is { } existing && !existing.Resolving)
            existing.PlayCount += play.PlayCount;
        else
        {
            Arrows.RemoveAll(a => a.Card == card);
            Arrows.Add(new(card, play, turns, card.Owner.PlayerCombatState!.TurnNumber, resolve) { Sequence = _sequence++, ExhaustAfterResolve = haste > 0 });
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

    public static async Task ActivateHaste(PlayerChoiceContext context, Player player)
    {
        var arrows = DisplayOrder().Where(a => a.Card.Owner == player).ToArray();
        foreach (var arrow in arrows)
        {
            arrow.ExhaustAfterResolve = true;
            arrow.Card.AddKeyword(CardKeyword.Exhaust);
        }
        foreach (var arrow in arrows) await Accelerate(context, arrow, 1);
    }

    public static async Task AccelerateAll(PlayerChoiceContext context, Player player, int turns)
    {
        foreach (var arrow in DisplayOrder().Where(a => a.Card.Owner == player))
            await Accelerate(context, arrow, turns);
    }

    public static async Task AccelerateTarget(PlayerChoiceContext context, Player player, Creature target, int turns)
    {
        var arrows = DisplayOrder().Where(a => a.Card.Owner == player && !a.Resolving &&
            a.Targets.Contains(target)).ToArray();
        foreach (var arrow in arrows) await Accelerate(context, arrow, turns);
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
            if (await HandArrow.Finish(context, arrow.Card, arrow.ExhaustAfterResolve)) return;
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
