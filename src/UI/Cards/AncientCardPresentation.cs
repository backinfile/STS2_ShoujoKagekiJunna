using Godot;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

internal static class AncientCardPresentation
{
    private sealed record Portrait(TextureRect Node, Texture2D? Texture, TextureRect.StretchModeEnum Mode);
    private static readonly ConditionalWeakTable<NCard, Portrait> Portraits = new();

    internal static void Restore(NCard node)
    {
        if (!Portraits.TryGetValue(node, out var original)) return;
        Portraits.Remove(node);
        if (!GodotObject.IsInstanceValid(original.Node)) return;
        original.Node.StretchMode = original.Mode;
        original.Node.Texture = original.Texture;
    }

    internal static void Update(NCard node)
    {
        if (node.Model is JunnaCard { Rarity: CardRarity.Ancient } card &&
            node.GetNodeOrNull<TextureRect>("%AncientPortrait") is { } portrait)
        {
            Portraits.AddOrUpdate(node, new(portrait, portrait.Texture, portrait.StretchMode));
            portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            if (card.AncientPortraitFocusX is { } focusX)
            {
                var size = card.Portrait.GetSize();
                float width = Math.Min(size.X, size.Y * portrait.Size.X / portrait.Size.Y);
                float left = Math.Clamp(size.X * focusX - width * .5f, 0, size.X - width);
                portrait.Texture = new AtlasTexture { Atlas = card.Portrait, Region = new Rect2(left, 0, width, size.Y) };
            }
        }
    }
}
