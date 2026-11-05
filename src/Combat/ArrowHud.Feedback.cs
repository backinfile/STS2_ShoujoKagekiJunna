using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

internal static class ArrowFeedback
{
    internal static CpuParticles2D CreateFlash(Texture2D texture, float width)
    {
        // Preserve the native Power amount-change shader, scale curve and fade ramp.
        var power = GD.Load<PackedScene>("res://scenes/combat/power.tscn").Instantiate();
        var flash = (CpuParticles2D)power.GetNode<CpuParticles2D>("%PowerFlash").Duplicate();
        power.Free();
        flash.Name = "CountdownFlash";
        flash.Texture = texture;
        flash.LocalCoords = true;
        flash.ScaleAmountMin = flash.ScaleAmountMax = width / texture.GetWidth();
        flash.Emitting = false;
        return flash;
    }

    internal static void PulseCounter(Label label, ref Tween? tween)
    {
        tween?.Kill();
        label.PivotOffset = label.Size * .5f;
        label.Scale = Vector2.One;
        tween = label.CreateTween();
        tween.TweenProperty(label, "scale", Vector2.One * 1.55f, .08).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "scale", Vector2.One, .26).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }
}

public partial class ArrowHud
{
    private static void UpdateCountdown(ArrowDeployment arrow, ArrowView view)
    {
        string remaining = arrow.Remaining.ToString();
        if (view.Count.Text == remaining) return;
        bool changed = view.Count.Text.Length > 0;
        view.Count.Text = remaining;
        if (!changed) return;
        ArrowFeedback.PulseCounter(view.Count, ref view.CountTween);
        view.Flash.Restart();
        view.Flash.Emitting = true;
    }

    internal void CountdownChanged(ArrowDeployment arrow)
    {
        if (_views.TryGetValue(arrow, out var view) && !view.Releasing) UpdateCountdown(arrow, view);
    }

    internal Task ReleaseArrow(ArrowDeployment arrow)
    {
        if (!_views.TryGetValue(arrow, out var view))
        {
            view = CreateArrow(arrow);
            _views.Add(arrow, view);
            view.Root.Position = new Vector2(GetViewportRect().Size.X * .44f, SecondRelicRowY()) - IconSize * .5f;
            view.Count.Position = CounterCenter - view.Count.Size * .5f;
        }
        if (view.Releasing) return Task.CompletedTask;
        FinishDeployment(arrow);
        view.Releasing = true;
        view.Frame.MouseFilter = MouseFilterEnum.Ignore;
        view.Count.Text = "0";
        view.Count.AddThemeColorOverride("font_color", new Color("FFF1AE"));
        ArrowFeedback.PulseCounter(view.Count, ref view.CountTween);
        view.Flash.Restart();
        view.Flash.Emitting = true;
        view.Root.Scale = Vector2.One;
        var origin = view.Root.Position + IconSize * .5f;
        var targets = TargetPoints(arrow);
        if (targets.Length == 0 && arrow.Card.TargetType == TargetType.Self &&
            NCombatRoom.Instance?.GetCreatureNode(arrow.Card.Owner.Creature) is { } owner)
            targets = [LocalPoint(owner.VfxSpawnPosition)];
        view.ExitPosition = GetGlobalTransform() * (targets.Length == 0 ? origin + Vector2.Up * 110
            : targets.Aggregate(Vector2.Zero, (sum, point) => sum + point) / targets.Length);
        view.Count.Position = RotateInFrame(CounterCenter, view.Frame.Rotation) - view.Count.Size * .5f;
        view.Root.PivotOffset = IconSize * .5f;
        var effect = new ArrowReleaseVfx { Name = "ArrowRelease", Position = origin, ZIndex = 6 };
        AddChild(effect);
        view.Root.Reparent(effect);
        view.Root.Position = -IconSize * .5f;
        effect.Initialize(view.Root, targets.Select(point => point - origin).ToArray(), (direction, delta) =>
        {
            view.Motion.Advance(origin, origin + direction, delta);
            view.Frame.Rotation = view.Motion.Angle;
            view.Count.Position = RotateInFrame(CounterCenter, view.Frame.Rotation) - view.Count.Size * .5f;
        });
        return Cmd.Wait(ArrowReleaseVfx.ImpactTime);
    }
}

/// <summary>A visible zero cue, a brief arrow flight, then a star-shaped impact for each target.</summary>
public partial class ArrowReleaseVfx : Node2D
{
    private const float Hold = .14f, Flight = .3f, Duration = .7f;
    internal const float ImpactTime = (Hold + Flight) / ArrowLayoutMotion.SpeedMultiplier;
    private Control _arrow = null!;
    private Vector2 _rest;
    private Vector2[] _targets = [];
    private float _time;
    private Action<Vector2, float>? _turn;

    internal void Initialize(Control arrow, Vector2[] targets, Action<Vector2, float>? turn = null)
    {
        _arrow = arrow;
        _rest = arrow.Position;
        _targets = targets;
        _turn = turn;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta * ArrowLayoutMotion.SpeedMultiplier;
        if (_time >= Duration) { QueueFree(); return; }
        float flight = Mathf.Clamp((_time - Hold) / Flight, 0, 1);
        var aim = _targets.Length == 0 ? new Vector2(0, -110) : _targets.Aggregate(Vector2.Zero, (sum, p) => sum + p) / _targets.Length;
        _turn?.Invoke(aim, (float)delta);
        _arrow.Position = _rest + aim * flight * flight;
        _arrow.Scale = Vector2.One * Mathf.Lerp(1.25f, .65f, flight);
        _arrow.Modulate = new Color(1.5f, 1.5f, 1.25f, 1 - Mathf.SmoothStep(.65f, 1, flight));
        QueueRedraw();
    }

    public override void _Draw()
    {
        float cue = Mathf.Clamp(_time / .3f, 0, 1);
        DrawArc(Vector2.Zero, 12 + cue * 48, 0, Mathf.Tau, 48, new Color(1, .9f, .6f, (1 - cue) * .85f), 2.5f, true);
        float flight = Mathf.Clamp((_time - Hold) / Flight, 0, 1);
        float impact = Mathf.Clamp((_time - Hold - Flight) / (Duration - Hold - Flight), 0, 1);
        foreach (var target in _targets)
        {
            if (_time >= Hold && flight < 1)
            {
                var head = target * flight * flight;
                var tail = target * Math.Max(0, flight * flight - .14f);
                DrawLine(tail, head, new Color(.65f, .88f, 1, .18f), 10, true);
                DrawLine(tail, head, new Color(1, .97f, .76f, .95f), 2.5f, true);
            }
            if (_time < Hold + Flight) continue;
            float radius = 8 + impact * 46;
            var color = new Color(1, .92f, .65f, 1 - impact);
            DrawArc(target, radius * .65f, 0, Mathf.Tau, 32, new Color(color, color.A * .45f), 2, true);
            for (int ray = 0; ray < 8; ray++)
            {
                var direction = Vector2.FromAngle(ray * Mathf.Tau / 8);
                DrawLine(target + direction * radius * .16f, target + direction * radius * (ray % 2 == 0 ? 1 : .55f), color, 3 * (1 - impact) + .5f, true);
            }
        }
    }
}
