using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<ArrowDeployment, ArrowDeploymentVfx> _deployments = [];

    internal bool AnimateDeployment(Tween tween, CardModel card, NCard cardNode)
    {
        var arrow = ArrowCombat.Find(card);
        if (arrow == null) return false;
        FinishDeployment(arrow);
        var effect = new ArrowDeploymentVfx { Name = "ArrowStringSnap", ZIndex = 5 };
        AddChild(effect);
        effect.Initialize(cardNode, LocalPoint(cardNode.Body.GetGlobalRect().GetCenter()));
        _deployments.Add(arrow, effect);
        if (!_views.TryGetValue(arrow, out var view))
        {
            view = CreateArrow(arrow);
            _views.Add(arrow, view);
        }
        view.Root.Visible = false;
        // BaseLib owns the pile transfer tween. Completing this segment also completes the transfer.
        tween.Parallel().TweenMethod(Callable.From<float>(progress =>
        {
            if (GodotObject.IsInstanceValid(effect)) effect.SetProgress(progress);
        }), 0f, 1f, ArrowDeploymentVfx.Duration);
        tween.Parallel().TweenCallback(Callable.From(() => FinishDeployment(arrow)))
            .SetDelay(ArrowDeploymentVfx.Duration);
        return true;
    }

    private void UpdateDeployment(ArrowDeployment arrow, ArrowView view)
    {
        if (!_deployments.TryGetValue(arrow, out var effect)) return;
        var destination = view.Root.Position + IconSize * .5f;
        effect.UpdateDestination(destination);
        view.Root.Visible = effect.ArrowVisible;
        float flight = effect.FlightProgress;
        float scale = Mathf.Lerp(1.7f, 1, flight);
        view.Root.Scale = Vector2.One * scale;
        view.Root.Position = effect.FlightPosition(flight) - IconSize * scale * .5f;
        float movingAngle = effect.FlightDirection.Angle() - Mathf.Pi * .5f;
        float settle = Mathf.SmoothStep(.55f, 1, flight);
        view.Frame.Rotation = Mathf.LerpAngle(movingAngle, view.Frame.Rotation, settle);
    }

    private void FinishDeployment(ArrowDeployment arrow)
    {
        if (!_deployments.Remove(arrow, out var effect)) return;
        if (GodotObject.IsInstanceValid(effect))
        {
            effect.ReleaseCard();
            effect.QueueFree();
        }
        if (_views.TryGetValue(arrow, out var view))
        {
            view.Root.Scale = Vector2.One;
            view.Root.Visible = true;
        }
    }

    public override void _ExitTree()
    {
        foreach (var arrow in _deployments.Keys.ToArray()) FinishDeployment(arrow);
    }
}

/// <summary>A taut bowstring masks a hard card-to-arrow cut, followed by a short flight.</summary>
public partial class ArrowDeploymentVfx : Control
{
    public const double Duration = .32;
    private const float ReleaseTime = .10f;
    private const float ArrowTime = .115f;
    private NCard? _cardNode;
    private Vector2 _cardStartPosition;
    private Vector2 _start;
    private Vector2 _stringOrigin;
    private Vector2 _origin;
    private Vector2 _destination;
    private float _time;
    public bool ArrowVisible => _time >= ArrowTime;
    public float FlightProgress => Mathf.Clamp((_time - ArrowTime) / ((float)Duration - ArrowTime), 0, 1);
    public Vector2 FlightDirection => (_destination - _origin).Normalized();

    internal void Initialize(NCard cardNode, Vector2 origin)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _cardNode = cardNode;
        _cardStartPosition = cardNode.GlobalPosition;
        _start = _stringOrigin = origin;
        // Manual play has only just left the hand. Lift the intact card clear of it during the draw.
        var canvas = GetViewportRect().Size;
        _origin = new(Mathf.Clamp(origin.X, canvas.X * .25f, canvas.X * .75f), Math.Min(origin.Y, canvas.Y * .58f));
        _destination = origin + Vector2.Up * 300;
    }

    internal void SetProgress(float progress)
    {
        _time = progress * (float)Duration;
        float lift = 1 - Mathf.Pow(1 - Mathf.Clamp(_time / .08f, 0, 1), 3);
        _stringOrigin = _start.Lerp(_origin, lift);
        if (_cardNode != null && GodotObject.IsInstanceValid(_cardNode))
        {
            var transform = GetGlobalTransform();
            _cardNode.GlobalPosition = _cardStartPosition + transform * _stringOrigin - transform * _start;
        }
        if (_time >= ReleaseTime) ReleaseCard();
        QueueRedraw();
    }

    internal void UpdateDestination(Vector2 destination) => _destination = destination;

    internal Vector2 FlightPosition(float progress)
    {
        float eased = 1 - Mathf.Pow(1 - progress, 2);
        var normal = FlightDirection.Orthogonal();
        return _origin.Lerp(_destination, eased) + normal * Mathf.Sin(eased * Mathf.Pi) * 24;
    }

    internal void ReleaseCard()
    {
        if (_cardNode == null) return;
        if (GodotObject.IsInstanceValid(_cardNode))
        {
            _cardNode.Visible = false;
            _cardNode.QueueFreeSafely();
        }
        _cardNode = null;
    }

    public override void _ExitTree() => ReleaseCard();

    public override void _Draw()
    {
        var forward = FlightDirection;
        var side = forward.Orthogonal();
        float draw = Mathf.Clamp(_time / ReleaseTime, 0, 1);
        float snap = Mathf.Clamp((_time - ReleaseTime) / .055f, 0, 1);
        float stringAlpha = _time < ReleaseTime ? Mathf.Sin(draw * Mathf.Pi * .5f) : 1 - snap;
        if (stringAlpha > 0)
        {
            var left = _stringOrigin + side * 82 + forward * 42;
            var right = _stringOrigin - side * 82 + forward * 42;
            var notch = _stringOrigin - forward * Mathf.Lerp(8, 42, draw) * (1 - snap);
            Vector2[] points = [left, notch, right];
            DrawPolyline(points, new Color(.38f, .78f, 1, stringAlpha * .2f), 7, true);
            DrawPolyline(points, new Color(.74f, .94f, 1, stringAlpha), 1.5f, true);
        }
        float flash = 1 - Mathf.Clamp(Mathf.Abs(_time - .112f) / .035f, 0, 1);
        if (flash > 0)
        {
            DrawLine(_origin - forward * 70, _origin + forward * 70, new Color(.5f, .85f, 1, flash * .4f), 9, true);
            DrawLine(_origin - forward * 70, _origin + forward * 70, new Color(.92f, 1, 1, flash), 2, true);
            DrawLine(_origin - side * 20, _origin + side * 20, new Color(.8f, .97f, 1, flash), 1.5f, true);
        }
        if (!ArrowVisible) return;
        float flight = FlightProgress;
        const int segments = 10;
        var trail = new Vector2[segments + 1];
        var colors = new Color[segments + 1];
        float tail = Math.Max(0, flight - .22f);
        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments;
            trail[i] = FlightPosition(Mathf.Lerp(tail, flight, u));
            colors[i] = new Color(.6f, .88f, 1, u * .65f * (1 - flight));
        }
        DrawPolylineColors(trail, colors, 2, true);
    }
}
