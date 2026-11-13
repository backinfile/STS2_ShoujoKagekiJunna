#if STS2_BETA
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna;

[HarmonyPatch(typeof(Fasten), "get_ExtraHoverTips")]
internal static class FastenHoverTipPatch
{
    private static bool Prefix(Fasten __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!__instance.IsMutable || __instance.Owner?.Character is not Junna) return true;
        // Native Fasten searches the reward pool for a Defend. Junna's reward filter
        // excludes starter cards, so use her canonical Defend for the same preview.
        __result = [HoverTipFactory.Static(StaticHoverTip.Block), HoverTipFactory.FromCard(ModelDb.Card<JunnaDefend>())];
        return false;
    }
}
#endif
