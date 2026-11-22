using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Keep the actual material card, its printed cost and its original effect.</summary>
public static class HandArrow
{
    private const int DefaultTurns = 5;
    private sealed class Form(CardType originalType, int turns, bool temporary, Form? previous = null)
    {
        public readonly CardType OriginalType = originalType;
        public readonly int Turns = turns;
        public readonly bool Temporary = temporary;
        public readonly Form? Previous = previous;
    }
    private static readonly ConditionalWeakTable<CardModel, Form> Forms = new();
    private static readonly AsyncLocal<CardModel?> ConvertingPlay = new();
    private static readonly HashSet<MethodBase> PatchedTypes = [];
    private static readonly Harmony Patcher = new(JunnaMod.Id + ".HandArrowTypes");
    public static bool IsArrow(CardModel card) => Forms.TryGetValue(card, out _);
    public static CardType OriginalType(CardModel card) => Forms.TryGetValue(card, out var form) ? form.OriginalType : card.Type;
    public static int Turns(CardModel card) => Forms.TryGetValue(card, out var form) ? form.Turns : card.DynamicVars["Turns"].IntValue;

    internal static int ConversionTurns(CardModel source)
        => (source as CatalogCard)?.Value("HandArrowTurns", DefaultTurns) ?? DefaultTurns;

    private static void Mark(CardModel card, int turns, bool temporary)
    {
        var type = OriginalType(card);
        Forms.TryGetValue(card, out var previous);
        Forms.Remove(card);
        Forms.Add(card, new(type, turns, temporary, temporary ? previous : null));
        var getter = AccessTools.PropertyGetter(card.GetType(), nameof(CardModel.Type));
        getter = getter.DeclaringType!.GetMethod(getter.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        if (!PatchedTypes.Contains(getter))
        {
            Patcher.Patch(getter, postfix: new(typeof(HandArrow), nameof(TypePostfix)));
            PatchedTypes.Add(getter);
        }
    }
    private static void TypePostfix(CardModel __instance, ref CardType __result)
    { if (IsArrow(__instance)) __result = ArrowPile.ArrowType; }

    // Patch shared getters before the engine JITs its card rendering and play pipeline.
    [HarmonyPatch]
    private static class InitialTypes
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(CardModel), typeof(CatalogCard), typeof(JunnaFullDraw) })
            {
                var method = type.GetMethod("get_Type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
                PatchedTypes.Add(method);
                yield return method;
            }
        }
        private static void Postfix(CardModel __instance, ref CardType __result) => TypePostfix(__instance, ref __result);
    }

    public static void Adapt(CardModel card)
    {
        Mark(card, DefaultTurns, temporary: false);
        CostChanges.SetFixed(card, 0);
    }
    internal static void Restore(CardModel card)
    {
        if (!Forms.TryGetValue(card, out var form) || !form.Temporary) return;
        Forms.Remove(card);
        if (form.Previous != null) Forms.Add(card, form.Previous);
    }

    public static async Task Select(PlayerChoiceContext context, CardModel source, int count = 1, bool optional = false)
    {
        var combat = source.Owner.PlayerCombatState!;
        var hand = combat.Hand;
        var selected = await CardActions.Choose(context, source, hand.Cards, count, optional);
        await PlaySelected(context, hand, selected, ConversionTurns(source));
    }

    internal static Task PlaySelected(PlayerChoiceContext context, CardPile hand, IEnumerable<CardModel> selected, int turns)
        => CardExecution.PlayStaged(selected.Where(card => card.Pile == hand).ToArray(),
            card => Play(context, card, turns),
            unplayed => CardPileCmd.Add(unplayed, hand));

    internal static bool Loops(CardModel card) => card is CatalogCard catalog && CardRules.Get(catalog.DesignId).LoopAsArrow;

    public static Task Play(PlayerChoiceContext context, CardModel card)
        => Play(context, card, CardTargets.First(card), DefaultTurns);

    internal static Task<bool> Play(PlayerChoiceContext context, CardModel card, int turns)
        => Play(context, card, CardTargets.First(card), turns);

    internal static Task<bool> ReplayLoop(PlayerChoiceContext context, CardModel card, Creature? target)
        => Play(context, card, target, ConversionTurns(card));

    private static async Task<bool> Play(PlayerChoiceContext context, CardModel card, Creature? target, int turns)
    {
        if (card.TargetType == TargetType.AnyEnemy && target != null && card.CombatState is { } combat)
            target = ArrowCombat.ResolveEffectTarget(target, combat);
        else target = CardTargets.First(card);
        if (!CardTargets.IsValid(card, target)) return false;
        CostChanges.CaptureFreePayment(card);
        Mark(card, turns, temporary: true);
        bool played = false;
        bool unplayable = card.Keywords.Contains(CardKeyword.Unplayable);
        var previousPlay = ConvertingPlay.Value;
        ConvertingPlay.Value = card;
        try
        {
            // Converting a card into an arrow also accepts unplayable material.
            if (unplayable) card.RemoveKeyword(CardKeyword.Unplayable);
            played = await CardExecution.Play(context, card, new CardExecutionRequest
            {
                Target = target, CaptureAvailableX = false, PreserveEnergyValue = true
            });
        }
        finally
        {
            ConvertingPlay.Value = previousPlay;
            if (unplayable) card.AddKeyword(CardKeyword.Unplayable);
            if (!played && ArrowCombat.Find(card) == null) Restore(card);
        }
        return played;
    }

    [HarmonyPatch(typeof(ChainsOfBindingPower), nameof(ChainsOfBindingPower.ShouldPlay))]
    private static class BindingPlayCheck
    {
        private static bool Prefix(CardModel card, ref bool __result)
        {
            if (!ReferenceEquals(ConvertingPlay.Value, card)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ChainsOfBindingPower), nameof(ChainsOfBindingPower.BeforeCardPlayed))]
    private static class BindingPlayCount
    {
        private static bool Prefix(CardPlay cardPlay, ref Task __result)
        {
            // A converted play neither checks nor spends the native Bound allowance.
            if (!ReferenceEquals(ConvertingPlay.Value, cardPlay.Card)) return true;
            __result = Task.CompletedTask;
            return false;
        }
    }

    internal static async Task Dispatch(CardModel card, PlayerChoiceContext context, CardPlay play, ICardEffectPayload next)
    {
        play = CardActions.ResolveTarget(play);
        PaidReplay.PrepareEffect(card, play.PlayIndex);
        if (!CardCatalog.IsArrow(card))
        {
            await next.Bind(card)(context, play);
            return;
        }
        if (!play.IsFirstInSeries) return;
        int turns = Turns(card);
        if (!IsArrow(card) && CardCatalog.DesignId(card) == "JA59") turns = Math.Max(0, turns - card.ResolveEnergyXValue());
        await ArrowCombat.DeployAsync(card, play, turns, next);
    }

    internal static async Task<bool> Finish(PlayerChoiceContext context, CardModel card, bool forceExhaust = false)
    {
        if (!Forms.TryGetValue(card, out var form)) return false;
        Restore(card);
        if (card.Pile?.Type != ArrowPile.PileId) return false;
        if (form.OriginalType == CardType.Power && !forceExhaust)
        {
            await CardPileCmd.RemoveFromCombat(card);
            return true;
        }
        return false;
    }

    internal static void Copy(CardModel source, CardModel copy)
    {
        if (Forms.TryGetValue(source, out var form)) Forms.AddOrUpdate(copy, form);
    }
}
