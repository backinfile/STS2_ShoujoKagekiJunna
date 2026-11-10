using Godot;
using ShoujoKageki.Core.V1.Presentation;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    internal void DrawGuides(ArrowGuides guides)
    {
        // Draw behind the portrait children. Dead or removed targets have no guide.
        foreach (var (arrow, view) in _views)
        {
            if (view.Releasing) continue;
            var targets = view.Targets;
            if (targets.Count == 0) continue;
            var tip = LocalPoint(view.Frame.GetGlobalTransform() * Tip);
            var start = _deployments.TryGetValue(arrow, out var deployment)
                ? deployment.CurrentPosition.Lerp(tip, deployment.ArrowAlpha) : tip;
            float guideProgress = deployment?.DrawProgress ?? 1;
            if (guideProgress <= 0) continue;
            if (arrow.TargetsGroup)
            {
                var left = targets[0] + new Vector2(-55, 25);
                var right = targets[^1] + new Vector2(55, 25);
                var midpoint = (left + right) * .5f;
                left = midpoint + (left - midpoint) * 1.25f;
                right = midpoint + (right - midpoint) * 1.25f;
                left = start.Lerp(left, guideProgress);
                right = start.Lerp(right, guideProgress);
                var fanStart = start.MoveToward(midpoint, 18 * guideProgress);
                guides.DrawFan(fanStart, left, right);
                guides.DrawGroupGuide(fanStart, left, boundary: true);
                guides.DrawGroupGuide(fanStart, right, boundary: true);
                foreach (var end in targets)
                    guides.DrawGroupGuide(fanStart, start.Lerp(end, guideProgress), boundary: false);
            }
            else
            {
                var end = start.Lerp(targets[0], guideProgress);
                guides.DrawSingleGuide(start, end);
            }
        }
    }
}

/// <summary>Batched decorative geometry, independently budgeted from arrow information.</summary>
internal partial class ArrowGuides : Node2D
{
    internal ArrowHud Hud { get; init; } = null!;
    private static ArrayMesh? _fanMesh, _groupMesh, _singleMesh;
    private MeshBatch _fans = null!, _groups = null!, _singles = null!;

    public override void _Ready()
    {
        _fans = new(this, "Fans", _fanMesh ??= CreateFan());
        _groups = new(this, "GroupLines", _groupMesh ??= CreateLine([6, 3, 1.5f], [.18f, .28f, .54f], true));
        _singles = new(this, "SingleLines", _singleMesh ??= CreateLine([6, 1.3f], [.08f, .42f], false));
        CoreEffects.Register(this);
    }

    public override void _Draw()
    {
        _fans.Reset();
        _groups.Reset();
        _singles.Reset();
        Hud.DrawGuides(this);
        _fans.Commit();
        _groups.Commit();
        _singles.Commit();
    }

    internal void DrawSingleGuide(Vector2 start, Vector2 target)
        => _singles.Add(LineTransform(start, target), 1);
    internal void DrawGroupGuide(Vector2 start, Vector2 target, bool boundary)
        => _groups.Add(LineTransform(start, target), boundary ? .2f : .13f);

    private static Transform2D LineTransform(Vector2 start, Vector2 target)
    {
        var axis = target - start;
        return new(axis * .7f, axis.Normalized().Orthogonal(), start);
    }

    internal void DrawFan(Vector2 start, Vector2 left, Vector2 right)
    {
        var leftAxis = left - start;
        var rightAxis = right - start;
        if (Mathf.Abs(leftAxis.Cross(rightAxis)) >= 64) _fans.Add(new(leftAxis, rightAxis, start), 1);
    }

    private static float GroupFade(float progress)
        => Mathf.SmoothStep(0, .18f, progress) * Mathf.Pow(1 - progress, 1.6f);

