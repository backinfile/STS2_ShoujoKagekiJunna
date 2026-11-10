using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class AncientCardPresentation
{
    private static void Postfix(NCard __instance)
    {
        if (__instance.Model is JunnaCard { Rarity: CardRarity.Ancient } card &&
            __instance.GetNodeOrNull<TextureRect>("%AncientPortrait") is { } portrait)
        {
            portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            if (card.DesignId == "JZ03")
            {
                var size = card.Portrait.GetSize();
                float width = Math.Min(size.X, size.Y * portrait.Size.X / portrait.Size.Y);
                float left = Math.Clamp(size.X * .35f - width * .5f, 0, size.X - width);
                portrait.Texture = new AtlasTexture { Atlas = card.Portrait, Region = new Rect2(left, 0, width, size.Y) };
            }
        }
    }
}
