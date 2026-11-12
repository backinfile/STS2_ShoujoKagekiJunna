using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Keep the actual material card, its printed cost and its original effect.</summary>
public static class HandArrow
{
    private sealed class Form(CardType originalType, bool temporary, Form? previous = null)
    {
        public readonly CardType OriginalType = originalType;
        public readonly bool Temporary = temporary;
        public readonly Form? Previous = previous;
    }
    private static ConditionalWeakTable<CardModel, Form> Forms = new();
    private static readonly HashSet<MethodBase> PatchedTypes = [];
    private static readonly Harmony Patcher = new(JunnaMod.Id + ".HandArrowTypes");
    public static bool IsArrow(CardModel card) => Forms.TryGetValue(card, out _);
    public static CardType OriginalType(CardModel card) => Forms.TryGetValue(card, out var form) ? form.OriginalType : card.Type;
    public static int Turns(CardModel card) => IsArrow(card) ? 5 : card.DynamicVars["Turns"].IntValue;

    private static void Mark(CardModel card, bool temporary)
    {
        var type = OriginalType(card);
        Forms.TryGetValue(card, out var previous);
        Forms.Remove(card);
        Forms.Add(card, new(type, temporary, temporary ? previous : null));
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
        Mark(card, false);
        CostChanges.SetFixed(card, 0);
    }
    internal static void Restore(CardModel card)
    {
        if (!Forms.TryGetValue(card, out var form) || !form.Temporary) return;
        Forms.Remove(card);
        if (form.Previous != null) Forms.Add(card, form.Previous);
    }
    internal static void Clear() => Forms = new();

    public static async Task Select(PlayerChoiceContext context, CardModel source, int count = 1, bool optional = false)
    {
        var combat = source.Owner.PlayerCombatState!;
        var hand = combat.Hand;
        var selected = (await CardActions.Choose(context, source, hand.Cards, count, optional))
            .Where(card => card.Pile == hand).ToArray();
        await CardExecution.PlayStaged(selected, card => Play(context, card),
            unplayed => CardPileCmd.Add(unplayed, hand));
    }

    public static async Task Play(PlayerChoiceContext context, CardModel card)
    {
        var target = CardTargets.First(card);
        if (!CardTargets.IsValid(card, target)) return;
        CostChanges.CaptureFreePayment(card);
        Mark(card, true);
        bool played = false;
        bool unplayable = card.Keywords.Contains(CardKeyword.Unplayable);
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
            if (unplayable) card.AddKeyword(CardKeyword.Unplayable);
            if (!played && ArrowCombat.Find(card) == null) Restore(card);
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
