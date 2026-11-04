using System.Globalization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ShoujoKagekiJunna.Combat;

/// <summary>One physical arrowhead per added hit; native multi-hit card text remains independent.</summary>
public partial class CardInvestmentMarks : Control
{
    private NCard? _cardNode;
    private CardModel? _arrowCard;
    private Label _bonusLabel = null!;
    private decimal _bonus = -1;
    private int _hits = -1;
    private float _badgeWidth;
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
        ZIndex = 2;
        _bonusLabel = new Label
        {
            Name = "DamageBonus", MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        _bonusLabel.AddThemeFontSizeOverride("font_size", Compact ? 14 : 28);
        _bonusLabel.AddThemeColorOverride("font_color", new Color("FFE4A3"));
        _bonusLabel.AddThemeColorOverride("font_outline_color", new Color("142438"));
        _bonusLabel.AddThemeConstantOverride("outline_size", Compact ? 2 : 3);
        AddChild(_bonusLabel);
        Position = Compact ? new(-8, 77) : new(120, -138);
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
        if (_bonus == bonus && _hits == hits) return;
        _bonus = bonus;
        _hits = hits;
        Visible = _bonus != 0 || _hits > 0;
        _bonusLabel.Visible = _bonus != 0;
        _bonusLabel.Text = _bonus == 0 ? "" : "+" + _bonus.ToString("0.############################", CultureInfo.InvariantCulture);
        _badgeWidth = Math.Max(52, 22 + _bonusLabel.Text.Length * 17) * Unit;
        _bonusLabel.Position = Compact ? Vector2.Zero : new(-_badgeWidth, 0);
        _bonusLabel.Size = new(_badgeWidth, 36 * Unit);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float unit = Unit;
        if (_bonus != 0)
        {
            float w = _badgeWidth, h = 36 * unit, cut = 7 * unit;
            Vector2[] badge = [new(cut, 0), new(w, 0), new(w, h - cut), new(w - cut, h), new(0, h), new(0, cut)];
            if (!Compact) badge = badge.Select(p => p - new Vector2(w, 0)).ToArray();
            DrawColoredPolygon(badge, new Color(.045f, .09f, .14f, .94f));
            DrawPolyline([..badge, badge[0]], new Color("D0B775"), 1.5f * unit, true);
        }
        // Wrap individual marks instead of replacing them with a multiplier or overflow number.
        int columns = Compact ? 6 : 3;
        float startY = _bonus != 0 ? 43 * unit : 0;
        for (int i = 0; i < _hits; i++)
        {
            float x = Compact ? i % columns * 24 * unit : -17 - i % columns * 24;
            var p = new Vector2(x, startY + i / columns * 28 * unit);
            Vector2[] outline = [new(1, 2), new(9, 6), new(17, 2), new(9, 24)];
            Vector2[] inset = [new(4, 6), new(9, 9), new(14, 6), new(9, 19)];
            DrawColoredPolygon(outline.Select(v => p + v * unit).ToArray(), new Color("162A3E"));
            DrawPolyline([..outline.Select(v => p + v * unit), p + outline[0] * unit], new Color("D7EEFF"), 1.5f * unit, true);
            DrawColoredPolygon(inset.Select(v => p + v * unit).ToArray(), new Color("77C7F0"));
        }
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class CardInvestmentMarksPatch
{
    private static void Postfix(NCard __instance) => CardInvestmentMarks.Attach(__instance);
}
