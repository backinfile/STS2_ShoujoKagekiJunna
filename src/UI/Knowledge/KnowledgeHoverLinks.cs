using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

/// <summary>Live, directional knowledge links between actual combat cards and their locations.</summary>
public partial class KnowledgeHoverLinks : Control
{
    private readonly List<LinkTarget> _targets = [];
    private const float ReceiverGap = 14;
    private readonly record struct LinkTarget(Vector2 Point, Vector2 CountPosition, int Count, bool Highlight, Vector2 Normal);
    private readonly List<Rect2> _tipBounds = [];
    private readonly Dictionary<Texture2D, Vector2[]> _iconBorders = [];
    private CardModel? _source;
    private Vector2[] _border = [];
    private float _frameWidth;
    private Vector2 _origin, _outward;
    private float _age;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 20;
    }

    public override void _Process(double delta)
    {
        _targets.Clear();
        var room = NCombatRoom.Instance;
        if (room == null || !room.IsVisibleInTree() || NHoverTipSet.shouldBlockHoverTips || room.Ui.Hand.InCardPlay)
        {
            _source = null;
            QueueRedraw();
            return;
        }

        var hovered = GetViewport().GuiGetHoveredControl();
        NCardHolder? holder = null;
        for (Node? node = hovered; node != null; node = node.GetParent())
        {
            if (node is not NCardHolder candidate) continue;
            holder = candidate;
            break;
        }
        var arrowHud = room.GetNodeOrNull<ArrowHud>("JunnaArrows");
        var arrows = arrowHud?.CardAnchors.ToArray() ?? [];
        var hoveredArrow = arrows.FirstOrDefault(pair => pair.Frame == hovered ||
            hovered != null && pair.Frame.IsAncestorOf(hovered));
        if (holder == null && hoveredArrow.Card == null && ActiveScreenContext.Instance.IsCurrent(room))
            holder = room.Ui.Hand.FocusedHolder;
        if (!GodotObject.IsInstanceValid(holder) || holder!.IsQueuedForDeletion()) holder = null;
        var cardNode = holder?.CardNode;
        CardModel? source = hoveredArrow.Card ?? (cardNode?.Visibility == ModelVisibility.Visible && holder!.IsVisibleInTree()
            ? cardNode.Model : null);
        if (source?.Pile?.IsCombatPile != true || !CardCatalog.IsKnowledge(source) || source.Owner.PlayerCombatState == null)
            source = null;
        if (_source != source) _age = 0;
        _source = source;
        _age += (float)delta;
        if (source != null)
        {
            var sourceFrame = cardNode?.Model == source ? cardNode.GetNode<Control>("%Frame") : hoveredArrow.Frame;
            bool deployedArrow = source.Pile!.Type == ArrowPile.PileId;
            _border = FrameBorder(sourceFrame, deployedArrow);
            _frameWidth = Point(sourceFrame, new(sourceFrame.Size.X, 0)).DistanceTo(Point(sourceFrame, Vector2.Zero));
            _origin = Point(sourceFrame, new(sourceFrame.Size.X * .5f, deployedArrow ? sourceFrame.Size.Y * 2 / 416 : 0));
            _outward = (_origin - Point(sourceFrame, new(sourceFrame.Size.X * .5f, sourceFrame.Size.Y * .5f))).Normalized();
            ReadTipBounds();
            var ids = KnowledgeMembers.PartnerIds(source);
            bool Related(CardModel card) => card != source && card.Owner == source.Owner && ids.Contains(card.Id);
            if (LocalContext.IsMe(source.Owner))
                foreach (var target in room.Ui.Hand.ActiveHolders)
                    if (target.CardNode is { Visibility: ModelVisibility.Visible, Model: { } card } node && card.Pile?.Type == PileType.Hand && Related(card))
                        AddTarget(node.GetNode<Control>("%Frame"));
            foreach (var (card, frame) in arrows)
                if (Related(card)) AddTarget(frame, arrow: true);
            if (LocalContext.IsMe(source.Owner))
            {
                var state = source.Owner.PlayerCombatState!;
                AddPile(state.DrawPile, room.Ui.DrawPile, Related);
                AddPile(state.DiscardPile, room.Ui.DiscardPile, Related);
                AddPile(state.ExhaustPile, room.Ui.ExhaustPile, Related);
            }
        }
        QueueRedraw();
    }

    private void AddPile(CardPile pile, Control button, Func<CardModel, bool> related)
    {
        if (!button.IsVisibleInTree()) return;
        int count = pile.Cards.Count(related);
        if (count == 0) return;
        AddTarget(button.GetNode<TextureRect>("Icon"), count: count);
    }

    private Vector2 Point(Control control, Vector2 local)
        => GetGlobalTransformWithCanvas().AffineInverse() * (control.GetGlobalTransformWithCanvas() * local);

    private Vector2 BorderPoint(Vector2 target, out Vector2 normal)
    {
        var closest = Vector2.Zero;
        normal = Vector2.Up;
        float nearest = float.PositiveInfinity;
        for (int index = 0; index < _border.Length; index++)
        {
            var start = _border[index];
            var edge = _border[(index + 1) % _border.Length] - start;
            float lengthSquared = edge.LengthSquared();
            if (lengthSquared <= .001f) continue;
            float fraction = Mathf.Clamp((target - start).Dot(edge) / lengthSquared, 0, 1);
            var candidate = start + edge * fraction;
            float distance = candidate.DistanceSquaredTo(target);
            if (distance >= nearest) continue;
            nearest = distance;
            closest = candidate;
            normal = new Vector2(edge.Y, -edge.X).Normalized();
        }
        return closest;
    }

    private Vector2[] IconBorder(TextureRect icon)
    {
        if (icon.Texture is { } texture)
        {
            if (!_iconBorders.TryGetValue(texture, out var contour))
            {
                // Pile sprites have transparent corners outside the visible card stack.
                using var image = texture.GetImage();
                if (image.IsCompressed()) image.Decompress();
                using var bitmap = new Bitmap();
                bitmap.CreateFromImageAlpha(image, .5f);
                static float Area(Vector2[] points) => Enumerable.Range(0, points.Length)
                    .Sum(i => points[i].Cross(points[(i + 1) % points.Length]));
                contour = bitmap.OpaqueToPolygons(new Rect2I(Vector2I.Zero, image.GetSize()), 1)
                    .MaxBy(points => Math.Abs(Area(points))) ?? [];
                if (Area(contour) < 0) Array.Reverse(contour);
                _iconBorders.Add(texture, contour);
            }
            if (contour.Length > 2)
            {
                var scale = icon.Size / texture.GetSize();
                var offset = Vector2.Zero;
                if (icon.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered)
                {
                    scale = Vector2.One * Math.Min(scale.X, scale.Y);
                    offset = (icon.Size - texture.GetSize() * scale) * .5f;
                }
                return contour.Select(point => Point(icon, offset + point * scale)).ToArray();
            }
        }
        return FrameBorder(icon);
    }

    private Vector2[] FrameBorder(Control frame, bool arrow = false)
    {
        Vector2[] outline = arrow
            ? [new(.5f, 2f / 416), new(238f / 240, 134f / 416), new(.5f, 413f / 416), new(2f / 240, 134f / 416)]
            : [Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down];
        return outline.Select(point => Point(frame, point * frame.Size)).ToArray();
    }

    private void AddTarget(Control frame, bool arrow = false, int count = 0)
    {
        var border = count > 0 && frame is TextureRect icon ? IconBorder(icon) : FrameBorder(frame, arrow);
        var point = ReceivingPoint(border, out var normal, out bool visible);
        _targets.Add(new(point, point + Vector2.Up * 28, count, visible, normal));
    }

    private Vector2 ReceivingPoint(Vector2[] border, out Vector2 normal, out bool visible)
    {
        var closest = border[0];
        normal = Vector2.Up;
        visible = false;
        float nearest = float.PositiveInfinity;
        var viewport = GetViewportRect().Grow(-8);
        bool inside = Geometry2D.IsPointInPolygon(_origin, border);
        for (int edge = 0; edge < border.Length; edge++)
        {
            var start = border[edge];
            var end = border[(edge + 1) % border.Length];
            var edgeVector = end - start;
            if (edgeVector.LengthSquared() < .001f) continue;
            var outward = new Vector2(edgeVector.Y, -edgeVector.X).Normalized();
            var projected = Geometry2D.GetClosestPointToSegment(_origin, start, end);
            for (int sample = -1; sample <= 32; sample++)
            {
                var anchor = sample < 0 ? projected : start.Lerp(end, sample / 32f);
                var toward = inside ? outward : (_origin - anchor).Normalized();
                if (!inside && outward.Dot(toward) <= 0) continue;
                var point = anchor + toward * ReceiverGap;
                bool candidateVisible = viewport.HasPoint(point) && !Geometry2D.IsPointInPolygon(point, _border)
                    && !_tipBounds.Any(rect => rect.HasPoint(point));
                if (visible && !candidateVisible) continue;
                float distance = point.DistanceSquaredTo(_origin);
                if (visible == candidateVisible && distance >= nearest) continue;
                nearest = distance;
                closest = point;
                normal = toward;
                visible = candidateVisible;
            }
        }
        return closest;
    }

    private void ReadTipBounds()
    {
        _tipBounds.Clear();
        if (Style != KnowledgeRibbonStyle.Linked || NGame.Instance?.HoverTipsContainer is not { } container) return;
        foreach (var set in container.GetChildren().OfType<NHoverTipSet>())
            foreach (var child in set.GetNode<Control>("textHoverTipContainer").GetChildren().OfType<Control>())
                if (child.IsVisibleInTree())
                    _tipBounds.Add(new Rect2(Point(child, Vector2.Zero), Point(child, child.Size) - Point(child, Vector2.Zero)).Abs().Grow(8));
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
internal static class KnowledgeHoverLinksPatch
{
    private static void Postfix(NCombatRoom __instance)
        => __instance.AddChild(new KnowledgeHoverLinks { Name = "JunnaKnowledgeLinks" });
}
