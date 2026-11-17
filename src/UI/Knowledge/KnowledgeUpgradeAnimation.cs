using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

internal static class KnowledgeUpgradeAnimation
{
    private static readonly ConditionalWeakTable<CardModel, Task> Animations = new();
    private static readonly ConditionalWeakTable<CardModel, TaskCompletionSource> Finishing = new();
    private static readonly ConditionalWeakTable<CardModel, object> Smithed = new();
    private static readonly AsyncLocal<bool> Smithing = new();

    private static Task Completion(CardModel card, Godot.Node vfx)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Finishing.AddOrUpdate(card, completion);
        vfx.TreeExited += () =>
        {
            if (Finishing.TryGetValue(card, out var pending) && ReferenceEquals(pending, completion)) Finishing.Remove(card);
            completion.TrySetResult();
        };
        return completion.Task;
    }

    private static Task SparksCompletion(NCardSmithVfx vfx)
    {
        var completions = vfx.FindChildren("*", "GPUParticles2D", true, false)
            .OfType<Godot.GpuParticles2D>().Where(particle => particle.OneShot).Select(particle =>
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                particle.Finished += () => completion.TrySetResult();
                vfx.TreeExited += () => completion.TrySetResult();
                return completion.Task;
            });
        return Task.WhenAll(completions);
    }

    internal static async Task WaitFor(CardModel card)
    {
        if (Animations.TryGetValue(card, out var animation))
        {
            Animations.Remove(card);
            await animation;
        }
        else if (Smithing.Value && LocalContext.IsMine(card) && NRun.Instance?.GlobalUi is { } ui)
        {
            // Native smithing normally plays after OnSelect; its upgrade reward must follow that VFX.
            var vfx = NCardSmithVfx.Create([card]);
            if (vfx == null) return;
            var completed = Task.WhenAll(Completion(card, vfx), SparksCompletion(vfx));
            Smithed.AddOrUpdate(card, new object());
            ui.CardPreviewContainer.AddChildSafely(vfx);
            await completed;
        }
    }

    [HarmonyPatch(typeof(NCardUpgradeVfx), nameof(NCardUpgradeVfx.Create))]
    private static class UpgradePreview
    {
        private static void Postfix(CardModel card, NCardUpgradeVfx? __result)
        {
            if (CardCatalog.DesignId(card) == "JK82" && __result != null)
                Animations.AddOrUpdate(card, Completion(card, __result));
        }
    }

    // The upgrade effect has finished when its card starts flying back to the pile.
    [HarmonyPatch(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create),
        [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)])]
    private static class UpgradeFlight
    {
        private static void Postfix(NCard card, bool isAddingToPile)
        {
            if (isAddingToPile || card.Model is not { } model || !Finishing.TryGetValue(model, out var completion)) return;
            Finishing.Remove(model);
            completion.TrySetResult();
        }
    }

    [HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.OnSelect))]
    private static class SmithSelection
    {
        private static void Prefix(out bool __state) { __state = Smithing.Value; Smithing.Value = true; }
        private static void Postfix(bool __state) => Smithing.Value = __state;
        private static void Finalizer(bool __state) => Smithing.Value = __state;
    }

    [HarmonyPatch(typeof(NCardSmithVfx), nameof(NCardSmithVfx.Create), [typeof(IEnumerable<CardModel>), typeof(bool)])]
    private static class NativeSmithPreview
    {
        private static bool Prefix(ref IEnumerable<CardModel> cards)
        {
            cards = cards.Where(card => !Smithed.Remove(card)).ToArray();
            return cards.Any();
        }
    }
}
