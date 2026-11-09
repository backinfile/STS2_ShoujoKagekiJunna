using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Orb-sized portrait arrows with an overhead formation and live target guides.</summary>
public partial class ArrowHud : Control
{
    // Game canvas units (1920 x 1080). Preserve the original kite proportions at orb scale.
    private const float IconWidth = 42;
    private const float FrameScale = IconWidth / 240;
    private const float SlotSpacing = 80;
    private const float RowSpacing = 96;
    private static readonly Vector2 FrameSize = new(240, 416);
    private static readonly Vector2 IconSize = FrameSize * FrameScale;
    // Pivot around the portrait body, above the kite's long lower point.
    private static readonly Vector2 BodyCenter = new Vector2(120, 138) * FrameScale;
    private static readonly Vector2 CounterCenter = new Vector2(120, 58) * FrameScale;
    private static readonly Vector2 Tip = new Vector2(120, 409) * FrameScale;
    private static readonly Vector2[] SatelliteOutline =
    [
        new(-5, -6), new(0, -3), new(5, -6), new(0, 9)
    ];
    private static readonly Vector2[] SatelliteInset =
    [
        new(-3, -3.5f), new(0, -1.5f), new(3, -3.5f), new(0, 5.5f)
    ];
    // Matches floating_arrow_c1.svg; the frame overlays this aperture.
    private static readonly Vector2[] PortraitWindow =
    [
        new(120, 30), new(216, 138), new(120, 364), new(24, 138)
    ];

