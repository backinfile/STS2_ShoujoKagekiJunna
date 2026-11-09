using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;

namespace ShoujoKagekiJunna.Models;

[HarmonyPatch(typeof(TheArchitect), nameof(TheArchitect.DialogueSet), MethodType.Getter)]
internal static class JunnaArchitectDialogue
{
    private static void Postfix(AncientDialogueSet __result)
    {
        // Alias after native localization keys are populated, keeping the placeholder's dialogue text.
        __result.CharacterDialogues.TryAdd(ModelDb.Character<Junna>().Id.Entry,
            __result.CharacterDialogues[ModelDb.Character<Silent>().Id.Entry]);
    }
}
