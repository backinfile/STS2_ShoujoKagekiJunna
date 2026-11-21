using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace ShoujoKagekiJunna.UI;

[HarmonyPatch]
internal static class JunnaMerchantPresentation
{
    static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(NMerchantCharacter), nameof(NMerchantCharacter._Ready)),
        AccessTools.Method(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))
    ];

    // This scene uses a static sprite rather than the native Spine animation rig.
    static bool Prefix(NMerchantCharacter __instance) =>
        __instance.SceneFilePath != JunnaMod.ResourceRoot + "scenes/junna_merchant.tscn"
        || __instance.GetChildCount() == 0
        || __instance.GetChild(0) is not Godot.Sprite2D;
}
