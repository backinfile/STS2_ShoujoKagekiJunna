using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
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
    private sealed class Battle
    {
        internal readonly List<ArrowDeployment> Arrows = [];
        internal readonly Dictionary<ulong, int> LastTick = [];
        internal readonly Dictionary<Creature, Creature[]> DeathOrders = [];
        internal long Sequence;
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<ArrowDeployment?> ReplayedDeployment = new();
    private static Battle For(ICombatState state) => Battles.GetOrCreateValue(state);
    public static IReadOnlyList<ArrowDeployment> Deployed =>
        CombatManager.Instance.DebugOnlyGetState() is { } state && Battles.TryGetValue(state, out var battle) ? battle.Arrows : [];
    internal static IEnumerable<ArrowDeployment> WaitingFor(Player owner) =>
        Deployed.Where(arrow => arrow.Card.Owner == owner && !arrow.Resolving && arrow.Remaining > 0 &&
            arrow.Card.Pile?.Type == ArrowPile.PileId);

    internal static IEnumerable<Creature> SingleEnemyTargets(Player owner) =>
        WaitingFor(owner).Where(arrow => !arrow.TargetsGroup)
            .Select(arrow => arrow.Target).OfType<Creature>().Where(target => target.IsEnemy);

    public static ArrowDeployment[] ResolutionOrder() => Deployed.OrderBy(a => a.TargetCategory)
        .ThenBy(a => a.Target is { } target ? Array.IndexOf(a.Card.CombatState?.Creatures.ToArray() ?? [], target) : 0)
        .ThenBy(a => a.Sequence).ToArray();
    public static ArrowDeployment[] DisplayOrder() => ResolutionOrder();
    public static ArrowDeployment? Find(CardModel card) =>
        card.IsMutable && card.Owner?.Creature.CombatState is { } state && Battles.TryGetValue(state, out var battle)
            ? battle.Arrows.FirstOrDefault(a => a.Card == card) : null;
    public static void Cancel(CardModel card)
    {
        if (card.IsMutable && card.Owner?.Creature.CombatState is { } state && Battles.TryGetValue(state, out var battle))
            battle.Arrows.RemoveAll(a => a.Card == card);
        HandArrow.Restore(card);
    }

    internal static async Task<bool> Replay(ArrowDeployment arrow, Func<Task<bool>> play)
    {
        if (!Battles.TryGetValue(arrow.Combat, out var battle) || !battle.Arrows.Remove(arrow)) return false;
        // The replay keeps the material card's temporary arrow form as it crosses the play pile.
        var previous = ReplayedDeployment.Value;
        ReplayedDeployment.Value = arrow;
        try { return await play(); }
        finally
        {
            ReplayedDeployment.Value = previous;
            var card = arrow.Card;
            if (Find(card) == null)
            {
                if (card.Pile?.Type == ArrowPile.PileId && card.CombatState == arrow.Combat &&
                    Battles.TryGetValue(arrow.Combat, out var current) && current == battle)
                    battle.Arrows.Add(arrow);
                else
                    HandArrow.Restore(card);
            }
        }
    }

    internal static Task AfterMovement(CardMovement movement)
    {
        if (movement.OldPile == ArrowPile.PileId && movement.Card.Pile?.Type != ArrowPile.PileId && Find(movement.Card) != null)
            Cancel(movement.Card);
        return Task.CompletedTask;
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
        bool exhaust = card.Owner.Creature.GetPower<JunnaPowerJA105>()?.MakesArrowsExhaust == true;
        if (exhaust) card.AddKeyword(CardKeyword.Exhaust);
        int round = card.Owner.PlayerCombatState!.TurnNumber;
        if (ReplayedDeployment.Value is { } replay && replay.Card == card && replay.Combat == card.CombatState)
        {
            turns = replay.Remaining;
            round = replay.DeployedRound;
        }
        return new(card, play, turns, round, resolve)
            { ExhaustAfterResolve = exhaust, Payload = payload };
    }

    private static ArrowDeployment Register(ArrowDeployment arrow)
    {
        if (Find(arrow.Card) is { Resolving: false } existing)
        {
            existing.PlayCount += arrow.PlayCount;
            return existing;
        }
        var battle = For(arrow.Combat);
        battle.Arrows.RemoveAll(a => a.Card == arrow.Card);
        arrow.Sequence = battle.Sequence++;
        battle.Arrows.Add(arrow);
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
        var battle = For(player.Creature.CombatState!);
        if (battle.LastTick.TryGetValue(player.NetId, out var previous) && previous == round) return;
        battle.LastTick[player.NetId] = round;
        foreach (var arrow in ResolutionOrder().Where(a => a.Card.Owner == player && a.DeployedRound < round).ToArray())
        {
            await Accelerate(context, arrow, 1);
        }
    }

    public static Task AccelerateAll(PlayerChoiceContext context, Player player, int turns)
        => ApplyAcceleration(context, player, new(ArrowAccelerationTargets.All, turns));

    internal static Task AccelerateFromCard(PlayerChoiceContext context, JunnaCard card, Creature? target = null)
        => ApplyAcceleration(context, card.Owner, card.Acceleration, target);

    internal static async Task ApplyAcceleration(PlayerChoiceContext context, Player player,
        ArrowAcceleration acceleration, Creature? target = null, bool snapshotTargets = false)
    {
        var arrows = ResolutionOrder().Where(arrow => acceleration.Matches(arrow, player, target));
        if (acceleration.Targets == ArrowAccelerationTargets.Target)
            arrows = arrows.Where(arrow => !arrow.Resolving).ToArray();
        else if (snapshotTargets) arrows = arrows.ToArray();
        foreach (var arrow in arrows)
            await Accelerate(context, arrow, acceleration.Turns);
    }

    public static Task CopyAll(PlayerChoiceContext context, Player player)
        => CopyAll(context, player, exhaustCopies: false);

    internal static async Task CopyAll(PlayerChoiceContext context, Player player, bool exhaustCopies)
    {
        var originals = DisplayOrder().Where(a => a.Card.Owner == player && !a.Resolving && a.Remaining > 0).ToArray();
        using var single = KnowledgeCombat.SingleCard();
        foreach (var original in originals)
        {
            var copy = CardCopies.ForCombat(original.Card, player, CardCopyPurpose.TemporaryCopy);
            if (exhaustCopies) copy.AddKeyword(CardKeyword.Exhaust);
            var play = new CardPlay
            {
                Card = copy, Target = original.Play.Target, ResultPile = original.Play.ResultPile,
#if STS2_MAIN || STS2_BETA
                Player = player,
#endif
                Resources = original.Play.Resources, IsAutoPlay = true, PlayIndex = 0, PlayCount = original.PlayCount
            };
            var deployment = new ArrowDeployment(copy, play, original.Remaining, player.PlayerCombatState!.TurnNumber,
                original.Payload.Bind(copy).Invoke, original.CopyXValues())
            {
                PlayCount = original.PlayCount, Payload = original.Payload, Destination = original.Destination,
                ExhaustAfterResolve = original.ExhaustAfterResolve || exhaustCopies
            };
            await Place(deployment, () => CardPileCmd.AddGeneratedCardToCombat(copy, ArrowPile.PileId, player), generated: true);
        }
    }

    public static Task AccelerateTarget(PlayerChoiceContext context, Player player, Creature target, int turns)
        => ApplyAcceleration(context, player, new(ArrowAccelerationTargets.Target, turns), target);

    public static Task AccelerateSelf(PlayerChoiceContext context, Player player, int turns)
        => AccelerateSelf(context, player, turns, includeUntargeted: true);

    internal static Task AccelerateSelf(PlayerChoiceContext context, Player player, int turns, bool includeUntargeted)
        => ApplyAcceleration(context, player, new(includeUntargeted
            ? ArrowAccelerationTargets.SelfOrUntargeted : ArrowAccelerationTargets.Self, turns));

    public static Task AccelerateEnemies(PlayerChoiceContext context, Player player, int turns)
        => ApplyAcceleration(context, player, new(ArrowAccelerationTargets.Enemies, turns));

    public static async Task Accelerate(PlayerChoiceContext context, ArrowDeployment arrow, int turns)
    {
        if (arrow.Resolving || Find(arrow.Card) != arrow) return;
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
        // Ready arrows complete even when their target is temporarily unavailable.
        arrow.Resolving = true;
        context.PushModel(arrow.Card);
        try
        {
            await using var resolutionBatch = Shine.BeginResolutionBatch();
            using var attack = ArrowAttackAnimation.Begin(arrow.Card.Owner);
            if (hud != null) await hud.ReleaseArrow(arrow);
            if (CombatManager.Instance.DebugOnlyGetState() != arrow.Combat || Find(arrow.Card) != arrow
                || arrow.Card.Owner.Creature.IsDead) return;
            for (int replay = 0; replay < arrow.PlayCount; replay++)
                await arrow.Resolve(context, replay);
            if (CombatManager.Instance.DebugOnlyGetState() != arrow.Combat || !CombatManager.Instance.IsInProgress
                || !Battles.TryGetValue(arrow.Combat, out var battle)) return;
            // A resolving arrow may have deployed itself again. Keep that new deployment intact.
            battle.Arrows.Remove(arrow);
            if (Find(arrow.Card) != null) return;
            if (arrow.Card.Pile?.Type == ArrowPile.PileId && !arrow.ExhaustsWhenResolved && HandArrow.Loops(arrow.Card))
            {
                Register(arrow.Renew(HandArrow.ConversionTurns(arrow.Card), arrow.Card.Owner.PlayerCombatState!.TurnNumber));
                return;
            }
            if (await HandArrow.Finish(context, arrow.Card, arrow.ExhaustsWhenResolved)) return;
            if (arrow.Card.Pile?.Type != ArrowPile.PileId) return;
            using var pileExit = new ArrowPileExit(hud?.PileExitPosition(arrow.Card));
            if (arrow.ExhaustsWhenResolved)
            {
                arrow.Card.ExhaustOnNextPlay = false;
                await CardCmd.Exhaust(context, arrow.Card, skipVisuals: true);
                // Silent transfers skip the native completion event used by the pile counter.
                if (arrow.Card.Pile?.Type == PileType.Exhaust) PileType.Exhaust.GetPile(arrow.Card.Owner).InvokeCardAddFinished();
            }
            else
            {
                var returnPower = arrow.Card.Owner.Creature.GetPower<JunnaPowerJA22>();
                await (returnPower?.Redirect(arrow) ?? arrow.Destination).Move(context, arrow.Card);
            }
        }
        finally { context.PopModel(arrow.Card); arrow.Resolving = false; }
    }

    internal static async Task AfterPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (!play.IsLastInSeries) return;
        if (!CardCatalog.IsArrow(play.Card) && HandArrow.Loops(play.Card))
            await HandArrow.ReplayLoop(context, play.Card, play.Target);
        else if (Find(play.Card) is { Remaining: 0 } ready) await Accelerate(context, ready, 0);
    }

    internal static void Clear()
    {
        if (CombatManager.Instance.DebugOnlyGetState() is { } state)
        {
            if (Battles.TryGetValue(state, out var battle))
                foreach (var arrow in battle.Arrows) HandArrow.Restore(arrow.Card);
            Battles.Remove(state);
        }
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
    private static class CombatReset
    {
        private static void Prefix() => Clear();
    }

#if STS2_MAIN || STS2_BETA
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.RemoveDeadPlayerCardsFromCombat))]
#else
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.HandlePlayerDeath))]
#endif
    private static class DeathCleanup
    {
        private static void Postfix(Player player, ref Task __result)
        {
            var state = player.Creature.CombatState;
            if (state == null || !Battles.TryGetValue(state, out var battle)) return;
            var cards = battle.Arrows.Where(arrow => arrow.Card.Owner == player).Select(arrow => arrow.Card).ToArray();
            if (cards.Length > 0) __result = Cleanup(__result, player, state, cards);
        }

        private static async Task Cleanup(Task result, Player player, ICombatState combat, CardModel[] cards)
        {
            await result;
            if (CombatManager.Instance.DebugOnlyGetState() != combat || !CombatManager.Instance.IsInProgress
                || !player.Creature.IsDead) return;
            var remaining = cards.Where(card => card.Pile?.Type == ArrowPile.PileId).ToArray();
            if (remaining.Length > 0) await CardPileCmd.RemoveFromCombat(remaining);
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.RemoveFromCombat), [typeof(IEnumerable<CardModel>), typeof(bool)])]
    private static class RemoveFromCombat
    {
        private static void Postfix(IEnumerable<CardModel> cards, ref Task __result)
        {
            var deployed = cards.Where(card => Find(card) != null).ToArray();
            if (deployed.Length > 0) __result = CompleteRemoval(__result, deployed);
        }
        private static async Task CompleteRemoval(Task result, CardModel[] cards)
        {
            try { await result; }
            finally
            {
                foreach (var card in cards)
                    if (card.Pile?.Type != ArrowPile.PileId) Cancel(card);
            }
        }
    }
}
