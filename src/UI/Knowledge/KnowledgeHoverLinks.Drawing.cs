using Godot;

namespace ShoujoKagekiJunna.UI;

internal enum KnowledgeRibbonStyle { Current, Focused, Mist, Close, Breath, Linked }

public partial class KnowledgeHoverLinks
{
    private const int RibbonSegments = 32, RibbonBands = 5;
    internal static KnowledgeRibbonStyle Style { get; set; } = KnowledgeRibbonStyle.Linked;
    private readonly record struct RibbonAppearance(float WidthScale, float TaperEnd, float Reach,
        float Opacity, float FadePower, float ShoulderAlpha);
    private static RibbonAppearance Appearance => Style switch
    {
        KnowledgeRibbonStyle.Linked => new(.6f, .20f, 1, .13f, 5, .28f),
        KnowledgeRibbonStyle.Focused => new(.65f, .22f, .76f, .60f, 1.45f, .28f),
        KnowledgeRibbonStyle.Mist => new(1.65f, .58f, .68f, .26f, 1.35f, .68f),
        KnowledgeRibbonStyle.Close => new(1.20f, .32f, .44f, .46f, 2.20f, .55f),
        _ => new(1, .35f, .72f, .42f, 1.60f, .55f)
    };
    private float Breath => Style == KnowledgeRibbonStyle.Breath ? Mathf.Sin(_age * Mathf.Tau / 2.6f) : 0;
    private readonly ArrayMesh _ribbonMesh = new();
    private readonly GradientTexture2D _receiverGlow = new()
    {
        Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new(.5f, .5f), FillTo = new(1, .5f),
        Gradient = new Gradient
        {
            Offsets = [0, .2f, .5f, 1],
            Colors = [new(.65f, .9f, 1, .7f), new(.65f, .9f, 1, .4f), new(.65f, .9f, 1, .12f), new(.65f, .9f, 1, 0)]
        }
    };
    private readonly Godot.Collections.Array _meshArrays = new();
    private readonly List<Vector3> _vertices = [];
    private readonly List<Color> _colors = [];
    private readonly List<int> _indices = [];

