using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.UI;

[HarmonyPatch]
internal static class KnowledgeAcquisition
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCardRewardSelectionScreen), "InspectCard");
        yield return AccessTools.Method(typeof(NChooseACardSelectionScreen), "OpenPreviewScreen");
    }

    internal static bool Pending(Control screen)
    {
        var completion = AccessTools.Field(screen.GetType(), "_completionSource").GetValue(screen);
        return completion switch
        {
            TaskCompletionSource<int?> reward => !reward.Task.IsCompleted,
            TaskCompletionSource<IEnumerable<CardModel>> choice => !choice.Task.IsCompleted,
            _ => false
        };
    }

    private static bool Prefix(Control __instance, NCardHolder cardHolder)
    {
        var card = cardHolder.CardModel;
        if (card == null || card.Pile != null || !KnowledgeCombat.HasGroup(card)) return true;
        if (!Pending(__instance)) return false;
        KnowledgeGroupPreview.OpenForSelection(card);
        return false;
    }
}

[HarmonyPatch(typeof(NCardGridSelectionScreen), "ShowCardDetail")]
internal static class KnowledgeGridAcquisition
{
    private static bool Prefix(NCardGridSelectionScreen __instance, CardModel card)
    {
        if (__instance is not NSimpleCardSelectScreen || card.Pile != null || !KnowledgeCombat.HasGroup(card)) return true;
        if (KnowledgeAcquisition.Pending(__instance)) KnowledgeGroupPreview.OpenForSelection(card);
        return false;
    }
}

/// <summary>Preview a permanent knowledge gain once, including its entrance card.</summary>
internal static class KnowledgeAcquisitionPresentation
{
    private static readonly ConditionalWeakTable<CardModel, object> Presented = new();
    private static readonly AsyncLocal<bool> ShowingGroup = new();

    internal static Task Show(IReadOnlyList<CardPileAddResult> results)
    {
        var source = results[0].cardAdded;
        if (LocalContext.IsMine(source)) Presented.AddOrUpdate(source, new object());
        bool previous = ShowingGroup.Value;
        try
        {
            ShowingGroup.Value = true;
            CardCmd.PreviewCardPileAdd(results,
                style: results.Count > 5 ? CardPreviewStyle.MessyLayout : CardPreviewStyle.HorizontalLayout);
        }
        finally { ShowingGroup.Value = previous; }
        return Task.CompletedTask;
    }

    private static bool AlreadyPresented(CardPileAddResult result) => !ShowingGroup.Value && result.oldPile == null
        && result.cardAdded.Pile?.Type == PileType.Deck && Presented.Remove(result.cardAdded);

    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.PreviewCardPileAdd),
        [typeof(CardPileAddResult), typeof(float), typeof(CardPreviewStyle)])]
    private static class SinglePreview
    {
        private static bool Prefix(CardPileAddResult result) => !AlreadyPresented(result);
    }

    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.PreviewCardPileAdd),
        [typeof(IReadOnlyList<CardPileAddResult>), typeof(float), typeof(CardPreviewStyle)])]
    private static class BatchPreview
    {
        private static void Prefix(ref IReadOnlyList<CardPileAddResult> results)
        {
            if (!ShowingGroup.Value) results = results.Where(result => !AlreadyPresented(result)).ToArray();
        }
    }

    [HarmonyPatch(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create),
        [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)])]
    private static class OriginalCardFlight
    {
        private static bool Prefix(NCard card, PileType pileType, bool isAddingToPile, ref NCardFlyVfx? __result)
        {
            if (pileType != PileType.Deck || !isAddingToPile || card.Model is not { } model
                || !Presented.TryGetValue(model, out _)) return true;
            // Native preview cards fly together; reward/shop nodes have already been represented there.
            var ui = MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi;
            var parent = card.GetParent();
            if (ui == null || parent == ui.CardPreviewContainer || parent == ui.MessyCardPreviewContainer
                || parent == ui.EventCardPreviewContainer || parent == ui.GridCardPreviewContainer) return true;
            Presented.Remove(model);
            card.QueueFreeSafely();
            __result = null;
            return false;
        }
    }
}
