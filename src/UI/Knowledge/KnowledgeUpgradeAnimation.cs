using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

internal static class KnowledgeUpgradeAnimation
{
    private static readonly ConditionalWeakTable<CardModel, Task> Animations = new();
    private static readonly ConditionalWeakTable<CardModel, object> Smithed = new();
    private static readonly AsyncLocal<bool> Smithing = new();

    private static Task Completion(Godot.Node vfx)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vfx.TreeExited += () => completion.TrySetResult();
        return completion.Task;
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
            var completed = Completion(vfx);
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
                Animations.AddOrUpdate(card, Completion(__result));
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
