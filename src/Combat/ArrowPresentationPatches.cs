using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Combat;

[HarmonyPatch(typeof(CardTypeExtensions), nameof(CardTypeExtensions.ToLocString))]
internal static class ArrowTypeNamePatch
{
    static bool Prefix(CardType cardType, ref LocString __result)
    {
        if (cardType != ArrowPile.ArrowType) return true;
        __result = new LocString("gameplay_ui", "JUNNA.CARD_TYPE.ARROW");
        return false;
    }
}

[HarmonyPatch]
internal static class ArrowFramePatch
{
    static IEnumerable<MethodBase> TargetMethods() => new[] { "FramePath", "PortraitBorderPath", "AncientTextBgPath" }
        .Select(name => AccessTools.PropertyGetter(typeof(CardModel), name));
    static bool Prefix(CardModel __instance, MethodBase __originalMethod, ref string __result)
    {
        if (__instance.Type != ArrowPile.ArrowType) return true;
        __result = __originalMethod.Name switch
        {
            "get_FramePath" => "res://images/atlases/ui_atlas.sprites/card/card_frame_skill_s.tres",
            "get_PortraitBorderPath" => "res://images/atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres",
            _ => "res://images/atlases/compressed_atlas.sprites/ancient_text_bg_skill.png.tres"
        };
        return false;
    }
}
