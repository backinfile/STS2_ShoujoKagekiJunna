using System.Runtime.CompilerServices;
using Godot;

namespace ShoujoKagekiJunna.Combat;

internal static class ArrowPortraitTexture
{
    private static readonly ConditionalWeakTable<AtlasTexture, ImageTexture> Textures = new();

    internal static Texture2D Resolve(Texture2D texture) => texture is AtlasTexture atlas
        ? Textures.GetValue(atlas, CreateTexture) : texture;

    private static ImageTexture CreateTexture(AtlasTexture atlas)
    {
        using var image = ReadImage(atlas);
        return ImageTexture.CreateFromImage(image);
    }

    private static Image ReadImage(Texture2D texture)
    {
        if (texture is AtlasTexture atlas)
        {
            using var source = ReadImage(atlas.Atlas);
            source.Convert(Image.Format.Rgba8);
            var region = new Rect2I((Vector2I)atlas.Region.Position, (Vector2I)atlas.Region.Size);
            if (region.Size.X == 0) region.Size = new Vector2I(source.GetWidth(), region.Size.Y);
            if (region.Size.Y == 0) region.Size = new Vector2I(region.Size.X, source.GetHeight());
            var size = (Vector2I)atlas.GetSize();
            var image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
            image.BlitRect(source, region, (Vector2I)atlas.Margin.Position);
            return image;
        }
        var result = texture.GetImage();
        // Compressed game atlases must be decompressed before their regions can be copied.
        if (result.IsCompressed()) result.Decompress();
        return result;
    }
}
