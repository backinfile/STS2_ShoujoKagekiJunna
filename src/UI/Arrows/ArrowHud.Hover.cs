using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.UI;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    internal IEnumerable<(CardModel Card, Control Frame)> CardAnchors => _views
        .Where(pair => pair.Key.Card.Pile?.Type == ArrowPile.PileId && !pair.Key.Resolving
            && !pair.Value.Releasing && pair.Value.Frame.IsVisibleInTree())
        .Select(pair => (pair.Key.Card, pair.Value.Frame));

    private const int PreviewCapacity = 5;
    private const float PreviewScale = .9f, PreviewMargin = 24;
    private Control? _hoverPreview, _hoverAnchor, _dismissedAnchor;
    private Label? _previewCount;
    private Creature? _previewTarget;
    private ArrowDeployment? _inspectedArrow;
    private readonly Dictionary<ArrowDeployment, NCard> _previewCache = [];
    private readonly List<(ArrowDeployment Arrow, NCard Card)> _previewCards = [];
    private readonly List<ArrowDeployment> _hoverArrows = [], _hoverCandidates = [];
    private readonly HashSet<ArrowDeployment> _highlightedArrows = [];
    private int _previewIndex, _previewStart;
    private bool _previewHoverSelection = true;
    private double _lastHoverTime;
    private Tween? _previewMotion;
    private (Vector2 Canvas, Rect2 Anchor, int Focus, int Total, int Visible)? _previewLayout;
    private readonly List<Rect2> _previewObstacles = [];

    private void WarmPreviewCache()
    {
        if (!IsVisibleInTree() || _hoverPreview?.Visible == true || _previewCache.Count >= PreviewCapacity * 2) return;
        foreach (var arrow in _displayOrder)
        {
            if (_previewCache.ContainsKey(arrow)) continue;
            EnsureHoverPreview();
            CreatePreviewCard(arrow);
            return; // Spread card construction across idle frames before the player opens a preview.
        }
    }

    private NCard CreatePreviewCard(ArrowDeployment arrow)
    {
        var card = NCard.Create(arrow.Card)!;
        _hoverPreview!.AddChild(card);
        KnowledgePreviewCards.IgnoreMouse(card);
        card.AddChild(new Control
        {
            Name = "ArrowPreviewHitbox", Position = new(-150, -211), Size = new(300, 422),
            MouseFilter = MouseFilterEnum.Stop
        });
        card.AddChild(MakeLabel("ArrowPreviewQuantity", new(-55, 216), new(110, 28), 20));
        card.Hide();
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        _previewCache.Add(arrow, card);
        return card;
    }

    private void UpdateHoverPreview(NCombatRoom room)
    {
        if (!IsVisibleInTree() || NHoverTipSet.shouldBlockHoverTips || room.Ui.Hand.InCardPlay)
        {
            ClearHoverPreview();
            return;
        }
        if (_hoverAnchor != null && !GodotObject.IsInstanceValid(_hoverAnchor)) ClearHoverPreview();
        var hovered = GetViewport().GuiGetHoveredControl();
        if (_dismissedAnchor != null && GodotObject.IsInstanceValid(_dismissedAnchor)
            && (hovered == _dismissedAnchor || hovered != null && _dismissedAnchor.IsAncestorOf(hovered))) return;
        _dismissedAnchor = null;
        bool insidePreview = _hoverPreview?.IsVisibleInTree() == true && hovered != null
            && (hovered == _hoverPreview || _hoverPreview.IsAncestorOf(hovered));
        ArrowDeployment? pointed = null;
        if (insidePreview)
        {
            if (_previewHoverSelection)
                pointed = _previewCards.FirstOrDefault(pair => pair.Card.IsAncestorOf(hovered!)).Arrow;
            hovered = _hoverAnchor;
        }
        var direct = _views.FirstOrDefault(pair => !pair.Value.Releasing && pair.Value.Root.Visible
            && (pair.Value.Frame == hovered || hovered != null && pair.Value.Frame.IsAncestorOf(hovered)));
        var creature = room.CreatureNodes.FirstOrDefault(node => node.Entity.IsAlive
            && (node.Hitbox == hovered || hovered != null && node.Hitbox.IsAncestorOf(hovered)));
        _hoverCandidates.Clear();
        Control? anchor = null;
        if (direct.Key != null)
        {
            anchor = direct.Value.Frame;
            _hoverCandidates.Add(direct.Key);
        }
        else if (creature != null)
        {
            anchor = creature.Hitbox;
            foreach (var arrow in _displayOrder)
                if (arrow.Targets.Contains(creature.Entity)) _hoverCandidates.Add(arrow);
        }
        if (_hoverCandidates.Count == 0)
        {
            if (!insidePreview && _hoverAnchor?.IsVisibleInTree() == true && _elapsed - _lastHoverTime < .25)
                return;
            ClearHoverPreview();
            return;
        }
        _lastHoverTime = _elapsed;
        bool sourceChanged = _hoverAnchor != anchor;
        var previous = _inspectedArrow;
        _hoverAnchor = anchor;
        _previewTarget = creature?.Entity;
        GroupHoverCandidates();
        if (sourceChanged)
        {
            _previewMotion?.Kill();
            _previewMotion = null;
            _previewIndex = _previewStart = 0;
        }
        else if ((pointed ?? previous) is { } selected && _hoverRepresentatives.TryGetValue(selected, out var representative))
            _previewIndex = _hoverArrows.IndexOf(representative);
        _previewIndex = Math.Clamp(_previewIndex, 0, _hoverArrows.Count - 1);
        _previewStart = Math.Clamp(_previewStart, 0, Math.Max(0, _hoverArrows.Count - PreviewCapacity));
        if (_previewIndex < _previewStart || _previewIndex >= _previewStart + PreviewCapacity)
            _previewStart = Math.Clamp(_previewIndex - 2, 0, Math.Max(0, _hoverArrows.Count - PreviewCapacity));
        _highlightedArrows.Clear();
        foreach (var arrow in _hoverCandidates) _highlightedArrows.Add(arrow);
        EnsureHoverPreview();
        RefreshPreviewFan();
        LayoutHoverPreview(anchor!, !sourceChanged && previous != _inspectedArrow);
        _hoverPreview!.Show();
    }

    private void EnsureHoverPreview()
    {
        if (_hoverPreview != null)
        {
            // Godot GUI picking follows sibling order, independently of visual ZIndex.
            if (_hoverPreview.GetIndex() != GetChildCount() - 1) MoveChild(_hoverPreview, GetChildCount() - 1);
            return;
        }
        _hoverPreview = new Control { Name = "JunnaArrowHoverPreview", MouseFilter = MouseFilterEnum.Stop, ZIndex = 20, Visible = false };
        AddChild(_hoverPreview);
        _previewCount = MakeLabel("CardIndex", Vector2.Zero, new(110, 28), 18);
        _previewCount.AddThemeColorOverride("font_color", new Color("D0B775"));
        _previewCount.ZIndex = 30;
        _hoverPreview.AddChild(_previewCount);
    }

    private void RefreshPreviewFan()
    {
        int count = Math.Min(PreviewCapacity, _hoverArrows.Count - _previewStart);
        bool same = _previewCards.Count == count;
        for (int i = 0; same && i < count; i++) same = _previewCards[i].Arrow == _hoverArrows[_previewStart + i];
        if (!same)
        {
            _previewLayout = null;
            foreach (var card in _previewCache.Values) card.Hide();
            _previewCards.Clear();
            for (int i = 0; i < count; i++)
            {
                var arrow = _hoverArrows[_previewStart + i];
                if (!_previewCache.TryGetValue(arrow, out var card)) card = CreatePreviewCard(arrow);
                card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                card.Show();
                _previewCards.Add((arrow, card));
            }
            foreach (var arrow in _previewCache.Keys.ToArray())
            {
                if (_previewCache.Count <= PreviewCapacity * 2) break;
                if (!_previewCards.Any(pair => pair.Arrow == arrow)) RemovePreviewCard(arrow);
            }
        }
        var inspected = _hoverArrows[_previewIndex];
        if (_inspectedArrow != inspected || !same)
        {
            _inspectedArrow = inspected;
            // Match native hand focus: the readable card is above the smaller neighbouring cards.
            for (int depth = PreviewCapacity - 1; depth >= 0; depth--)
                for (int i = 0; i < _previewCards.Count; i++)
                    if (Math.Abs(_previewStart + i - _previewIndex) == depth)
                        _hoverPreview!.MoveChild(_previewCards[i].Card, _hoverPreview.GetChildCount() - 1);
        }
        foreach (var (arrow, card) in _previewCards)
        {
            if (_previewStates.TryGetValue(arrow, out var state)
                && (!_renderedPreviewStates.TryGetValue(arrow, out var rendered) || rendered != state))
            {
                card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                _renderedPreviewStates[arrow] = state;
            }
            card.SetPreviewTarget(_previewTarget ?? arrow.Target);
            var quantity = card.GetNode<Label>("ArrowPreviewQuantity");
            quantity.Visible = CardCatalog.DesignId(arrow.Card) == "U_CARD_005";
            quantity.Text = $"×{_previewQuantities.GetValueOrDefault(arrow, 1)}";
        }
    }

    private void LayoutHoverPreview(Control anchor, bool animate = false)
    {
        var canvas = GetViewportRect().Size;
        var anchorRect = anchor.GetGlobalRect();
        int focus = _previewIndex - _previewStart;
        var layout = (canvas, anchorRect, focus, _hoverArrows.Count, _previewCards.Count);
        bool obstaclesChanged = false;
        int obstacleCount = 0;
        foreach (var creature in NCombatRoom.Instance!.CreatureNodes)
        {
            if (!creature.Entity.IsAlive) continue;
            var rect = creature.Hitbox.GetGlobalRect().GrowIndividual(24, 110, 24, 12);
            if (obstacleCount == _previewObstacles.Count)
            {
                _previewObstacles.Add(rect);
                obstaclesChanged = true;
            }
            else if (_previewObstacles[obstacleCount] != rect)
            {
                _previewObstacles[obstacleCount] = rect;
                obstaclesChanged = true;
            }
            obstacleCount++;
        }
        if (obstacleCount < _previewObstacles.Count)
        {
            _previewObstacles.RemoveRange(obstacleCount, _previewObstacles.Count - obstacleCount);
            obstaclesChanged = true;
        }
        if (!obstaclesChanged && _previewLayout == layout) return;
        float scale = Math.Clamp((canvas.Y * .8f - 96 - PreviewMargin * 2) / 504, .1f, PreviewScale);
        Span<Vector2> positions = stackalloc Vector2[PreviewCapacity];
        Span<Vector2> scales = stackalloc Vector2[PreviewCapacity];
        Span<float> angles = stackalloc float[PreviewCapacity];
        var bounds = new Rect2();
        // Keep the input area and placement fixed while any card in this fan lifts.
        for (int possibleFocus = 0; possibleFocus < _previewCards.Count; possibleFocus++)
        {
            for (int i = 0; i < _previewCards.Count; i++)
            {
                int distance = Math.Abs(i - possibleFocus);
                var native = HandPosHelper.GetPosition(_previewCards.Count, i);
                var nativeFocus = HandPosHelper.GetPosition(_previewCards.Count, possibleFocus);
                float separation = MathF.Sign(i - possibleFocus) * Math.Max(0, 45 - distance * 10);
                var position = new Vector2(native.X * .35f + separation,
                    distance == 0 ? 0 : 26 + Math.Abs(native.Y - nativeFocus.Y) * .4f);
                var cardScale = Vector2.One * scale * (distance == 0 ? 1 : .78f);
                float angle = distance == 0 ? 0 : Mathf.DegToRad(HandPosHelper.GetAngle(_previewCards.Count, i));
                if (possibleFocus == focus)
                {
                    positions[i] = position;
                    scales[i] = cardScale;
                    angles[i] = angle;
                }
                var half = new Vector2(150, CardCatalog.DesignId(_previewCards[i].Arrow.Card) == "U_CARD_005" ? 245 : 211) * cardScale;
                var extent = new Vector2(Math.Abs(Mathf.Cos(angle)) * half.X + Math.Abs(Mathf.Sin(angle)) * half.Y,
                    Math.Abs(Mathf.Sin(angle)) * half.X + Math.Abs(Mathf.Cos(angle)) * half.Y);
                var rect = new Rect2(position - extent, extent * 2);
                bounds = possibleFocus == 0 && i == 0 ? rect : bounds.Merge(rect);
            }
        }
        float counterHeight = _hoverArrows.Count > 1 ? 36 : 0;
        var size = bounds.Size + new Vector2(PreviewMargin, PreviewMargin + counterHeight);
        var available = new Rect2(new Vector2(PreviewMargin, 96),
            new Vector2(canvas.X - PreviewMargin * 2, canvas.Y * .8f - 96 - PreviewMargin));
        var maximum = (available.End - size).Max(available.Position);
        var center = anchorRect.GetCenter();
        ReadOnlySpan<Vector2> candidates =
        [
            new(center.X - size.X * .5f, anchorRect.Position.Y - size.Y - 24),
            new(center.X - size.X * .5f, anchorRect.End.Y + 24),
            new(anchorRect.Position.X - size.X - 24, center.Y - size.Y * .5f),
            new(anchorRect.End.X + 24, center.Y - size.Y * .5f),
            new(center.X - size.X * .5f, available.Position.Y)
        ];
        float bestScore = float.PositiveInfinity;
        var best = available.Position;
        foreach (var candidate in candidates)
        {
            var position = candidate.Clamp(available.Position, maximum);
            var rect = new Rect2(position, size);
            var overlap = rect.Intersection(anchorRect);
            float score = overlap.Size.X * overlap.Size.Y * 3 + position.DistanceTo(candidate) * 2;
            foreach (var protectedRect in _previewObstacles)
            {
                var intersection = rect.Intersection(protectedRect);
                score += intersection.Size.X * intersection.Size.Y;
            }
            if (score >= bestScore) continue;
            bestScore = score;
            best = position;
        }
        _hoverPreview!.GlobalPosition = best;
        _hoverPreview.Size = size;
        if (animate)
        {
            _previewMotion?.Kill();
            _previewMotion = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        bool moving = _previewMotion?.IsRunning() == true;
        for (int i = 0; i < _previewCards.Count; i++)
        {
            var card = _previewCards[i].Card;
            var position = positions[i] - bounds.Position + Vector2.One * (PreviewMargin * .5f);
            card.ZIndex = 10 - Math.Abs(i - focus);
            if (animate)
            {
                _previewMotion!.TweenProperty(card, "position", position, .12);
                _previewMotion.TweenProperty(card, "scale", scales[i], .12);
                _previewMotion.TweenProperty(card, "rotation", angles[i], .12);
            }
            else if (!moving)
            {
                card.Position = position;
                card.Scale = scales[i];
                card.Rotation = angles[i];
            }
        }
        _previewCount!.Visible = _hoverArrows.Count > 1;
        _previewCount.Text = $"{_previewIndex + 1} / {_hoverArrows.Count}";
        _previewCount.Position = new(positions[focus].X - bounds.Position.X + PreviewMargin * .5f - _previewCount.Size.X * .5f,
            bounds.Size.Y + PreviewMargin * .5f + 4);
        _previewLayout = moving && !animate ? null : layout;
    }

    public override void _Input(InputEvent input)
    {
        if (_hoverPreview?.IsVisibleInTree() != true) return;
        if (input is InputEventMouseMotion)
        {
            _previewHoverSelection = true;
            return;
        }
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape)
            {
                var anchor = _hoverAnchor;
                ClearHoverPreview();
                _dismissedAnchor = anchor;
            }
            else if (key.Keycode is Key.Pageup or Key.Left) StepPreview(-1);
            else if (key.Keycode is Key.Pagedown or Key.Right) StepPreview(1);
            else return;
        }
        else if (input is InputEventMouseButton { Pressed: true } mouse
            && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            StepPreview(mouse.ButtonIndex == MouseButton.WheelDown ? 1 : -1);
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void StepPreview(int direction)
    {
        _previewIndex = Math.Clamp(_previewIndex + direction, 0, _hoverArrows.Count - 1);
        _previewHoverSelection = false;
        if (_previewIndex < _previewStart || _previewIndex >= _previewStart + PreviewCapacity)
            _previewStart = Math.Clamp(_previewIndex - 2, 0, Math.Max(0, _hoverArrows.Count - PreviewCapacity));
        RefreshPreviewFan();
        LayoutHoverPreview(_hoverAnchor!, animate: true);
    }

    private void RemovePreviewCard(ArrowDeployment arrow)
    {
        if (!_previewCache.Remove(arrow, out var card)) return;
        _previewMotion?.Kill();
        _previewMotion = null;
        _previewLayout = null;
        _previewCards.RemoveAll(pair => pair.Arrow == arrow);
        if (_inspectedArrow == arrow) _inspectedArrow = null;
        var hitbox = card.GetNode<Control>("ArrowPreviewHitbox");
        card.RemoveChild(hitbox);
        hitbox.QueueFree();
        var quantity = card.GetNode<Label>("ArrowPreviewQuantity");
        card.RemoveChild(quantity);
        quantity.QueueFree();
        _renderedPreviewStates.Remove(arrow);
        card.ZIndex = 0;
        card.QueueFreeSafely();
    }

    private void ClearHoverPreview()
    {
        _previewLayout = null;
        _previewMotion?.Kill();
        _previewMotion = null;
        _hoverPreview?.Hide();
        _hoverAnchor = null;
        _hoverArrows.Clear();
        _hoverRepresentatives.Clear();
        _previewQuantities.Clear();
        _previewStates.Clear();
        _highlightedArrows.Clear();
        _previewCards.Clear();
        _inspectedArrow = null;
        _previewHoverSelection = true;
        foreach (var card in _previewCache.Values) card.Hide();
    }
}
