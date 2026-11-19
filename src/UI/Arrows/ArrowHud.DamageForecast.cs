using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<Creature, ArrowHealthForecastView> _damageForecasts = [];
    private double _nextDamageForecast;

    private void RefreshDamageForecasts(NCombatRoom room)
    {
        if (!CombatManager.Instance.IsInProgress || !ArrowCombat.Deployed.Any(IsWaitingArrow))
        {
            foreach (var view in _damageForecasts.Values)
                if (GodotObject.IsInstanceValid(view)) view.Hide();
            _nextDamageForecast = 0;
            return;
        }

        if (_elapsed < _nextDamageForecast) return;
        _nextDamageForecast = _elapsed + .10;
        foreach (var (creature, view) in _damageForecasts.ToArray())
        {
            if (!GodotObject.IsInstanceValid(view) || !creature.IsAlive)
            {
                if (GodotObject.IsInstanceValid(view)) view.QueueFree();
                _damageForecasts.Remove(creature);
            }
        }

        var waiting = ArrowCombat.ResolutionOrder().Where(IsWaitingArrow).ToArray();
        var batches = waiting.GroupBy(arrow => arrow.Remaining).OrderBy(batch => batch.Key).ToArray();
        HashSet<Creature> shown = [];
        foreach (var creature in waiting.SelectMany(arrow => arrow.Targets).Where(target => target.IsEnemy).Distinct())
        {
            var forecast = ArrowDamageForecast.ForTarget(creature, batches);
            if (forecast.Length == 0) continue;
            if (!_damageForecasts.TryGetValue(creature, out var view))
            {
                var display = room.GetCreatureNode(creature)?.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
                var bar = display?.GetNodeOrNull<NHealthBar>("%HealthBar");
                if (bar?.HpBarContainer == null) continue;
                var fill = bar.GetNodeOrNull<Control>("%HpForegroundContainer");
                var hpLabel = bar.GetNodeOrNull<Label>("%HpLabel");
                if (fill == null || hpLabel == null) continue;
                view = new ArrowHealthForecastView
                {
                    Name = "JunnaArrowDamageForecast", MouseFilter = MouseFilterEnum.Ignore,
                    Fill = fill, NumberFont = hpLabel.GetThemeFont("font"), ArrowFrame = _frame
                };
                var container = bar.HpBarContainer;
                container.AddChild(view);
                if (hpLabel.GetParent() == container) container.MoveChild(view, hpLabel.GetIndex());
                _damageForecasts.Add(creature, view);
            }
            view.Display(creature.CurrentHp, creature.MaxHp, forecast);
            shown.Add(creature);
        }
        foreach (var (creature, view) in _damageForecasts)
            if (!shown.Contains(creature)) view.Hide();
    }

    private static bool IsWaitingArrow(ArrowDeployment arrow) => !arrow.Resolving && arrow.Remaining > 0;

    private void ClearDamageForecasts()
    {
        foreach (var view in _damageForecasts.Values)
            if (GodotObject.IsInstanceValid(view)) view.QueueFree();
        _damageForecasts.Clear();
    }
}

internal sealed partial class ArrowHealthForecastView : Control
{
    private const float SegmentHeight = 3;
    private const float SegmentTop = -SegmentHeight;
    private const float ArrowWidth = 23;
    private const float LabelGap = 4;
    private const float BatchGap = 2;
    private const float LabelInset = 5;
    private const int TurnSize = 14;
    private const int DamageSize = 20;

    private static readonly Color Ivory = new("EFE4D0");
    private static readonly Color Later = new("A99887");
    private static readonly Color Outline = new("251C23");
    private static readonly Color ArrowInterior = new("222D36");
    private static readonly float FrameScale = ArrowWidth / ArrowHud.FrameSize.X;
    private static readonly Vector2 FrameDimensions = ArrowHud.FrameSize * FrameScale;
    private static readonly Vector2 FrameOrigin = new(0, -FrameDimensions.Y);
    private static readonly Vector2 FrameCenter = FrameOrigin + FrameDimensions * .5f;
    private static readonly Vector2[] FrameInterior =
        ArrowHud.PortraitWindow.Select(point => FrameOrigin + point * FrameScale).ToArray();

    internal Control Fill { get; init; } = null!;
    internal Font NumberFont { get; init; } = null!;
    internal Texture2D ArrowFrame { get; init; } = null!;
    private int _currentHp;
    private int _maxHp;
    private ArrowDamageForecast.Batch[] _batches = [];

    private readonly record struct BatchLabel(float Center, string Turns, string Damage, float Width, Color Tint);

    public override void _Ready() => Fill.ItemRectChanged += QueueRedraw;

