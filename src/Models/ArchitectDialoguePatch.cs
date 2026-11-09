using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models.Events;

namespace ShoujoKagekiJunna.Models;

[HarmonyPatch(typeof(TheArchitect), "WinRun")]
internal static class ArchitectDialoguePatch
{
    [HarmonyPrefix]
    private static void Prefix(TheArchitect __instance, ref AncientDialogue? ____dialogue)
    {
        // Native options permit a missing dialogue, but WinRun reads its EndAttackers.
        if (__instance.Owner?.Character is Junna && ____dialogue == null)
            ____dialogue = new AncientDialogue("");
    }
}
