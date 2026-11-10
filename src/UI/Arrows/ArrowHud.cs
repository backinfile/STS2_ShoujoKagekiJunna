using ShoujoKageki.Core.V1.Presentation;
using Godot;
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
    private sealed record ArrowView(Control Root, Control Frame, Label Count, Label Repeats, TextureRect Border,
        CpuParticles2D Flash)
    {
        public ArrowLayoutMotion Motion { get; } = new();
        public SatelliteMotion[] Satellites { get; set; } = [];
        internal bool Group;
        internal Tween? CountTween;
        internal bool Releasing;
        internal Vector2? ExitPosition;
        internal List<Vector2> Targets { get; } = [];
        internal Vector2 TargetCenter;
        internal int TargetOrder;
        internal int Remaining = int.MinValue;
        internal int PlayCount = int.MinValue;
        internal bool? SatellitesVisible;
    }
    private readonly Dictionary<ArrowDeployment, ArrowView> _views = [];
    private Texture2D _frame = null!;
    private Texture2D _groupFrame = null!;
    private double _elapsed;
    private ArrowGuides _guides = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 0;
        _frame = GD.Load<Texture2D>(JunnaMod.ResourceRoot + "images/floating_arrow_c1.svg");
        var groupPath = JunnaMod.ResourceRoot + "images/floating_arrow_group_a.svg";
        _groupFrame = ResourceLoader.Exists(groupPath) ? GD.Load<Texture2D>(groupPath) : _frame;
        ArrowDeploymentVfx.PreloadTextures();
        _guides = new ArrowGuides { Name = "ArrowGuides", Hud = this, ShowBehindParent = true };
        AddChild(_guides);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        var room = NCombatRoom.Instance;
        if (room == null) return;
        RefreshArrowSnapshots(room);
        var canvas = GetViewportRect().Size;
        // Use the second relic row, reserving exactly one row above the arrows.
        float rowY = SecondRelicRowY();
        var arrows = _displayOrder;
        UpdateLayout(arrows, canvas, rowY);
        for (int index = 0; index < arrows.Count; index++)
        {
            var arrow = arrows[index];
            var view = _views[arrow];
            var center = _slots[arrow];
            bool visible = _visibleArrows.Contains(arrow);
            if (!visible) FinishDeployment(arrow);
            view.Root.Visible = visible;
            float bob = CoreEffects.AmbientEffects ? Mathf.Sin((float)_elapsed * 1.8f + arrow.Sequence * .8f) * 2 : 0;
            var aim = view.Targets.Count == 0 ? center + new Vector2(400, 400) : view.TargetCenter;
            // Aim the lower point directly at the target, including the current floating offset.
            // Countdown numerals remain upright as the frame rotates.
            view.Motion.Advance(center + Vector2.Down * bob, aim, (float)delta);
            view.Root.Position = view.Motion.Center - BodyCenter;
            view.Frame.Rotation = view.Motion.Angle;
            if (visible) UpdateDeployment(arrow, view, aim);
            view.Count.Position = RotateInFrame(CounterCenter, view.Frame.Rotation) - view.Count.Size * .5f;
            UpdateCountdown(arrow, view);
            if (view.PlayCount != arrow.PlayCount)
            {
                view.PlayCount = arrow.PlayCount;
                view.Repeats.Visible = arrow.PlayCount > 1;
                view.Repeats.Text = $"×{arrow.PlayCount}";
            }
            bool satellitesVisible = visible && CoreEffects.AmbientEffects && view.Targets.Count > 0;
            foreach (var satellite in view.Satellites)
            {
                if (view.SatellitesVisible != satellitesVisible) satellite.Node.Visible = satellitesVisible;
                if (satellitesVisible) UpdateSatellite(satellite);
            }
            view.SatellitesVisible = satellitesVisible;
        }
        UpdateHoverPreview(room);
        WarmPreviewCache();
        foreach (var arrow in arrows)
            _views[arrow].Frame.Modulate = new Color(1, 1, 1,
                _highlightedArrows.Count == 0 || _highlightedArrows.Contains(arrow) ? 1 : .55f);
        CoreEffects.Redraw(_guides);
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
        return arrow.DisplayTargets.Where(t => t.IsAlive || ArrowCombat.AwaitingReturn(arrow))
            .Select(room.GetCreatureNode).Where(n => n != null)
            .Select(n => LocalPoint(n!.VfxSpawnPosition)).OrderBy(p => p.X).ToArray();
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
        var border = new TextureRect
        {
            Name = "ArrowFrame", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = arrow.DisplaysGroup ? _groupFrame : _frame,
            Size = IconSize, MouseFilter = MouseFilterEnum.Ignore
        };
        frame.AddChild(border);
        frame.AddChild(CardBonusVfx.ForArrow(arrow.Card,
            new Vector2[] { new(120, 14), new(228, 138), new(120, 401), new(12, 138) }
                .Select(p => p * FrameScale).ToArray()));
        var count = MakeLabel("Countdown", Vector2.Zero, new(26, 26), 20);
        count.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("172331"), CornerRadiusTopLeft = 13, CornerRadiusTopRight = 13,
            CornerRadiusBottomLeft = 13, CornerRadiusBottomRight = 13
        });
        root.AddChild(count);
        var repeats = MakeLabel("Repeats", new(IconWidth - 5, 16), new(36, 20), 14);
        root.AddChild(repeats);
        root.AddChild(CardInvestmentMarks.ForArrow(arrow.Card));
        var flash = ArrowFeedback.CreateFlash(arrow.DisplaysGroup ? _groupFrame : _frame, IconWidth);
        flash.Position = IconSize * .5f;
        frame.AddChild(flash);
        AddChild(root);
        return new(root, frame, count, repeats, border, flash)
        {
            Group = arrow.DisplaysGroup,
            Satellites = arrow.DisplaysGroup ? CreateSatellites(frame) : []
        };
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
