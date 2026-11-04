using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Orb-sized portrait arrows with an overhead formation and live target guides.</summary>
public partial class ArrowHud : Control
{
    // Game canvas units (1920 x 1080). Preserve the original kite proportions at orb scale.
    private const float IconWidth = 42;
    private const float FrameScale = IconWidth / 240;
    private const float SlotSpacing = 108;
    private static readonly Vector2 FrameSize = new(240, 416);
    private static readonly Vector2 IconSize = FrameSize * FrameScale;
    private static readonly Vector2 CounterCenter = new Vector2(120, 58) * FrameScale;
    private static readonly Vector2 Tip = new Vector2(120, 409) * FrameScale;
    private static readonly Vector2 GroupLeftTip = new Vector2(55, 372) * FrameScale;
    private static readonly Vector2 GroupRightTip = new Vector2(185, 372) * FrameScale;
    // Matches floating_arrow_c1.svg; the frame overlays this aperture.
    private static readonly Vector2[] PortraitWindow =
    [
        new(120, 30), new(216, 138), new(120, 364), new(24, 138)
    ];

    private sealed record ArrowView(Control Root, Control Frame, Label Count, Label Repeats);
    private readonly Dictionary<ArrowDeployment, ArrowView> _views = [];
    private Texture2D _frame = null!;
    private Texture2D _groupFrame = null!;
    private double _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 3;
        _frame = GD.Load<Texture2D>(JunnaMod.ResourceRoot + "images/floating_arrow_c1.svg");
        var groupPath = JunnaMod.ResourceRoot + "images/floating_arrow_group_a.svg";
        _groupFrame = ResourceLoader.Exists(groupPath) ? GD.Load<Texture2D>(groupPath) : _frame;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        foreach (var removed in _views.Keys.Where(a => !ArrowCombat.Deployed.Contains(a)).ToArray())
        {
            _views[removed].Root.QueueFree();
            _views.Remove(removed);
        }
        var room = NCombatRoom.Instance;
        if (room == null) return;
        var canvas = GetViewportRect().Size;
        // Use the second relic row, reserving exactly one row above the arrows.
        float rowY = SecondRelicRowY();
        IReadOnlyList<ArrowDeployment> arrows = ArrowCombat.Deployed
            .OrderBy(a => a.TargetsAllEnemies ? float.NegativeInfinity
                : room.GetCreatureNode(a.Play.Target)?.VfxSpawnPosition.X ?? float.PositiveInfinity)
            .ToArray();
        float singleStart = canvas.X * .44f;
        var groupSlots = new Dictionary<Creature, int>();
        foreach (var arrow in arrows.Where(a => a.TargetsAllEnemies))
        {
            var owner = arrow.Card.Owner.Creature;
            int slot = groupSlots.GetValueOrDefault(owner);
            groupSlots[owner] = slot + 1;
            float x = GroupStart(owner, canvas) + Math.Min(slot, 2) * SlotSpacing;
            singleStart = Math.Max(singleStart, x + SlotSpacing);
        }
        singleStart = Math.Min(singleStart, canvas.X * .72f);
        int columns = Math.Max(1, (int)((canvas.X * .9f - singleStart) / SlotSpacing) + 1);
        groupSlots.Clear();
        int singleIndex = 0;
        for (int index = 0; index < arrows.Count; index++)
        {
            var arrow = arrows[index];
            if (!_views.TryGetValue(arrow, out var view))
            {
                view = CreateArrow(arrow);
                _views.Add(arrow, view);
            }
            Vector2 center;
            if (arrow.TargetsAllEnemies)
            {
                var owner = arrow.Card.Owner.Creature;
                int slot = groupSlots.GetValueOrDefault(owner);
                groupSlots[owner] = slot + 1;
                center = new(GroupStart(owner, canvas) + slot % 3 * SlotSpacing, rowY + slot / 3 * 140);
            }
            else
            {
                center = new(singleStart + singleIndex % columns * SlotSpacing, rowY + singleIndex / columns * 140);
                singleIndex++;
            }
            float bob = Mathf.Sin((float)_elapsed * 1.8f + index * .8f) * 2;
            view.Root.Position = center + Vector2.Down * bob - IconSize * .5f;
            var targets = TargetPoints(arrow);
            var aim = targets.Length == 0 ? center + new Vector2(400, 400)
                : targets.Aggregate(Vector2.Zero, (sum, p) => sum + p) / targets.Length;
            // Aim the lower point directly at the target, including the current floating offset.
            // Countdown numerals remain upright as the frame rotates.
            var facingCenter = view.Root.Position + IconSize * .5f;
            view.Frame.Rotation = (aim - facingCenter).Angle() - Mathf.Pi * .5f;
            view.Count.Position = RotateInFrame(CounterCenter, view.Frame.Rotation) - view.Count.Size * .5f;
            view.Count.Text = arrow.Remaining.ToString();
            view.Count.AddThemeColorOverride("font_color", arrow.Remaining <= 1 ? new Color("FFE3A0") : Colors.White);
            view.Repeats.Visible = arrow.Play.PlayCount > 1;
            view.Repeats.Text = $"×{arrow.Play.PlayCount}";
        }
        QueueRedraw();
    }

    private float SecondRelicRowY()
    {
        var inventory = NRun.Instance?.GlobalUi.RelicInventory;
        var first = inventory?.RelicNodes.FirstOrDefault();
        if (first == null) return 184;
        float rowStep = first.Size.Y + inventory!.GetThemeConstant("v_separation", "FlowContainer");
        return LocalPoint(first.GetGlobalRect().GetCenter() + inventory.GetGlobalTransform().Y * rowStep).Y;
    }

    private float GroupStart(Creature owner, Vector2 canvas)
    {
        var node = NCombatRoom.Instance?.GetCreatureNode(owner);
        float playerX = node == null ? canvas.X * .25f : LocalPoint(node.VfxSpawnPosition).X;
        return Mathf.Clamp(playerX + 180, canvas.X * .25f, canvas.X * .43f);
    }

    private Vector2 LocalPoint(Vector2 global) => GetGlobalTransform().AffineInverse() * global;
    private static Vector2 RotateInFrame(Vector2 point, float angle) => (point - IconSize * .5f).Rotated(angle) + IconSize * .5f;

    private Vector2[] TargetPoints(ArrowDeployment arrow)
    {
        var room = NCombatRoom.Instance;
        if (room == null || arrow.Card.CombatState == null) return [];
        IEnumerable<Creature> targets = arrow.TargetsAllEnemies
            ? arrow.Card.CombatState!.GetOpponentsOf(arrow.Card.Owner.Creature)
            : arrow.Play.Target is { } target ? [target] : [];
        return targets.Where(t => t.IsAlive).Select(room.GetCreatureNode).Where(n => n != null)
            .Select(n => LocalPoint(n!.VfxSpawnPosition)).OrderBy(p => p.X).ToArray();
    }

    public override void _Draw()
    {
        // Draw behind the portrait children. Dead or removed targets have no guide.
        foreach (var (arrow, view) in _views)
        {
            var targets = TargetPoints(arrow);
            if (targets.Length == 0) continue;
            var start = view.Root.Position + RotateInFrame(Tip, view.Frame.Rotation);
            if (arrow.TargetsAllEnemies)
            {
                var left = targets[0] + new Vector2(-55, 25);
                var right = targets[^1] + new Vector2(55, 25);
                var midpoint = (left + right) * .5f;
                left = midpoint + (left - midpoint) * 1.25f;
                right = midpoint + (right - midpoint) * 1.25f;
                var leftStart = view.Root.Position + RotateInFrame(GroupLeftTip, view.Frame.Rotation);
                var rightStart = view.Root.Position + RotateInFrame(GroupRightTip, view.Frame.Rotation);
                var fanColor = new Color(.48f, .8f, .95f, .045f);
                DrawPolygon([leftStart, leftStart.Lerp(left, .7f), rightStart.Lerp(right, .7f), rightStart],
                    [fanColor, Colors.Transparent, Colors.Transparent, fanColor]);
                DrawFadingGuide(leftStart, left, .2f);
                DrawFadingGuide(rightStart, right, .2f);
                foreach (var end in targets)
                    DrawFadingGuide(start, end, .13f);
            }
            else
            {
                var end = targets[0];
                DrawFadingGuide(start, end, .08f, 6);
                DrawFadingGuide(start, end, .42f, 1.3f);
            }
        }
    }

    private void DrawFadingGuide(Vector2 start, Vector2 target, float alpha, float width = 1)
    {
        const int segments = 16;
        var points = new Vector2[segments + 1];
        var colors = new Color[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float progress = (float)i / segments;
            points[i] = start.Lerp(target, progress * .7f);
            colors[i] = new Color(.6f, .86f, 1, alpha * Mathf.Pow(1 - progress, 1.6f));
        }
        DrawPolylineColors(points, colors, width, true);
    }

    private ArrowView CreateArrow(ArrowDeployment arrow)
    {
        var root = new Control { Name = "FloatingArrow", Size = IconSize, MouseFilter = MouseFilterEnum.Ignore };
        var frame = new Control { Name = "TiltedFrame", Size = IconSize, PivotOffset = IconSize * .5f, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(frame);
        var portraitPath = JunnaMod.ResourceRoot + $"images/floating_arrows/{arrow.Card.Id.Entry}.png";
        bool hasArrowPortrait = ResourceLoader.Exists(portraitPath);
        var portrait = hasArrowPortrait ? GD.Load<Texture2D>(portraitPath) : arrow.Card.Portrait;
        var textureSize = portrait.GetSize();
        float coverScale = Math.Max(FrameSize.X / textureSize.X, FrameSize.Y / textureSize.Y);
        var cropOffset = (textureSize - FrameSize / coverScale) * .5f;
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
            Texture = arrow.TargetsAllEnemies ? _groupFrame : _frame,
            Size = IconSize, MouseFilter = MouseFilterEnum.Ignore
        });
        var count = MakeLabel("Countdown", Vector2.Zero, new(20, 20), 14);
        root.AddChild(count);
        var repeats = MakeLabel("Repeats", new(IconWidth - 5, 16), new(32, 20), 16);
        root.AddChild(repeats);
        AddChild(root);
        return new(root, frame, count, repeats);
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
