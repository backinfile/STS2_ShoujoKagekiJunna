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
    private static bool _confirming;

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
        if (_confirming || card == null || card.Pile != null || KnowledgeCombat.Group(card) == null) return true;
        if (!Pending(__instance)) return false;
        KnowledgeGroupPreview.OpenForSelection(card, () =>
        {
            if (!GodotObject.IsInstanceValid(__instance) || !__instance.IsInsideTree() ||
                !GodotObject.IsInstanceValid(cardHolder) || cardHolder.CardModel != card || !Pending(__instance)) return;
            _confirming = true;
            try
            {
                string method = __instance is NCardRewardSelectionScreen ? "SelectCard" : "SelectHolder";
                AccessTools.Method(__instance.GetType(), method).Invoke(__instance, [cardHolder]);
            }
            finally { _confirming = false; }
        });
        return false;
    }
}