    private sealed record SatelliteMotion(Node2D Node, float Height, float Radius, float Phase, float WobbleSpeed);
    private sealed record ArrowView(Control Root, Control Frame, Label Count, Label Repeats, SatelliteMotion[] Satellites,
        CpuParticles2D Flash)
    {
        public ArrowLayoutMotion Motion { get; } = new();
        internal Tween? CountTween;
        internal bool Releasing;
        internal Vector2? ExitPosition;
    }
    private readonly Dictionary<ArrowDeployment, ArrowView> _views = [];
    private Texture2D _frame = null!;
    private Texture2D _groupFrame = null!;
    private double _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 0;
        _frame = GD.Load<Texture2D>(JunnaMod.ResourceRoot + "images/floating_arrow_c1.svg");
        var groupPath = JunnaMod.ResourceRoot + "images/floating_arrow_group_a.svg";
        _groupFrame = ResourceLoader.Exists(groupPath) ? GD.Load<Texture2D>(groupPath) : _frame;
        ArrowDeploymentVfx.PreloadTextures();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        foreach (var removed in _views.Keys.Where(a => !ArrowCombat.Deployed.Contains(a)).ToArray())
        {
            FinishDeployment(removed);
            if (!_views[removed].Releasing) _views[removed].Root.QueueFree();
            _views.Remove(removed);
        }
        var room = NCombatRoom.Instance;
        if (room == null) return;
        var canvas = GetViewportRect().Size;
        // Use the second relic row, reserving exactly one row above the arrows.
        float rowY = SecondRelicRowY();
        IReadOnlyList<ArrowDeployment> arrows = ArrowCombat.DisplayOrder().Where(a => !a.Resolving).ToArray();
        UpdateLayout(arrows, canvas, rowY);
        for (int index = 0; index < arrows.Count; index++)
        {
            var arrow = arrows[index];
            if (!_views.TryGetValue(arrow, out var view))
            {
                view = CreateArrow(arrow);
                _views.Add(arrow, view);
            }
            if (view.Releasing) continue;
            var center = _slots[arrow];
            float bob = Mathf.Sin((float)_elapsed * 1.8f + arrow.Sequence * .8f) * 2;
            var targets = TargetPoints(arrow);
            var aim = targets.Length == 0 ? center + new Vector2(400, 400)
                : targets.Aggregate(Vector2.Zero, (sum, p) => sum + p) / targets.Length;
            // Aim the lower point directly at the target, including the current floating offset.
            // Countdown numerals remain upright as the frame rotates.
            view.Motion.Advance(center + Vector2.Down * bob, aim, (float)delta);
            view.Root.Position = view.Motion.Center - BodyCenter;
            view.Frame.Rotation = view.Motion.Angle;
            UpdateDeployment(arrow, view, aim);
            view.Count.Position = RotateInFrame(CounterCenter, view.Frame.Rotation) - view.Count.Size * .5f;
            UpdateCountdown(arrow, view);
            view.Count.AddThemeColorOverride("font_color", arrow.Remaining <= 1 ? new Color("FFE3A0") : Colors.White);
            view.Repeats.Visible = arrow.PlayCount > 1;
            view.Repeats.Text = $"×{arrow.PlayCount}";
            foreach (var satellite in view.Satellites)
            {
                satellite.Node.Visible = targets.Length > 0;
                UpdateSatellite(satellite);
            }
        }
        UpdateHoverPreview(room);
        QueueRedraw();
    }

    private float SecondRelicRowY()
    {
        var inventory = NRun.Instance?.GlobalUi.RelicInventory;
        var first = inventory?.RelicNodes.FirstOrDefault();
        if (first == null) return 184;
        float rowStep = first.Size.Y + inventory!.GetThemeConstant("v_separation", "FlowContainer");
        var reference = inventory.GetTransform();
        reference.Origin = inventory.GetDefaultPosition();
        var stableTransform = inventory.GetParent<CanvasItem>().GetGlobalTransform() * reference;
        return LocalPoint(stableTransform * (first.Position + first.Size * .5f + Vector2.Down * rowStep)).Y;
    }

    private Vector2 LocalPoint(Vector2 global) => GetGlobalTransform().AffineInverse() * global;
    private static Vector2 RotateInFrame(Vector2 point, float angle) => (point - BodyCenter).Rotated(angle) + BodyCenter;

    private Vector2[] TargetPoints(ArrowDeployment arrow)
    {
        var room = NCombatRoom.Instance;
        if (room == null || arrow.Card.CombatState == null) return [];
        return arrow.Targets.Where(t => t.IsAlive || ArrowCombat.AwaitingReturn(arrow))
            .Select(room.GetCreatureNode).Where(n => n != null)
            .Select(n => LocalPoint(n!.VfxSpawnPosition)).OrderBy(p => p.X).ToArray();
    }

    public override void _Draw()
    {
        // Draw behind the portrait children. Dead or removed targets have no guide.
        foreach (var (arrow, view) in _views)
        {
            if (view.Releasing) continue;
            var targets = TargetPoints(arrow);
            if (targets.Length == 0) continue;
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
                DrawSoftFan(fanStart, left, right);
                DrawSoftGroupGuide(fanStart, left, .2f);
                DrawSoftGroupGuide(fanStart, right, .2f);
                foreach (var end in targets)
                    DrawSoftGroupGuide(fanStart, start.Lerp(end, guideProgress), .13f);
            }
            else
            {
                var end = start.Lerp(targets[0], guideProgress);
                DrawFadingGuide(start, end, .08f, 6);
                DrawFadingGuide(start, end, .42f, 1.3f);
            }
        }
    }

    private static SatelliteMotion[] CreateSatellites(Control frame)
    {
        // Cosmetic layout is sampled once per deployment, independently of combat RNG.
        // Uneven phases keep the arrows staggered as they orbit the front half of the body.
        var samples = Enumerable.Range(0, 3).Select(_ => Random.Shared.NextSingle()).Order().ToArray();
        float phaseOffset = Random.Shared.NextSingle() * Mathf.Tau;
        return samples.Select((sample, index) =>
        {
            var node = new Node2D { Name = $"GroupSatellite{index}" };
            node.AddChild(new Polygon2D
            {
                Polygon = SatelliteOutline, Color = new Color("BEC3BF"), Antialiased = true
            });
            node.AddChild(new Polygon2D
            {
                Polygon = SatelliteInset, Antialiased = true,
                VertexColors = [new Color("A8C9DC"), new Color("7698AD"), new Color("789DB5"), new Color("60849E")]
            });
            frame.AddChild(node);
            return new SatelliteMotion(node, 44 + Random.Shared.NextSingle() * 11,
                14 + Random.Shared.NextSingle() * 4,
                phaseOffset + sample * (Mathf.Tau - 5.1f) + index * 1.7f,
                .5f + Random.Shared.NextSingle() * .7f);
        }).ToArray();
    }

    private void UpdateSatellite(SatelliteMotion satellite)
    {
        float time = (float)_elapsed;
        float phase = time * .65f + satellite.Phase
            + Mathf.Sin(time * satellite.WobbleSpeed + satellite.Phase) * .12f;
        float depth = Mathf.Sin(phase);
        float scale = .96f + depth * .1f;
        satellite.Node.Position = new(IconWidth * .5f + Mathf.Cos(phase) * satellite.Radius,
            satellite.Height + Mathf.Sin(time * satellite.WobbleSpeed + satellite.Phase) * 1.1f);
        satellite.Node.Scale = new(scale * (.78f + .22f * Mathf.Abs(depth)), scale);
        satellite.Node.Modulate = new Color(1, 1, 1, .8f + depth * .2f);
        // Behind the portrait on the far side, over it on the near side; tips stay behind the main point.
        satellite.Node.ShowBehindParent = depth < 0;
    }

    private static float GroupGuideFade(float progress)
        => Mathf.SmoothStep(0, .18f, progress) * Mathf.Pow(1 - progress, 1.6f);

    private void DrawSoftFan(Vector2 start, Vector2 left, Vector2 right)
    {
        // The first frames of a growing fan can be too small to triangulate reliably.
        if (Mathf.Abs((left - start).Cross(right - start)) < 64) return;
        const int segments = 10;
        ReadOnlySpan<float> across = [0, .22f, .78f, 1];
        ReadOnlySpan<float> edgeAlpha = [0, 1, 1, 0];
        for (int i = 0; i < segments; i++)
        {
            float near = (float)i / segments;
            float far = (float)(i + 1) / segments;
            var nearLeft = start.Lerp(left, near * .7f);
            var nearRight = start.Lerp(right, near * .7f);
            var farLeft = start.Lerp(left, far * .7f);
            var farRight = start.Lerp(right, far * .7f);
            float nearAlpha = .045f * GroupGuideFade(near);
            float farAlpha = .045f * GroupGuideFade(far);
            for (int band = 0; band < across.Length - 1; band++)
            {
                var a = nearLeft.Lerp(nearRight, across[band]);
                var b = nearLeft.Lerp(nearRight, across[band + 1]);
                var c = farLeft.Lerp(farRight, across[band + 1]);
                var d = farLeft.Lerp(farRight, across[band]);
                var ca = new Color(.48f, .8f, .95f, nearAlpha * edgeAlpha[band]);
                var cb = new Color(.48f, .8f, .95f, nearAlpha * edgeAlpha[band + 1]);
                var cc = new Color(.48f, .8f, .95f, farAlpha * edgeAlpha[band + 1]);
                var cd = new Color(.48f, .8f, .95f, farAlpha * edgeAlpha[band]);
                // Feather both side edges and both ends instead of leaving a hard cone apex.
                if (i == 0) DrawPolygon([start, c, d], [ca, cc, cd]);
                else DrawPolygon([a, b, c, d], [ca, cb, cc, cd]);
            }
        }
    }

    private void DrawSoftGroupGuide(Vector2 start, Vector2 target, float alpha)
    {
        DrawFadingGuide(start, target, alpha * .18f, 6, true);
        DrawFadingGuide(start, target, alpha * .28f, 3, true);
        DrawFadingGuide(start, target, alpha * .54f, 1.5f, true);
    }

    private void DrawFadingGuide(Vector2 start, Vector2 target, float alpha, float width = 1, bool fadeStart = false)
    {
        const int segments = 16;
        var points = new Vector2[segments + 1];
        var colors = new Color[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float progress = (float)i / segments;
            points[i] = start.Lerp(target, progress * .7f);
            float fade = fadeStart ? GroupGuideFade(progress) : Mathf.Pow(1 - progress, 1.6f);
            colors[i] = new Color(.6f, .86f, 1, alpha * fade);
        }
        DrawPolylineColors(points, colors, width, true);
    }

    private ArrowView CreateArrow(ArrowDeployment arrow)
    {
        var root = new Control { Name = "FloatingArrow", Size = IconSize, MouseFilter = MouseFilterEnum.Ignore };
        var frame = new Control { Name = "TiltedFrame", Size = IconSize, PivotOffset = BodyCenter, MouseFilter = MouseFilterEnum.Stop };
        root.AddChild(frame);
        var portrait = ArrowPortraitTexture.Resolve(arrow.Card.Portrait);
        var textureSize = portrait.GetSize();
        var designId = CardCatalog.DesignId(arrow.Card);
        var focus = designId is null ? null : CardCatalog.Get(designId).ArrowFocus;
        var focusPoint = new Vector2(focus?.X ?? .5f, focus?.Y ?? .5f).Clamp(.01f, .99f);
        var margin = new Vector2(Math.Min(focusPoint.X, 1 - focusPoint.X), Math.Min(focusPoint.Y, 1 - focusPoint.Y));
        float coverScale = Math.Max(FrameSize.X / (textureSize.X * 2 * margin.X), FrameSize.Y / (textureSize.Y * 2 * margin.Y));
        var cropSize = FrameSize / coverScale;
        var cropOffset = textureSize * focusPoint - cropSize * .5f;
        frame.AddChild(new Polygon2D
        {
            Name = "Portrait", Polygon = PortraitWindow.Select(p => p * FrameScale).ToArray(),
            UV = PortraitWindow.Select(p => p / coverScale + cropOffset).ToArray(),
            Texture = portrait, Antialiased = true
        });
        frame.AddChild(new TextureRect
        {
            Name = "ArrowFrame", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = arrow.TargetsGroup ? _groupFrame : _frame,
            Size = IconSize, MouseFilter = MouseFilterEnum.Ignore
        });
        frame.AddChild(CardBonusVfx.ForArrow(arrow.Card,
            new Vector2[] { new(120, 14), new(228, 138), new(120, 401), new(12, 138) }
                .Select(p => p * FrameScale).ToArray()));
        var count = MakeLabel("Countdown", Vector2.Zero, new(20, 20), 14);
        root.AddChild(count);
        var repeats = MakeLabel("Repeats", new(IconWidth - 5, 16), new(32, 20), 16);
        root.AddChild(repeats);
        root.AddChild(CardInvestmentMarks.ForArrow(arrow.Card));
        var flash = ArrowFeedback.CreateFlash(arrow.TargetsGroup ? _groupFrame : _frame, IconWidth);
        flash.Position = IconSize * .5f;
        frame.AddChild(flash);
        AddChild(root);
        return new(root, frame, count, repeats, arrow.TargetsGroup ? CreateSatellites(frame) : [], flash);
    }

    private static Label MakeLabel(string name, Vector2 position, Vector2 size, int fontSize)
    {
        var label = new Label
        {
            Name = name, Position = position, Size = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", new Color("172331"));
        label.AddThemeConstantOverride("outline_size", 2);
        return label;
    }
}
