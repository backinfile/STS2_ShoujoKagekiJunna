using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Change the same combat instance only after its full play and replays have finished.</summary>
internal static class CardForms
{
    private sealed class Pending { public int Depth; public string? Next; }
    private static readonly ConditionalWeakTable<CatalogCard, Pending> Plays = new();
    internal static void AfterPlay(CatalogCard card, string next) => Plays.GetOrCreateValue(card).Next = next;

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    private static class FinishPlay
    {
        private static void Prefix(CardModel __instance)
        { if (__instance is CatalogCard { HasAlternateForm: true } card) Plays.GetOrCreateValue(card).Depth++; }
        private static void Postfix(CardModel __instance, ref Task __result)
        { if (__instance is CatalogCard { HasAlternateForm: true } card) __result = Complete(__result, card); }
        private static async Task Complete(Task play, CatalogCard card)
        {
            var pending = Plays.GetOrCreateValue(card);
            try { await play; }
            finally
            {
                if (--pending.Depth == 0)
                {
                    string? next = pending.Next;
                    Plays.Remove(card);
                    if (next != null && card.Pile != null) card.ChangeForm(next);
                }
            }
        }
    }

    [HarmonyPatch]
    private static class FormLocalization
    {
        private static IEnumerable<MethodBase> TargetMethods() => new[] { nameof(CardModel.TitleLocString), nameof(CardModel.Description) }
            .Select(name => AccessTools.PropertyGetter(typeof(CardModel), name));
        private static bool Prefix(CardModel __instance, MethodBase __originalMethod, ref LocString __result)
        {
            if (__instance is not CatalogCard { HasChangedForm: true } card) return true;
            __result = new LocString("cards", card.Definition.ModelId + (__originalMethod.Name == "get_Description" ? ".description" : ".title"));
            return false;
        }
    }
}
