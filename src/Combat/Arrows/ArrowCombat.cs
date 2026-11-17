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
    public static ArrowDeployment[] ResolutionOrder() => Arrows.OrderBy(a => a.TargetCategory)
        .ThenBy(a => a.Target is { } target ? Array.IndexOf(a.Card.CombatState?.Creatures.ToArray() ?? [], target) : 0)
        .ThenBy(a => a.Sequence).ToArray();
    public static ArrowDeployment[] DisplayOrder() => ResolutionOrder();
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
        Register(CreateDeployment(card, play, turns, resolve, CoreApi.Get<ICardPayloadRouting>().Native));
        EnsureHud();
    }

    internal static Task DeployAsync(CardModel card, CardPlay play, int turns, ICardEffectPayload payload)
        => Place(CreateDeployment(card, play, turns, payload.Bind(card).Invoke, payload),
            () => CardPileCmd.Add(card, ArrowPile.PileId));

    private static ArrowDeployment CreateDeployment(CardModel card, CardPlay play, int turns,
        Func<PlayerChoiceContext, CardPlay, Task> resolve, ICardEffectPayload payload)
    {
        int haste = CatalogPower.ArrowHaste(card.Owner);
        if (haste > 0) card.AddKeyword(CardKeyword.Exhaust);
        return new(card, play, turns, card.Owner.PlayerCombatState!.TurnNumber, resolve)
            { Sequence = _sequence, ExhaustAfterResolve = haste > 0, Payload = payload };
    }

    private static ArrowDeployment Register(ArrowDeployment arrow)
    {
        if (Find(arrow.Card) is { Resolving: false } existing)
        {
            existing.PlayCount += arrow.PlayCount;
            return existing;
        }
        Arrows.RemoveAll(a => a.Card == arrow.Card);
        Arrows.Add(arrow);
        _sequence++;
        return arrow;
    }

    private static void EnsureHud()
    {
        var room = NCombatRoom.Instance;
        if (room != null && room.GetNodeOrNull<ArrowHud>("JunnaArrows") == null)
            room.AddChild(new ArrowHud { Name = "JunnaArrows" });
    }

    private static async Task Place(ArrowDeployment arrow, Func<Task> move, bool generated = false)
    {
        arrow = Register(arrow);
        try
        {
            EnsureHud();
            await move();
        }
        finally
        {
            // Move hooks may fail after the native move has already committed.
            if (arrow.Card.Pile?.Type != ArrowPile.PileId && ReferenceEquals(Find(arrow.Card), arrow))
                Cancel(arrow.Card);
            if (generated && arrow.Card.Pile == null) arrow.Card.RemoveFromState();
        }
    }

    internal static async Task Advance(PlayerChoiceContext context, Player player)
    {
        int round = player.PlayerCombatState!.TurnNumber;
        if (LastTick.TryGetValue(player.NetId, out var previous) && previous == round) return;
        LastTick[player.NetId] = round;
        foreach (var arrow in ResolutionOrder().Where(a => a.Card.Owner == player && a.DeployedRound < round).ToArray())
        {
            await Accelerate(context, arrow, 1);
        }
    }

    public static async Task AccelerateAll(PlayerChoiceContext context, Player player, int turns)
    {
        foreach (var arrow in ResolutionOrder().Where(a => a.Card.Owner == player))
            await Accelerate(context, arrow, turns);
    }

    public static async Task CopyAll(PlayerChoiceContext context, Player player)
    {
        var originals = DisplayOrder().Where(a => a.Card.Owner == player && !a.Resolving && a.Remaining > 0).ToArray();
        using var single = KnowledgeCombat.SingleCard();
        foreach (var original in originals)
        {
            var copy = CardCopies.ForCombat(original.Card, player, CardCopyPurpose.TemporaryCopy);
            var play = new CardPlay
            {
                Card = copy, Target = original.Play.Target, ResultPile = original.Play.ResultPile,
#if STS2_BETA
                Player = player,
#endif
                Resources = original.Play.Resources, IsAutoPlay = true, PlayIndex = 0, PlayCount = original.PlayCount
            };
            var deployment = new ArrowDeployment(copy, play, original.Remaining, player.PlayerCombatState!.TurnNumber,
                original.Payload.Bind(copy).Invoke, original.CopyXValues())
            {
                Sequence = _sequence, PlayCount = original.PlayCount, Payload = original.Payload,
                ExhaustAfterResolve = original.ExhaustAfterResolve, AttacksWhileWaiting = original.AttacksWhileWaiting
            };
            await Place(deployment, () => CardPileCmd.AddGeneratedCardToCombat(copy, ArrowPile.PileId, player), generated: true);
        }
    }

    public static async Task AccelerateTarget(PlayerChoiceContext context, Player player, Creature target, int turns)
    {
        var arrows = ResolutionOrder().Where(a => a.Card.Owner == player && !a.Resolving &&
            a.Targets.Contains(target)).ToArray();
        foreach (var arrow in arrows) await Accelerate(context, arrow, turns);
    }

    public static async Task AccelerateSelf(PlayerChoiceContext context, Player player, int turns)
    {
        foreach (var arrow in ResolutionOrder().Where(a => a.Card.Owner == player && !a.TargetsGroup &&
                     (a.Target == player.Creature || a.Target == null)))
            await Accelerate(context, arrow, turns);
    }

    public static async Task AccelerateEnemies(PlayerChoiceContext context, Player player, int turns)
    {
        foreach (var arrow in ResolutionOrder().Where(a => a.Card.Owner == player &&
                     (a.TargetsAllEnemies || a.Target?.IsEnemy == true)))
            await Accelerate(context, arrow, turns);
    }

    public static async Task Accelerate(PlayerChoiceContext context, ArrowDeployment arrow, int turns)
    {
        if (arrow.Resolving || !Arrows.Contains(arrow)) return;
        if (arrow.Card.Pile?.Type != ArrowPile.PileId) { Cancel(arrow.Card); return; }
        if (!arrow.TargetsGroup && arrow.Target is { } target && arrow.Card.CombatState is { } state)
        {
            var replacement = ResolveEffectTarget(target, state);
            if (replacement != null && replacement != target) arrow.Retarget(replacement);
        }
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
            if (!arrow.ExhaustAfterResolve && !arrow.Card.ExhaustOnNextPlay &&
                !arrow.Card.Keywords.Contains(CardKeyword.Exhaust) && HandArrow.Loops(arrow.Card) &&
                await HandArrow.Play(context, arrow.Card, arrow.Play.Target)) return;
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
        if (!play.IsLastInSeries) return;
        if (!CardCatalog.IsArrow(play.Card) && HandArrow.Loops(play.Card))
            await HandArrow.Play(context, play.Card, play.Target);
        else if (Find(play.Card) is { Remaining: 0 } ready) await Accelerate(context, ready, 0);
    }

    internal static void Clear() { Arrows.Clear(); HandArrow.Clear(); LastTick.Clear(); DeathOrders.Clear(); _sequence = 0; }
}
