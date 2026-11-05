using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.UI;

/// <summary>Next-turn energy and retained energy, attached to the native energy counter.</summary>
public partial class EnergyStatusBar : Control
{
    private const int FontSize = 22;
    private const float ArrowSize = 22;
    private static readonly Vector2 BarSize = new(128, 30);
    private static readonly Color WarmGray = new("BCA98C");
    internal static readonly Color BlueGray = new("86A6B0");
    private Player _player = null!;
    private Label _energyLabel = null!;
    private Label _next = null!;
    private Control _retained = null!;
    private RetentionArrow _arrow = null!;
    private Label _retainedAmount = null!;
    private int _nextAmount = -1;
    private int _reserveAmount = -1;

    internal static void Attach(NEnergyCounter counter, Player player)
    {
        if (counter.GetNodeOrNull<EnergyStatusBar>("JunnaEnergyStatus") != null) return;
        counter.AddChild(new EnergyStatusBar
        {
            Name = "JunnaEnergyStatus", _player = player,
            _energyLabel = counter.GetNode<Label>("Label")
        });
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = BarSize;
        ZIndex = -1;
        _next = MakeLabel("NextTurn", WarmGray);
        AddChild(_next);
        _retained = new Control { Name = "Retained", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_retained);
        _arrow = new RetentionArrow { Name = "Arrow", Size = new(ArrowSize, ArrowSize), MouseFilter = MouseFilterEnum.Ignore };
        _retained.AddChild(_arrow);
        _retainedAmount = MakeLabel("Amount", BlueGray);
        _retained.AddChild(_retainedAmount);
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        var counter = (Control)GetParent();
        var center = counter.GetGlobalTransform().AffineInverse() * _energyLabel.GetGlobalRect().GetCenter();
        Position = center + new Vector2(-BarSize.X * .5f, 64);
        int next = Math.Max(0, _player.Creature.GetPowerAmount<EnergyNextTurnPower>());
        int reserve = Math.Max(0, _player.Creature.GetPowerAmount<EnergyReserve>());
        if (next == _nextAmount && reserve == _reserveAmount) return;
        _nextAmount = next;
        _reserveAmount = reserve;
        Visible = next > 0 || reserve > 0;
        _next.Visible = next > 0;
        _retained.Visible = reserve > 0;
        bool both = next > 0 && reserve > 0;
        float available = both ? 54 : 112;
        if (next > 0)
        {
            string text = $"+{next}";
            FitText(_next, text, available);
            float x = both ? BarSize.X * .25f : BarSize.X * .5f;
            _next.Size = new(available, BarSize.Y);
            _next.Position = new(x - available * .5f, 0);
        }
        if (reserve > 0)
        {
            string text = reserve.ToString();
            FitText(_retainedAmount, text, available - ArrowSize - 2);
            float width = TextWidth(_retainedAmount) + 2;
            float groupWidth = ArrowSize + 2 + width;
            float x = both ? BarSize.X * .75f : BarSize.X * .5f;
            _retained.Size = new(groupWidth, BarSize.Y);
            _retained.Position = new(x - groupWidth * .5f, 0);
            _arrow.Position = new(0, (BarSize.Y - ArrowSize) * .5f);
            _retainedAmount.Size = new(width, BarSize.Y);
            _retainedAmount.Position = new(ArrowSize + 2, 0);
        }
    }

    private Label MakeLabel(string name, Color color)
    {
        var label = new Label
        {
            Name = name, MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontOverride("font", _energyLabel.GetThemeFont("font"));
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color("08121C99"));
        label.AddThemeConstantOverride("outline_size", 2);
        return label;
    }

    private static void FitText(Label label, string text, float available)
    {
        float width = label.GetThemeFont("font").GetStringSize(text, fontSize: FontSize).X;
        label.AddThemeFontSizeOverride("font_size", Math.Min(FontSize, Math.Max(12, (int)(FontSize * available / Math.Max(1, width)))));
        label.Text = text;
    }

    private static float TextWidth(Label label) => label.GetThemeFont("font")
        .GetStringSize(label.Text, fontSize: label.GetThemeFontSize("font_size")).X;
}

/// <summary>A drawn clockwise arrow stays crisp without relying on font glyph coverage.</summary>
public partial class RetentionArrow : Control
{
    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, Size / 32);
        var center = new Vector2(16, 16);
        float end = Mathf.DegToRad(315);
        var radial = Vector2.FromAngle(end);
        var tangent = radial.Rotated(Mathf.Pi * .5f);
        var tip = center + radial * 10 + tangent * 4;
        var back = tip - tangent * 9;
        Vector2[] head = [tip, back + radial * 6, back - radial * 6];
        var centroid = head.Aggregate(Vector2.Zero, (sum, p) => sum + p) / 3;
        var shadow = new Color("08121C99");
        DrawArc(center, 10, Mathf.Pi * .25f, end, 40, shadow, 7, true);
        DrawColoredPolygon(head.Select(p => centroid + (p - centroid) * 1.25f).ToArray(), shadow);
        DrawArc(center, 10, Mathf.Pi * .25f, end, 40, EnergyStatusBar.BlueGray, 4.2f, true);
        DrawColoredPolygon(head, EnergyStatusBar.BlueGray);
    }
}

[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
internal static class EnergyStatusBarPatch
{
    private static void Postfix(NEnergyCounter __instance, Player ____player)
        => EnergyStatusBar.Attach(__instance, ____player);
}
