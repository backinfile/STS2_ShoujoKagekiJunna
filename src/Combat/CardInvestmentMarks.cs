using System.Globalization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>A fitted portrait-bottom rail for damage bonus and added hits.</summary>
public partial class CardInvestmentMarks : Control
{
    private const int IndividualHitLimit = 3;
    private NCard? _cardNode;
    private CardModel? _arrowCard;
    private Label _bonusLabel = null!;
    private Label _hitCountLabel = null!;
    private decimal _bonus = -1;
    private int _hits = -1;
    private float _badgeWidth, _hitsWidth, _bonusX, _hitsX;
    private bool _arrowFrame;
    private bool Compact => _arrowCard != null;
    private float Unit => Compact ? .48f : 1;

    internal static void Attach(NCard cardNode)
    {
        if (!cardNode.IsNodeReady() || cardNode.Body == null) return;
        var marks = cardNode.Body.GetNodeOrNull<CardInvestmentMarks>("JunnaInvestmentMarks");
        if (marks == null)
        {
            marks = new CardInvestmentMarks { Name = "JunnaInvestmentMarks", _cardNode = cardNode };
            cardNode.Body.AddChild(marks);
        }
        marks.Refresh();
    }

    internal static CardInvestmentMarks ForArrow(CardModel card) => new()
    {
        Name = "JunnaInvestmentMarks", _arrowCard = card
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        // Normal cards share their body's draw order, so foreground cards cover the badges.
        ZIndex = Compact ? 2 : 0;
        _bonusLabel = new Label
        {
            Name = "DamageBonus", MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        _bonusLabel.AddThemeFontSizeOverride("font_size", Compact ? 14 : 26);
        _bonusLabel.AddThemeColorOverride("font_color", new Color("FFE4A3"));
        _bonusLabel.AddThemeColorOverride("font_outline_color", new Color("142438"));
        _bonusLabel.AddThemeConstantOverride("outline_size", Compact ? 2 : 3);
        AddChild(_bonusLabel);
        _hitCountLabel = new Label
        {
            Name = "ExtraHitCount", MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        _hitCountLabel.AddThemeFontSizeOverride("font_size", Compact ? 12 : 24);
        _hitCountLabel.AddThemeColorOverride("font_color", new Color("D7EEFF"));
        _hitCountLabel.AddThemeColorOverride("font_outline_color", new Color("142438"));
        _hitCountLabel.AddThemeConstantOverride("outline_size", Compact ? 2 : 3);
        AddChild(_hitCountLabel);
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        if (!IsNodeReady()) return;
        var card = _arrowCard ?? _cardNode?.Model;
        bool revealed = _cardNode == null || _cardNode.Visibility == ModelVisibility.Visible;
        var snapshot = card != null && revealed ? DamageModifiers.CaptureDamageModifierSnapshot(card) : default;
        decimal bonus = Compact ? 0 : snapshot.DamageBonus;
        int hits = DamageModifiers.ExtraHits(snapshot);
        bool arrowFrame = card != null && CardCatalog.IsArrow(card);
        if (_bonus == bonus && _hits == hits && _arrowFrame == arrowFrame) return;
        _bonus = bonus;
        _hits = hits;
        _arrowFrame = arrowFrame;
        Visible = _bonus != 0 || _hits > 0;
        _bonusLabel.Visible = _bonus != 0;
        _bonusLabel.Text = _bonus == 0 ? "" : "+" + _bonus.ToString("0.############################", CultureInfo.InvariantCulture);
        _badgeWidth = _bonus != 0 ? Math.Max(56 * Unit, TextWidth(_bonusLabel) + 22 * Unit) : 0;
        _hitCountLabel.Visible = _hits > IndividualHitLimit;
        _hitCountLabel.Text = _hitCountLabel.Visible ? _hits.ToString(CultureInfo.InvariantCulture) : "";
        _hitsWidth = _hits <= 0 ? 0 : _hitCountLabel.Visible
            ? TextWidth(_hitCountLabel) + 38 * Unit
            : Math.Max(40, 10 + 24 * _hits) * Unit;
        float gap = _bonus != 0 && _hits > 0 ? 2 * Unit : 0;
        float width = _badgeWidth + gap + _hitsWidth;
        _bonusX = 0;
        _hitsX = Compact ? -_hitsWidth : _badgeWidth + gap;
        // Keep the rail inside the portrait even when both values have many digits.
        float scale = Compact ? 1 : Math.Min(1, 244 / Math.Max(1, width));
        Scale = Vector2.One * scale;
        // Fit along the arrow frame's lower edge while clearing the turn-number label.
        Position = Compact ? new(-8, 77) : new(-122, (_arrowFrame ? -16 : -1) - 32 * scale);
        _bonusLabel.Position = new(_bonusX, 0);
        _bonusLabel.Size = new(_badgeWidth, 32 * Unit);
        _hitCountLabel.Position = new(_hitsX + 28 * Unit, 0);
        // Reset label size so a previously large count can shrink with its segment.
        _hitCountLabel.Size = new(Math.Max(0, _hitsWidth - 34 * Unit), 32 * Unit);
        QueueRedraw();
    }

    private static float TextWidth(Label label) => label.GetThemeFont("font").GetStringSize(
        label.Text, HorizontalAlignment.Left, -1, label.GetThemeFontSize("font_size")).X;

    public override void _Draw()
    {
        float unit = Unit;
        if (_bonus != 0)
            DrawBadge(new(_bonusX, 0), new(_badgeWidth, 32 * unit), new Color("D0B775"));
        if (_hits <= 0) return;
        DrawBadge(new(_hitsX, 0), new(_hitsWidth, 32 * unit), new Color("91A9B6"));
        // Large counts occupy one row and perform a fixed amount of drawing, even at int.MaxValue.
        int marks = _hits > IndividualHitLimit ? 1 : _hits;
        float first = _hitCountLabel.Visible ? 8 * unit : (_hitsWidth - ((marks - 1) * 24 + 18) * unit) * .5f;
        for (int i = 0; i < marks; i++)
        {
            var p = new Vector2(_hitsX + first + i * 24 * unit, 3 * unit);
            Vector2[] outline = [new(1, 2), new(9, 6), new(17, 2), new(9, 24)];
            Vector2[] inset = [new(4, 6), new(9, 9), new(14, 6), new(9, 19)];
            DrawColoredPolygon(outline.Select(v => p + v * unit).ToArray(), new Color("162A3E"));
            DrawPolyline([..outline.Select(v => p + v * unit), p + outline[0] * unit], new Color("D7EEFF"), 1.5f * unit, true);
            DrawColoredPolygon(inset.Select(v => p + v * unit).ToArray(), new Color("77C7F0"));
        }
    }

    private void DrawBadge(Vector2 position, Vector2 size, Color border)
    {
        float w = size.X, h = size.Y, cut = 9 * Unit;
        Vector2[] badge = [new(cut, 0), new(w, 0), new(w, h - cut), new(w - cut, h), new(0, h), new(0, cut)];
        badge = badge.Select(p => p + position).ToArray();
        DrawColoredPolygon(badge.Select(p => p + new Vector2(0, 2 * Unit)).ToArray(), new Color(0, 0, 0, .45f));
        DrawColoredPolygon(badge, new Color("1D2A34"));
        DrawPolyline([..badge, badge[0]], new Color("565E64"), 4 * Unit, true);
        DrawPolyline([..badge, badge[0]], border, 1.5f * Unit, true);
        DrawLine(position + new Vector2(cut + Unit, 2 * Unit),
            position + new Vector2(w - 2 * Unit, 2 * Unit), new Color(1, 1, 1, .16f), Unit, true);
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class CardInvestmentMarksPatch
{
    private static void Postfix(NCard __instance)
    {
        CardInvestmentMarks.Attach(__instance);
        CardBonusVfx.Attach(__instance);
    }
}
