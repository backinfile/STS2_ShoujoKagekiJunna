using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

internal static class KnowledgeUpgradeAnimation
{
    private sealed class SmithPresentation
    {
        internal readonly List<CardPileAddResult> Added = [];
        internal readonly List<Task> Effects = [];
    }
    private static readonly ConditionalWeakTable<CardModel, Task> Animations = new();
    private static readonly ConditionalWeakTable<SmithRestSiteOption, SmithPresentation> Presentations = new();
    private static readonly AsyncLocal<SmithRestSiteOption?> Selecting = new();
    private static readonly AsyncLocal<SmithRestSiteOption?> Playing = new();

    private static Task Exit(Godot.Node vfx)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vfx.TreeExited += () => completion.TrySetResult();
        return completion.Task;
    }

    private static Task SparksCompletion(NCardSmithVfx vfx) => Task.WhenAll(
        vfx.FindChildren("*", "GPUParticles2D", true, false).OfType<Godot.GpuParticles2D>()
            .Where(particle => particle.OneShot).Select(particle =>
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                particle.Finished += () => completion.TrySetResult();
                vfx.TreeExited += () => completion.TrySetResult();
                return completion.Task;
            }));

    internal static Task WaitFor(CardModel card)
    {
        if (!Animations.TryGetValue(card, out var animation)) return Task.CompletedTask;
        Animations.Remove(card);
        return animation;
    }

    internal static bool DeferSmithPreview(IReadOnlyList<CardPileAddResult> added)
    {
        if (Selecting.Value is not { } option || added.Count == 0 || !LocalContext.IsMine(added[0].cardAdded)) return false;
        Presentations.GetValue(option, _ => new()).Added.AddRange(added);
        return true;
    }

    [HarmonyPatch(typeof(NCardUpgradeVfx), nameof(NCardUpgradeVfx.Create))]
    private static class UpgradePreview
    {
        private static void Postfix(CardModel card, NCardUpgradeVfx? __result)
        {
            if (CardCatalog.DesignId(card) == "JK82" && __result != null)
                Animations.AddOrUpdate(card, Exit(__result));
        }
    }

    [HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.OnSelect))]
    private static class SmithSelection
    {
        private static void Prefix(SmithRestSiteOption __instance, out SmithRestSiteOption? __state)
        { __state = Selecting.Value; Selecting.Value = __instance; }
        private static void Postfix(SmithRestSiteOption? __state) => Selecting.Value = __state;
        private static void Finalizer(SmithRestSiteOption? __state) => Selecting.Value = __state;
    }

    // Observe the VFX created by the native post-selection method without replacing or filtering it.
    [HarmonyPatch(typeof(NCardSmithVfx), nameof(NCardSmithVfx.Create), [typeof(IEnumerable<CardModel>), typeof(bool)])]
    private static class NativeSmithPreview
    {
        private static void Postfix(NCardSmithVfx? __result)
        {
            if (__result != null && Playing.Value is { } option && Presentations.TryGetValue(option, out var presentation))
                presentation.Effects.Add(SparksCompletion(__result));
        }
    }

    [HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.DoLocalPostSelectVfx))]
    private static class SmithPresentationCompletion
    {
        private static void Prefix(SmithRestSiteOption __instance, out SmithRestSiteOption? __state)
        { __state = Playing.Value; Playing.Value = __instance; }
        private static void Postfix(SmithRestSiteOption __instance, ref Task __result, SmithRestSiteOption? __state)
        {
            __result = Complete(__result, __instance);
            Playing.Value = __state;
        }
        private static void Finalizer(SmithRestSiteOption? __state) => Playing.Value = __state;
        private static async Task Complete(Task task, SmithRestSiteOption option)
        {
            try
            {
                await task;
                if (!Presentations.TryGetValue(option, out var presentation)) return;
                await Task.WhenAll(presentation.Effects);
                await CardActions.PreviewAdded(presentation.Added, PileType.Deck);
            }
            finally { Presentations.Remove(option); }
        }
    }
}
