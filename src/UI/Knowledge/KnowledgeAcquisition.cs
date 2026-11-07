using System.Reflection;
using Godot;
using HarmonyLib;
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

    private static bool Pending(Control screen)
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