    public override void _ExitTree()
    {
        if (IsInstanceValid(Fill)) Fill.ItemRectChanged -= QueueRedraw;
    }

    internal void Display(int currentHp, int maxHp, ArrowDamageForecast.Batch[] batches)
    {
        if (_currentHp != currentHp || _maxHp != maxHp || !_batches.AsSpan().SequenceEqual(batches))
        {
            _currentHp = currentHp;
            _maxHp = maxHp;
            _batches = batches;
            QueueRedraw();
        }
        Show();
    }

    public override void _Draw()
    {
        if (_maxHp <= 0 || Fill.Size.X <= 0 || _batches.Length == 0) return;

        float fillLeft = Fill.Position.X;
        float hpWidth = Fill.Size.X * Mathf.Clamp((float)_currentHp / _maxHp, 0, 1);
        float right = fillLeft + hpWidth;
        // The batches are independent snapshots. Fit the rail inside current HP even when their sum is larger.
        long total = _batches.Sum(batch => (long)batch.Loss);
        float unit = hpWidth / Math.Max(_currentHp, total);
        var labels = new BatchLabel[_batches.Length];
        for (int i = 0; i < _batches.Length; i++)
        {
            var batch = _batches[i];
            float left = Mathf.Max(fillLeft, right - batch.Loss * unit);
            float width = right - left;
            var tint = Ivory.Lerp(Later, Mathf.Clamp((batch.Turns - 1) * .28f, 0, 1));
            if (width > 0)
            {
                float cut = i > 0 ? Mathf.Min(1.8f, width * .35f) : 0;
                DrawRect(new Rect2(left, SegmentTop, width, SegmentHeight), Outline);
                DrawRect(new Rect2(left, SegmentTop, width - cut, SegmentHeight), tint);
            }

            // Keep the compact marker to one digit. The overhead arrow still has the full countdown.
            string turns = batch.Turns is > 0 and <= 9
                ? batch.Turns.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
            string damage = batch.Loss.ToString(System.Globalization.CultureInfo.InvariantCulture);
            float labelWidth = LabelInset * 2 + ArrowWidth + LabelGap + NumberFont.GetStringSize(damage, fontSize: DamageSize).X;
            labels[i] = new((left + right) * .5f, turns, damage, labelWidth, tint);
            right = left;
        }

        // Pack from the latest HP edge toward the left, reserving room for the other labels.
        float fullWidth = labels.Sum(label => label.Width) + BatchGap * (labels.Length - 1);
        float scale = Mathf.Min(1, Fill.Size.X / fullWidth);
        float remaining = fullWidth * scale;
        right = fillLeft + Fill.Size.X;
        foreach (var label in labels)
        {
            float width = label.Width * scale;
            float latest = right - width;
            float earliest = Mathf.Min(latest, fillLeft + remaining - width);
            float left = Mathf.Clamp(label.Center - (LabelInset + ArrowWidth * .5f) * scale, earliest, latest);
            DrawBatchLabel(label, left, scale);
            right = left - BatchGap * scale;
            remaining -= width + BatchGap * scale;
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void DrawBatchLabel(BatchLabel label, float left, float scale)
    {
        // The drawn glyphs use the same scale as the width reserved by the row layout.
        DrawSetTransform(new(left + LabelInset * scale, SegmentTop), 0, Vector2.One * scale);
        DrawColoredPolygon(FrameInterior, ArrowInterior);
        DrawTextureRect(ArrowFrame, new Rect2(FrameOrigin, FrameDimensions), false);

        int countSize = TurnSize;
        float countWidth = NumberFont.GetStringSize(label.Turns, fontSize: countSize).X;
        const float countRoom = 8;
        if (countWidth > countRoom)
        {
            countSize = Math.Max(3, (int)(TurnSize * countRoom / countWidth));
            countWidth = NumberFont.GetStringSize(label.Turns, fontSize: countSize).X;
        }

        float CenteredBaseline(int size) =>
            FrameCenter.Y + (NumberFont.GetAscent(size) - NumberFont.GetDescent(size)) * .5f;

        var turns = new Vector2(FrameCenter.X - countWidth * .5f, CenteredBaseline(countSize));
        DrawStringOutline(NumberFont, turns, label.Turns, fontSize: countSize, size: 1, modulate: Outline);
        DrawString(NumberFont, turns, label.Turns, fontSize: countSize, modulate: Ivory);

        var damage = new Vector2(ArrowWidth + LabelGap, CenteredBaseline(DamageSize));
        DrawStringOutline(NumberFont, damage, label.Damage, fontSize: DamageSize, size: 4, modulate: Outline);
        DrawString(NumberFont, damage, label.Damage, fontSize: DamageSize, modulate: label.Tint);
    }
}