    private static ArrayMesh CreateLine(float[] widths, float[] alphas, bool fadeStart)
    {
        const int segments = 16, bands = 4, layerSize = (segments + 1) * bands;
        var vertices = new Vector3[widths.Length * layerSize];
        var colors = new Color[vertices.Length];
        var indices = new List<int>();
        for (int layer = 0; layer < widths.Length; layer++)
        for (int row = 0; row <= segments; row++)
        for (int band = 0; band < bands; band++)
        {
            float progress = (float)row / segments;
            float half = widths[layer] * .5f;
            float y = band switch { 0 => -half - .5f, 1 => -half + .5f, 2 => half - .5f, _ => half + .5f };
            int index = layer * layerSize + row * bands + band;
            vertices[index] = new(progress, y, 0);
            float fade = fadeStart ? GroupFade(progress) : Mathf.Pow(1 - progress, 1.6f);
            colors[index] = new Color(.6f, .86f, 1, band is 0 or 3 ? 0 : alphas[layer] * fade);
            if (row > 0 && band < bands - 1)
                indices.AddRange([index - bands, index - bands + 1, index + 1, index - bands, index + 1, index]);
        }
        return CreateMesh(vertices, colors, indices.ToArray());
    }

    private static ArrayMesh CreateFan()
    {
        // Unit barycentric coordinates let every arrow share one immutable, feathered mesh.
        const int segments = 10, bands = 4;
        float[] across = [0, .22f, .78f, 1], edgeAlpha = [0, 1, 1, 0];
        var vertices = new Vector3[(segments + 1) * bands];
        var colors = new Color[vertices.Length];
        var indices = new List<int>();
        for (int row = 0; row <= segments; row++)
        for (int band = 0; band < bands; band++)
        {
            float progress = (float)row / segments;
            int index = row * bands + band;
            vertices[index] = new(progress * .7f * (1 - across[band]), progress * .7f * across[band], 0);
            colors[index] = new Color(.48f, .8f, .95f, .045f * GroupFade(progress) * edgeAlpha[band]);
            if (row == 0 || band == bands - 1) continue;
            int a = index - bands, b = a + 1, c = index + 1, d = index;
            if (row == 1) indices.AddRange([a, c, d]);
            else indices.AddRange([d, a, b, b, c, d]);
        }
        return CreateMesh(vertices, colors, indices.ToArray());
    }

    private static ArrayMesh CreateMesh(Vector3[] vertices, Color[] colors, int[] indices)
    {
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private sealed class MeshBatch
    {
        private const int Stride = 12;
        private readonly MultiMesh _mesh;
        private float[] _buffer = [];
        private int _count;

        internal MeshBatch(Node owner, string name, Mesh geometry)
        {
            _mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, UseColors = true, Mesh = geometry };
            owner.AddChild(new MultiMeshInstance2D { Name = name, Multimesh = _mesh });
        }
        internal void Reset() => _count = 0;
        internal void Add(Transform2D transform, float alpha)
        {
            int offset = _count++ * Stride;
            if (_count > _buffer.Length / Stride)
            {
                int capacity = Math.Max(4, _buffer.Length / Stride * 2);
                Array.Resize(ref _buffer, capacity * Stride);
                _mesh.InstanceCount = capacity;
            }
            // Godot's 2D instance buffer stores two padded matrix rows, then RGBA.
            _buffer[offset] = transform.X.X; _buffer[offset + 1] = transform.Y.X;
            _buffer[offset + 3] = transform.Origin.X;
            _buffer[offset + 4] = transform.X.Y; _buffer[offset + 5] = transform.Y.Y;
            _buffer[offset + 7] = transform.Origin.Y;
            _buffer[offset + 8] = _buffer[offset + 9] = _buffer[offset + 10] = 1;
            _buffer[offset + 11] = alpha;
        }
        internal void Commit()
        {
            _mesh.VisibleInstanceCount = _count;
            if (_count > 0) RenderingServer.MultimeshSetBuffer(_mesh.GetRid(), _buffer);
        }
    }
}
