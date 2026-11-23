using System.Reflection;
using Godot;
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

    static bool Prefix(NMerchantCharacter __instance)
    {
        if (__instance.GetChildCount() == 0) return true;
        // BaseLib can keep the original scene as a child of the converted merchant.
        var first = __instance.GetChild(0);
        var visuals = first as Sprite2D ?? first.GetNodeOrNull<Sprite2D>("Visuals");
        return visuals?.Texture?.ResourcePath != JunnaMod.ResourceRoot + "images/merchant_glasses.png";
    }
}
