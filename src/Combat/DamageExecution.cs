using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using System.Runtime.CompilerServices;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Async-local scopes isolate nested retaliation, asynchronous effects and generated cards.</summary>
public static class DamageExecution
{
    private sealed class CardResolution(CardModel card)
    {
        public readonly CardModel Card = card;
        public int ExtraHitsClaimed;
    }

    private static readonly AsyncLocal<CardResolution?> ResolutionContext = new();
    private static readonly ConditionalWeakTable<CardPlay, CardResolution> PlayResolutions = new();

    /// <summary>One direct card resolution, including a deferred arrow or a custom scheduler callback.</summary>
    public static IDisposable BeginCardResolution(CardModel card) => new ResolutionScope(new(card));

    internal static IDisposable BeginCardPlay(CardModel card, CardPlay play) =>
        new ResolutionScope(PlayResolutions.GetValue(play, _ => new(card)));

    private sealed class ResolutionScope : IDisposable
    {
        private readonly IDisposable _resolution;
        private readonly IDisposable _source;
        public ResolutionScope(CardResolution resolution)
        {
            _resolution = new Scope<CardResolution?>(ResolutionContext, resolution);
            _source = UseDamageSource(resolution.Card);
        }
        public void Dispose() { _source.Dispose(); _resolution.Dispose(); }
    }

    internal static int ClaimExtraHits(CardModel card, CardPlay? play, DamageModifierSnapshot snapshot)
    {
        if (ResourceLoss.Value) return 0;
        var resolution = ResolutionContext.Value;
        if (resolution == null || !ReferenceEquals(resolution.Card, card))
            resolution = play != null && ReferenceEquals(play.Card, card)
                ? PlayResolutions.GetValue(play, _ => new(card)) : null;
        if (resolution != null && Interlocked.Exchange(ref resolution.ExtraHitsClaimed, 1) != 0) return 0;
        return DamageModifiers.ExtraHits(snapshot);
    }

    private sealed class AttackProgress(int originalHits)
    {
        public int OriginalHits = originalHits;
        public int CompletedHits;
    }
    private static readonly ConditionalWeakTable<AttackCommand, AttackProgress> AttackProgressByCommand = new();
    internal static void PrepareAttack(AttackCommand command, decimal originalHits) =>
        AttackProgressByCommand.AddOrUpdate(command, new((int)Math.Ceiling(originalHits)));

    public static Task RunOriginalHitCallback(Func<Task> callback, AttackCommand command) =>
        AttackProgressByCommand.TryGetValue(command, out var progress) && progress.CompletedHits >= progress.OriginalHits
            ? Task.CompletedTask : callback();
    internal sealed record DamageFrame(AbstractModel? Source, DamageModifierSnapshot Snapshot);
    private sealed class Batch(AbstractModel? source, bool countHandled)
    {
        public readonly AbstractModel? Source = source;
        public readonly bool CountHandled = countHandled;
        public bool Claimed;
    }

    private static readonly AsyncLocal<Batch?> BatchContext = new();
    private static readonly AsyncLocal<AbstractModel?> SourceContext = new();
    private static readonly AsyncLocal<bool> ResourceLoss = new();
    internal static readonly AsyncLocal<DamageFrame?> Current = new();
    internal static AbstractModel? ExplicitSource => SourceContext.Value;

    public static IDisposable UseDamageSource(AbstractModel source) => new Scope<AbstractModel?>(SourceContext, source);
    public static IDisposable MarkResourceLoss() => new Scope<bool>(ResourceLoss, true);
    public static IDisposable BeginDamageBatch(AbstractModel? source, bool countHandled = false) =>
        new Scope<Batch?>(BatchContext, new Batch(source, countHandled));

    private sealed class Scope<T>(AsyncLocal<T> slot, T value) : IDisposable
    {
        private readonly T _previous = Swap(slot, value);
        private bool _disposed;
        private static T Swap(AsyncLocal<T> local, T next)
        {
            T previous = local.Value!;
            local.Value = next;
            return previous;
        }
        public void Dispose()
        {
            if (_disposed) return;
            slot.Value = _previous;
            _disposed = true;
        }
    }

    internal static async Task<IEnumerable<DamageResult>> Resolve(
        PlayerChoiceContext context, IEnumerable<Creature>? targets, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        var list = targets?.ToList();
        var (source, snapshot) = DamageModifiers.Resolve(context, cardSource);
        if (ResourceLoss.Value) snapshot = default;
        var batch = BatchContext.Value;
        bool countHandled = batch is { Claimed: false, CountHandled: true } && ReferenceEquals(batch.Source, source);
        if (batch != null) batch.Claimed = true;
        bool hasOpponent = snapshot.Owner != null && list?.Any(t => t.IsAlive && t.Side != snapshot.Owner.Creature.Side) == true;
        int extra = !countHandled && hasOpponent && source is CardModel card
            ? ClaimExtraHits(card, cardPlay, snapshot) : 0;
        var results = new List<DamageResult>();
        using var frame = new Scope<DamageFrame?>(Current, new(source, snapshot));
        for (int hit = 0; hit <= extra; hit++)
        {
            // The first pass retains all vanilla targets, including self-damage. Only hostile damage is repeated.
            var hitTargets = hit == 0 ? list : list?.Where(t => t.IsAlive && t.Side != snapshot.Owner!.Creature.Side).ToList();
            if (hit > 0 && (hitTargets?.Count == 0 || dealer?.IsDead == true)) break;
            DamagePatches.Bypass.Value = true;
            try
            {
                results.AddRange(await CreatureCmd.Damage(context, hitTargets, amount, props, dealer, cardSource, cardPlay));
            }
            finally { DamagePatches.Bypass.Value = false; }
        }
        return results;
    }

    /// <summary>Only the canonical damage call inside AttackCommand uses this gate. Nested effects create new batches.</summary>
    public static async Task<IEnumerable<DamageResult>> FromAttack(
        PlayerChoiceContext context, IEnumerable<Creature>? targets, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, AttackCommand attack)
    {
        using var batch = BeginDamageBatch(attack.ModelSource, countHandled: true);
        try { return await CreatureCmd.Damage(context, targets, amount, props, dealer, cardSource, cardPlay); }
        finally
        {
            if (AttackProgressByCommand.TryGetValue(attack, out var progress)) progress.CompletedHits++;
        }
    }
}