    public override void _Draw()
    {
        if (_targets.Count == 0 || _border.Length < 3) return;
        _vertices.Clear();
        _colors.Clear();
        _indices.Clear();
        float alpha = Mathf.Clamp(_age / .14f, 0, 1);
        foreach (var target in _targets)
        {
            var end = target.Point;
            var start = _origin;
            var outward = _outward;
            if (Style != KnowledgeRibbonStyle.Linked) start = BorderPoint(end, out outward);
            float distance = start.DistanceTo(end);
            if (distance < 12) continue;
            var direction = (end - start).Normalized();
            float departure = Style == KnowledgeRibbonStyle.Linked ? Mathf.Clamp(distance * .55f, 140, 240)
                : Mathf.Clamp(distance * .28f, 28, 120);
            var control1 = start + outward * departure;
            var control2 = Style == KnowledgeRibbonStyle.Linked
                ? end + target.Normal * Mathf.Clamp(distance * .2f, 24, 120)
                : end - direction * distance * .25f + Vector2.Up * Math.Min(40, distance * .05f);
            AddRibbon(start, control1, control2, end, alpha, target.Highlight);
        }
        if (_vertices.Count == 0) return;
        _meshArrays.Resize((int)Mesh.ArrayType.Max);
        _meshArrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        _meshArrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        _meshArrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
        _ribbonMesh.ClearSurfaces();
        _ribbonMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _meshArrays);
        DrawMesh(_ribbonMesh, null);
        if (Style == KnowledgeRibbonStyle.Linked)
            foreach (var target in _targets) DrawTarget(target, alpha);
    }

    private void DrawTarget(LinkTarget target, float alpha)
    {
        if (!target.Highlight) return;
        var color = new Color(.65f, .9f, 1);
        DrawTextureRect(_receiverGlow, new Rect2(target.Point - Vector2.One * 22, Vector2.One * 44),
            false, new Color(1, 1, 1, alpha));
        DrawCircle(target.Point, 3.5f, new Color(.9f, .97f, 1, alpha * .95f), true, -1, true);
        if (target.Count == 0) return;
        var font = ThemeDB.FallbackFont;
        string text = "×" + target.Count;
        var size = font.GetStringSize(text, fontSize: 17);
        var baseline = target.CountPosition + new Vector2(-size.X * .5f, 6);
        DrawStringOutline(font, baseline, text, fontSize: 17, size: 5, modulate: new Color(.04f, .09f, .12f, alpha * .65f));
        DrawString(font, baseline, text, fontSize: 17, modulate: new Color(color, alpha * .6f));
    }

    private float RibbonHalfWidth(float progress)
        => Mathf.Lerp(Mathf.Clamp(_frameWidth * .08f, 5, 28) * Appearance.WidthScale * (1 + Breath * .1f),
            1.3f, Mathf.SmoothStep(0, Appearance.TaperEnd, progress)) * .5f;

    private void AddRibbon(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, float alpha, bool receiving)
    {
        ReadOnlySpan<float> across = [-1, -.55f, 0, .55f, 1];
        var appearance = Appearance;
        ReadOnlySpan<float> feather = [0, appearance.ShoulderAlpha, 1, appearance.ShoulderAlpha, 0];
        float pulse = Style == KnowledgeRibbonStyle.Breath ? .86f + .14f * Breath : 1;
        int offset = _vertices.Count;
        float receivingLength = Math.Min(72, start.DistanceTo(end) * .2f);
        for (int row = 0; row <= RibbonSegments; row++)
        {
            float progress = (float)row / RibbonSegments;
            float path = progress * appearance.Reach;
            var center = start.BezierInterpolate(control1, control2, end, path);
            var sideways = start.BezierDerivative(control1, control2, end, path).Normalized().Orthogonal();
            float halfWidth = RibbonHalfWidth(progress);
            float sharedLines = Mathf.Lerp(_targets.Count, 1, Mathf.SmoothStep(.08f, .32f, progress));
            float sourceScale = Style == KnowledgeRibbonStyle.Linked ? 1 / Mathf.Sqrt(sharedLines) : 1;
            float fadeProgress = Style == KnowledgeRibbonStyle.Linked
                ? Mathf.Clamp((progress - .14f) / .86f, 0, 1) : progress;
            float opacity = alpha * pulse * (sourceScale * appearance.Opacity * Mathf.Pow(1 - fadeProgress, appearance.FadePower)
                + (Style == KnowledgeRibbonStyle.Linked ? .008f * Mathf.SmoothStep(0, .3f, progress) : 0));
            if (Style == KnowledgeRibbonStyle.Linked && receiving)
                opacity += alpha * .22f * Mathf.Pow(Mathf.Clamp(1 - center.DistanceTo(end) / receivingLength, 0, 1), 2);
            for (int band = 0; band < RibbonBands; band++)
            {
                float featherEdge = band switch { 0 => -.7f, RibbonBands - 1 => .7f, _ => 0 };
                var vertex = center + sideways * (halfWidth * across[band] + featherEdge);
                _vertices.Add(new(vertex.X, vertex.Y, 0));
                _colors.Add(new(.6f, .86f, 1, opacity * feather[band]));
                if (row == 0 || band == RibbonBands - 1) continue;
                int current = offset + row * RibbonBands + band;
                _indices.Add(current - RibbonBands);
                _indices.Add(current - RibbonBands + 1);
                _indices.Add(current + 1);
                _indices.Add(current - RibbonBands);
                _indices.Add(current + 1);
                _indices.Add(current);
            }
        }
        if (Style is KnowledgeRibbonStyle.Focused or KnowledgeRibbonStyle.Linked)
        {
            var corePoints = new Vector2[RibbonSegments + 1];
            var coreColors = new Color[corePoints.Length];
            for (int row = 0; row <= RibbonSegments; row++)
            {
                int center = offset + row * RibbonBands + RibbonBands / 2;
                var vertex = _vertices[center];
                corePoints[row] = new(vertex.X, vertex.Y);
                coreColors[row] = new(.8f, .95f, 1, _colors[center].A * .5f);
            }
            DrawPolylineColors(corePoints, coreColors, 1.2f, true);
        }
    }
}
