using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.UI;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class InvestmentHoverTipPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (InvestmentTip(__instance) is { } tip) __result = __result.Append(tip);
    }

    private static HoverTip? InvestmentTip(CardModel card)
    {
        var snapshot = DamageModifiers.CaptureDamageModifierSnapshot(card);
        int hits = DamageModifiers.ExtraHits(snapshot);
        if (snapshot.DamageBonus == 0 && hits == 0) return null;
        var lines = new List<string>();
        if (snapshot.DamageBonus != 0)
        {
            var text = new LocString("gameplay_ui", "JUNNA.INVESTMENT_BONUS");
            text.Add("Amount", snapshot.DamageBonus);
            lines.Add(text.GetFormattedText());
        }
        if (hits != 0)
        {
            var text = new LocString("gameplay_ui", "JUNNA.INVESTMENT_HITS");
            text.Add("Amount", hits);
            lines.Add(text.GetFormattedText());
        }
        return new HoverTip(new LocString("gameplay_ui", "JUNNA.INVESTMENT"), string.Join("\n", lines)) { Id = "junna_investment" };
    }
}
