using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
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
    private static readonly ConditionalWeakTable<CardModel, Task<bool>> Animations = new();
    private static readonly ConditionalWeakTable<NCardUpgradeVfx, TaskCompletionSource<bool>> UpgradeCompletions = new();
    private static readonly ConditionalWeakTable<SmithRestSiteOption, SmithPresentation> Presentations = new();
    private static readonly AsyncLocal<SmithRestSiteOption?> Selecting = new();
    private static readonly AsyncLocal<SmithRestSiteOption?> Playing = new();

    private static Task SparksCompletion(NCardSmithVfx vfx) => Task.WhenAll(
        vfx.FindChildren("*", "GPUParticles2D", true, false).OfType<Godot.GpuParticles2D>()
            .Where(particle => particle.OneShot).Select(particle =>
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                particle.Finished += () => completion.TrySetResult();
                vfx.TreeExited += () => completion.TrySetResult();
                return completion.Task;
            }));

    internal static bool DeferPreview(CardModel card, IReadOnlyList<CardPileAddResult> added)
    {
        if (added.Count == 0 || !LocalContext.IsMine(card)) return false;
        if (Selecting.Value is { } option)
        {
            Presentations.GetValue(option, _ => new()).Added.AddRange(added);
            return true;
        }
        if (!Animations.TryGetValue(card, out var animation)) return false;
        Animations.Remove(card);
        TaskHelper.RunSafely(PreviewUpgradeAfter(animation, card, added));
        return true;
    }

    private static async Task PreviewUpgradeAfter(Task<bool> animation, CardModel card, IReadOnlyList<CardPileAddResult> added)
    {
        var run = MegaCrit.Sts2.Core.Nodes.NRun.Instance;
        var room = card.Owner.RunState.CurrentRoom;
        if (await animation && Godot.GodotObject.IsInstanceValid(run)
            && run == MegaCrit.Sts2.Core.Nodes.NRun.Instance && card.Owner.RunState.CurrentRoom == room)
            await CardActions.PreviewAdded(added, PileType.Deck);
    }

    private static async Task PreviewAfter(Task effect, IReadOnlyList<CardPileAddResult> added, CancellationToken token)
    {
        await effect;
        if (!token.IsCancellationRequested) await CardActions.PreviewAdded(added, PileType.Deck);
    }

    [HarmonyPatch(typeof(NCardUpgradeVfx), nameof(NCardUpgradeVfx.Create))]
    private static class UpgradePreview
    {
        private static void Postfix(CardModel card, NCardUpgradeVfx? __result)
        {
            if (CardCatalog.DesignId(card) != "JK82" || __result == null) return;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            __result.TreeExited += () => completion.TrySetResult(false);
            UpgradeCompletions.Add(__result, completion);
            Animations.AddOrUpdate(card, completion.Task);
        }
    }

    [HarmonyPatch(typeof(NCardUpgradeVfx), "PlayAnimation")]
    private static class NativeUpgradeCompletion
    {
        private static void Postfix(NCardUpgradeVfx __instance, ref Task __result)
        {
            if (UpgradeCompletions.TryGetValue(__instance, out var completion))
                __result = Complete(__result, __instance, completion);
        }

        private static async Task Complete(Task animation, NCardUpgradeVfx node, TaskCompletionSource<bool> completion)
        {
            try
            {
                await animation;
                completion.TrySetResult(Godot.GodotObject.IsInstanceValid(node) && node.IsInsideTree());
            }
            finally { completion.TrySetResult(false); }
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
        private static void Postfix(SmithRestSiteOption __instance, CancellationToken ct, SmithRestSiteOption? __state)
        {
            Playing.Value = __state;
            if (!Presentations.TryGetValue(__instance, out var presentation)) return;
            Presentations.Remove(__instance);
            TaskHelper.RunSafely(PreviewAfter(Task.WhenAll(presentation.Effects), presentation.Added, ct));
        }
        private static void Finalizer(SmithRestSiteOption? __state) => Playing.Value = __state;
    }
}
