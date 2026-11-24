using Godot;

namespace ShoujoKagekiJunna.Combat;

internal readonly record struct ArrowAppearance(Texture2D Portrait, Texture2D Frame, Rect2 Crop);

/// <summary>Draws the portrait and frame of every waiting arrow in scene-tree order.</summary>
internal sealed partial class ArrowBodyBatches : Node2D
{
    // Rasterize at 2x HUD size and leave a transparent gutter between atlas entries.
    private static readonly Vector2I TileSize = new(84, 146);
    private static readonly Vector2I CellSize = TileSize + new Vector2I(2, 2);
    private const int Columns = 8, Rows = 4;
    private const int NewTilesPerFrame = 4;
    private static readonly Vector2I PageSize = new(Columns * CellSize.X, Rows * CellSize.Y);
    private readonly record struct Tile(Page Page, Rect2 Uv);

    private readonly Dictionary<ArrowAppearance, Tile?> _tiles = [];
    private readonly List<Page> _pages = [];
    private readonly List<Batch> _batches = [];
    private ShaderMaterial _material = null!;
    private ArrayMesh _geometry = null!;
    private int _batchCount;
    private int _tileBudget;

    internal void Initialize(Vector2 size, Vector2 pivot)
    {
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[]
        {
            new(-pivot.X, -pivot.Y, 0), new(size.X - pivot.X, -pivot.Y, 0),
            new(size.X - pivot.X, size.Y - pivot.Y, 0), new(-pivot.X, size.Y - pivot.Y, 0)
        };
        arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        _geometry = new ArrayMesh();
        _geometry.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                    shader_type canvas_item;
                    render_mode unshaded;
                    void vertex() {
                        UV = INSTANCE_CUSTOM.xy + UV * INSTANCE_CUSTOM.zw;
                    }
                    """
            }
        };
    }

    internal void Begin()
    {
        _batchCount = 0;
        _tileBudget = NewTilesPerFrame;
    }

    internal bool Add(ArrowAppearance appearance, Transform2D pose)
    {
        if (!_tiles.TryGetValue(appearance, out var tile))
        {
            // Native bodies remain visible while uncommon portraits wait for atlas space.
            if (_tileBudget == 0) return false;
            _tileBudget--;
            tile = CreateTile(appearance);
            _tiles.Add(appearance, tile);
        }
        if (tile is not { } sprite) return false;
        if (_batchCount == 0 || _batches[_batchCount - 1].Page != sprite.Page)
        {
            // Adjacent runs retain the paint order across atlas pages.
            if (_batchCount == _batches.Count) _batches.Add(new(this, _geometry, _material));
            _batches[_batchCount++].Begin(sprite.Page);
        }
        _batches[_batchCount - 1].Add(pose, sprite.Uv);
        return true;
    }

    internal void Commit()
    {
        foreach (var page in _pages) page.Upload();
        for (int i = 0; i < _batches.Count; i++) _batches[i].Commit(i < _batchCount);
    }

    private Tile? CreateTile(ArrowAppearance appearance)
    {
        using var source = appearance.Portrait.GetImage();
        using var frame = appearance.Frame.GetImage();
        if (source == null || frame == null || source.IsEmpty() || frame.IsEmpty()) return null;
        if (source.IsCompressed() && source.Decompress() != Error.Ok) return null;
        if (frame.IsCompressed() && frame.Decompress() != Error.Ok) return null;

        var offset = (Vector2I)appearance.Crop.Position.Round();
        var size = (Vector2I)appearance.Crop.Size.Round();
        var bounds = new Rect2I(Vector2I.Zero, source.GetSize());
        var region = bounds.Intersection(new(offset, new(Math.Max(1, size.X), Math.Max(1, size.Y))));
        if (!region.HasArea()) return null;
        using var cropped = source.GetRegion(region);
        cropped.Resize(TileSize.X, TileSize.Y, Image.Interpolation.Bilinear);
        cropped.Convert(Image.Format.Rgba8);
        var pixels = cropped.GetData();
        var window = ArrowHud.PortraitWindow;
        float pixelWidth = ArrowHud.FrameSize.X / TileSize.X;
        for (int y = 0; y < TileSize.Y; y++)
        for (int x = 0; x < TileSize.X; x++)
        {
            var point = new Vector2((x + .5f) * pixelWidth, (y + .5f) * ArrowHud.FrameSize.Y / TileSize.Y);
            float distance = float.PositiveInfinity;
            for (int i = 0; i < window.Length; i++)
            {
                var edge = window[(i + 1) % window.Length] - window[i];
                distance = Math.Min(distance, edge.Cross(point - window[i]) / edge.Length());
            }
            int alpha = (y * TileSize.X + x) * 4 + 3;
            pixels[alpha] = (byte)Mathf.Round(pixels[alpha] * Mathf.Clamp(.5f + distance / pixelWidth, 0, 1));
        }

        using var icon = Image.CreateFromData(TileSize.X, TileSize.Y, false, Image.Format.Rgba8, pixels);
        frame.Resize(TileSize.X, TileSize.Y, Image.Interpolation.Bilinear);
        frame.Convert(Image.Format.Rgba8);
        icon.BlendRect(frame, new(Vector2I.Zero, TileSize), Vector2I.Zero);

        if (_pages.Count == 0 || _pages[^1].Count == Rows * Columns) _pages.Add(new());
        var page = _pages[^1];
        int index = page.Count++;
        var position = new Vector2I(index % Columns * CellSize.X + 1, index / Columns * CellSize.Y + 1);
        page.Image.BlitRect(icon, new(Vector2I.Zero, TileSize), position);
        page.Dirty = true;
        return new(page, new((Vector2)position / PageSize, (Vector2)TileSize / PageSize));
    }

    public override void _ExitTree()
    {
        foreach (var page in _pages) page.Image.Dispose();
    }

    private sealed class Page
    {
        internal readonly Image Image = Image.CreateEmpty(PageSize.X, PageSize.Y, false, Image.Format.Rgba8);
        internal readonly ImageTexture Texture = new();
        internal int Count;
        internal bool Dirty;

        internal void Upload()
        {
            if (!Dirty) return;
            if (Texture.GetWidth() == 0) Texture.SetImage(Image);
            else Texture.Update(Image);
            Dirty = false;
        }
    }

    private sealed class Batch
    {
        private const int Stride = 12;
        private readonly MultiMeshInstance2D _node;
        private readonly MultiMesh _mesh;
        private float[] _buffer = [];
        private int _count;
        internal Page Page { get; private set; } = null!;

        internal Batch(Node owner, Mesh geometry, Material material)
        {
            _mesh = new() { Mesh = geometry, TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, UseCustomData = true };
            _node = new() { Multimesh = _mesh, Material = material };
            owner.AddChild(_node);
        }

        internal void Begin(Page page)
        {
            if (Page != page)
            {
                Page = page;
                _node.Texture = page.Texture;
            }
            _count = 0;
        }

        internal void Add(Transform2D pose, Rect2 uv)
        {
            int offset = _count++ * Stride;
            if (_count > _buffer.Length / Stride)
            {
                int capacity = Math.Max(8, _buffer.Length / Stride * 2);
                Array.Resize(ref _buffer, capacity * Stride);
                _mesh.InstanceCount = capacity;
            }
            _buffer[offset] = pose.X.X;
            _buffer[offset + 1] = pose.Y.X;
            _buffer[offset + 3] = pose.Origin.X;
            _buffer[offset + 4] = pose.X.Y;
            _buffer[offset + 5] = pose.Y.Y;
            _buffer[offset + 7] = pose.Origin.Y;
            _buffer[offset + 8] = uv.Position.X;
            _buffer[offset + 9] = uv.Position.Y;
            _buffer[offset + 10] = uv.Size.X;
            _buffer[offset + 11] = uv.Size.Y;
        }

        internal void Commit(bool active)
        {
            if (!active)
            {
                _node.Hide();
                return;
            }
            _node.Show();
            _mesh.VisibleInstanceCount = _count;
            RenderingServer.MultimeshSetBuffer(_mesh.GetRid(), _buffer);
        }
    }
}
