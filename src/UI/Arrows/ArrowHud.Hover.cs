using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.UI;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private Control? _hoverPreview;
    private readonly List<(ArrowDeployment Arrow, NCard Card)> _previewCards = [];
    private readonly List<ArrowDeployment> _hoverArrows = [];

    private void UpdateHoverPreview(NCombatRoom room)
    {
        var hovered = GetViewport().GuiGetHoveredControl();
        if (!IsVisibleInTree() || NHoverTipSet.shouldBlockHoverTips || room.Ui.Hand.InCardPlay || hovered == null)
        {
            ClearHoverPreview();
            return;
        }
        var direct = _views.FirstOrDefault(pair => !pair.Value.Releasing &&
            (pair.Value.Frame == hovered || pair.Value.Frame.IsAncestorOf(hovered)));
        var creature = room.CreatureNodes.FirstOrDefault(node => node.Entity.IsAlive &&
            (node.Hitbox == hovered || node.Hitbox.IsAncestorOf(hovered)));
        _hoverArrows.Clear();
        if (direct.Key != null) _hoverArrows.Add(direct.Key);
        else if (creature != null)
            foreach (var arrow in _displayOrder)
                if (arrow.Targets.Contains(creature.Entity)) _hoverArrows.Add(arrow);
        if (_hoverArrows.Count == 0)
        {
            ClearHoverPreview();
            return;
        }
        bool same = _previewCards.Count == _hoverArrows.Count;
        for (int i = 0; same && i < _hoverArrows.Count; i++) same = _previewCards[i].Arrow == _hoverArrows[i];
        if (!same)
        {
            ClearHoverPreview();
            _hoverPreview = new Control { Name = "JunnaArrowHoverPreview", MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_hoverPreview);
            foreach (var arrow in _hoverArrows)
            {
                // Keep physical copies and their combat modifications, even when their card IDs match.
                var card = NCard.Create(arrow.Card)!;
                _hoverPreview.AddChild(card);
                card.SetPreviewTarget(arrow.Play.Target);
                card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                KnowledgePreviewCards.IgnoreMouse(card);
                _previewCards.Add((arrow, card));
            }
        }
        foreach (var (arrow, card) in _previewCards) card.SetPreviewTarget(arrow.Target);
        LayoutHoverPreview(direct.Key != null ? direct.Value.Frame : creature!.Hitbox);
    }

    private void LayoutHoverPreview(Control anchor)
    {
        const float gap = 12, margin = 24;
        var canvas = _hoverPreview!.GetViewportRect().Size;
        int rows = Math.Min(_previewCards.Count, Math.Max(1, (int)((canvas.Y - margin * 2) / (422 * .65f + gap))));
        int columns = (_previewCards.Count + rows - 1) / rows;
        float scale = Math.Min(.65f, Math.Min((canvas.X - margin * 2 - gap * (columns - 1)) / (300 * columns),
            (canvas.Y - margin * 2 - gap * (rows - 1)) / (422 * rows)));
        var cardSize = new Vector2(300, 422) * scale;
        var size = new Vector2(columns * cardSize.X + gap * (columns - 1), rows * cardSize.Y + gap * (rows - 1));
        var rect = anchor.GetGlobalRect();
        // Put cards opposite the creature's native text tips; keep the entire grid on screen.
        float x = rect.GetCenter().X > canvas.X * .5f ? rect.End.X + gap : rect.Position.X - size.X - gap;
        _hoverPreview.GlobalPosition = new Vector2(x, rect.Position.Y).Clamp(Vector2.One * margin, canvas - size - Vector2.One * margin);
        _hoverPreview.Size = size;
        for (int index = 0; index < _previewCards.Count; index++)
        {
            var card = _previewCards[index].Card;
            card.Scale = Vector2.One * scale;
            card.Position = cardSize * .5f + new Vector2(index / rows * (cardSize.X + gap), index % rows * (cardSize.Y + gap));
        }
    }

    private void ClearHoverPreview()
    {
        if (_hoverPreview == null) return;
        _hoverPreview.GetParent().RemoveChild(_hoverPreview);
        _hoverPreview.QueueFree();
        _hoverPreview = null;
        _previewCards.Clear();
    }
}
