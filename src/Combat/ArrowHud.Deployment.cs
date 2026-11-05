using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<ArrowDeployment, ArrowDeploymentVfx> _deployments = [];

    internal bool AnimateDeployment(Tween tween, CardModel card, NCard cardNode)
    {
        var arrow = ArrowCombat.Find(card);
        if (arrow == null) return false;
        FinishDeployment(arrow);
        var effect = new ArrowDeploymentVfx { Name = "ArrowPaperFold", ZIndex = 5 };
        AddChild(effect);
        effect.Initialize(cardNode);
        _deployments.Add(arrow, effect);
        if (!_views.TryGetValue(arrow, out var view))
        {
            view = CreateArrow(arrow);
            _views.Add(arrow, view);
        }
        view.Root.Visible = false;
        effect.SetProgress(0);
        QueueRedraw();
        // BaseLib owns the pile transfer tween. Completing this segment also completes the transfer.
        tween.Parallel().TweenMethod(Callable.From<float>(progress =>
        {
            if (GodotObject.IsInstanceValid(effect)) effect.SetProgress(progress);
        }), 0f, 1f, effect.Duration);
        tween.Parallel().TweenCallback(Callable.From(() => FinishDeployment(arrow)))
            .SetDelay(effect.Duration);
        return true;
    }

    private void UpdateDeployment(ArrowDeployment arrow, ArrowView view, Vector2 aim)
    {
        if (!_deployments.TryGetValue(arrow, out var effect)) return;
        var destination = view.Root.Position + IconSize * .5f;
        effect.UpdateAim(destination, aim);
        view.Root.Visible = effect.ArrowVisible;
        float scale = Mathf.Lerp(1.12f, 1, effect.DrawProgress);
        view.Root.Scale = Vector2.One * scale;
        view.Root.Modulate = new Color(1, 1, 1, effect.ArrowAlpha);
        view.Root.Position = effect.CurrentPosition - IconSize * scale * .5f;
        view.Frame.Rotation = effect.ArrowDirection.Angle() - Mathf.Pi * .5f;
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
            view.Root.Modulate = Colors.White;
            view.Root.Visible = true;
        }
    }

    public override void _ExitTree()
    {
        ClearHoverPreview();
        foreach (var arrow in _deployments.Keys.ToArray()) FinishDeployment(arrow);
    }
}

/// <summary>The card circles behind a fixed bow, becomes an arrow while feeding forward, then draws the string.</summary>
public partial class ArrowDeploymentVfx : Control
{
    // Selected by the isolated preview mod; each deployment captures its own choice.
    internal static int PreviewTrajectory { get; set; } = 2;
    private int _trajectory;
    private const float StageTime = .14f;
    private float FeedTime => _trajectory switch { 1 => .76f, 2 => .64f, 3 => .44f, _ => .90f };
    private float ArrowTime => FeedTime - .015f;
    private float CardGoneTime => FeedTime + .065f;
    private float DockTime => FeedTime + .28f;
    private float DrawTime => DockTime + .09f;
    private float TightenTime => DrawTime + .18f;
    private float DrawEndTime => DrawTime + .40f;
    private float HoldEndTime => DrawEndTime + .40f;
    public double Duration => HoldEndTime + .24f;
    private const float ShaftLength = 160;
    private const float BowGripOffset = 36;
    private const float BraceHeight = 10;
    private const string DrawSfx = "event:/sfx/enemy/enemy_attacks/crossbow_ruby_raider/crossbow_ruby_raider_reload";
    private const string FullDrawSfx = "event:/sfx/ui/cards/card_impact_into_single";
    private static Texture2D _foldFrames = null!;
    private NCard? _cardNode;
    private Control _trailAnchor = null!;
    private NCardTrailVfx? _trail;
    private Vector2 _cardCenter;
    private Vector2 _cardScale;
    private float _cardRotation;
    private Vector2 _cardSize;
    private Color _cardModulate;
    private Vector2 _start;
    private Vector2 _stage;
    private Vector2 _canvas;
    private Curve2D _flightPath = null!;
    private Vector2 _destination;
    private Vector2 _aim;
    private bool _aimLocked;
    private float _time;
    public bool ArrowVisible => _time >= ArrowTime;
    public float ArrowAlpha => Mathf.SmoothStep(ArrowTime, CardGoneTime, _time);
    // Pull most of the distance in one stroke, then slowly work against the tension.
    public float DrawProgress => .78f * Mathf.SmoothStep(DrawTime, TightenTime, _time)
        + .22f * Mathf.SmoothStep(TightenTime, DrawEndTime, _time);
    public Vector2 AimDirection => (_aim - _destination).Normalized();
    private float OrbitProgress => Mathf.Clamp((_time - StageTime) / (FeedTime - StageTime), 0, 1);
    public Vector2 ArrowDirection
    {
        get
        {
            if (_time < StageTime || _time >= FeedTime || _flightPath == null) return AimDirection;
            float progress = OrbitProgress;
            var tangent = FlightPosition(Math.Min(1, progress + .005f))
                - FlightPosition(Math.Max(0, progress - .005f));
            return tangent.Normalized();
        }
    }
    private Vector2 NockPosition => _destination + AimDirection * (ShaftLength - BowGripOffset - BraceHeight);
    private Vector2 BowPosition => _destination - AimDirection * BowGripOffset;
    private Vector2 RearPosition => BowPosition - AimDirection * 45;
    public Vector2 CurrentPosition
    {
        get
        {
            if (_time < StageTime || _flightPath == null) return _start.Lerp(_stage, Mathf.SmoothStep(0, StageTime, _time));
            if (_time < FeedTime) return FlightPosition(OrbitProgress);
            if (_time < DockTime) return RearPosition.Lerp(NockPosition, Mathf.SmoothStep(FeedTime, DockTime, _time));
            return NockPosition.Lerp(_destination, DrawProgress);
        }
    }

    internal static void PreloadTextures()
    {
        _foldFrames = GD.Load<Texture2D>(JunnaMod.ResourceRoot + "images/vfx/arrow_fold_frames.png");
    }

    internal void Initialize(NCard cardNode)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _cardNode = cardNode;
        _trajectory = PreviewTrajectory;
        _cardScale = cardNode.Scale;
        _cardRotation = cardNode.Rotation;
        _cardModulate = cardNode.Modulate;
        var frame = cardNode.GetNode<Control>("%Frame");
        var bounds = frame.GetGlobalRect();
        var globalCenter = frame.GetGlobalTransform() * (frame.Size * .5f);
        _cardCenter = cardNode.GetGlobalTransform().AffineInverse() * globalCenter;
        var local = GetGlobalTransform().AffineInverse();
        _start = local * globalCenter;
        _cardSize = (local * bounds.End - local * bounds.Position).Abs();
        _cardSize = new(Math.Max(120, _cardSize.X), Math.Max(168, _cardSize.Y));
        _canvas = GetViewportRect().Size;
        _stage = new(Mathf.Clamp(_start.X, _canvas.X * .35f, _canvas.X * .72f), Math.Min(_start.Y, _canvas.Y * .47f));
        // The HUD supplies the live slot and target before the first moving frame.
        _destination = _start + Vector2.Up * 400;
        _aim = _start + Vector2.Right * 400;
        _trailAnchor = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = _start };
        AddChild(_trailAnchor);
        _trail = NCardTrailVfx.Create(_trailAnchor, cardNode.Model!.Owner!.Character.TrailPath);
        if (_trail != null)
        {
            AddChild(_trail);
            // Retain the native trail's taper and point lifetime with a quieter palette.
            foreach (var line in _trail.GetNode<Node2D>("Trails").GetChildren().OfType<Line2D>())
            {
                bool outer = line.Name == "OuterTrail";
                line.Width = outer ? 28 : 12;
                line.Modulate = new Color(.58f, .75f, .82f, outer ? .24f : .42f);
            }
            foreach (var child in _trail.GetNode<Node2D>("Sprites").GetChildren())
            {
                if (child is CpuParticles2D particles)
                {
                    particles.Amount = 16;
                    particles.Lifetime = Math.Min(particles.Lifetime, .45);
                    particles.ColorRamp = new Gradient
                    {
                        Colors = [new Color(.66f, .80f, .85f, .38f), new Color(.44f, .60f, .68f, 0)]
                    };
                }
                else if (child is Sprite2D sprite)
                {
                    sprite.Modulate = new Color(.60f, .77f, .83f, .22f);
                    sprite.Scale *= .55f;
                }
            }
        }
    }

    internal void SetProgress(float progress)
    {
        float previousTime = _time;
        _time = progress * (float)Duration;
        // The reload event has a short lead-in before its first string movement.
        float drawCueTime = DrawTime - .21f;
        if (previousTime < drawCueTime && _time >= drawCueTime) SfxCmd.Play(DrawSfx, .28f);
        if (previousTime < DrawEndTime && _time >= DrawEndTime) SfxCmd.Play(FullDrawSfx, .22f);
        if (_cardNode != null && GodotObject.IsInstanceValid(_cardNode))
        {
            float fold = Mathf.SmoothStep(FeedTime - .06f, CardGoneTime, _time);
            float travelScale = Mathf.Lerp(1, .22f, Mathf.SmoothStep(0, FeedTime * .70f, _time));
            float shrink = Mathf.Lerp(travelScale, .18f, fold);
            _cardNode.Scale = _cardScale * new Vector2(shrink * Mathf.Lerp(1, .65f, fold), shrink);
            float rotation = Mathf.LerpAngle(_cardRotation, ArrowDirection.Angle() - Mathf.Pi * .5f,
                Mathf.SmoothStep(StageTime, StageTime + .12f, _time));
            _cardNode.Rotation = Mathf.LerpAngle(_cardNode.Rotation, rotation,
                1 - Mathf.Exp(-28 * Math.Max(0, _time - previousTime)));
            _cardNode.GlobalPosition += GetGlobalTransform() * CurrentPosition
                - _cardNode.GetGlobalTransform() * _cardCenter;
            _cardNode.Modulate = new Color(_cardModulate,
                _cardModulate.A * (1 - Mathf.SmoothStep(ArrowTime, CardGoneTime, _time)));
        }
        _trailAnchor.Position = CurrentPosition;
        if (_time >= FeedTime && _trail != null)
        {
            TaskHelper.RunSafely(_trail.FadeOut());
            _trail = null;
        }
        if (_time >= CardGoneTime) ReleaseCard();
        QueueRedraw();
    }

    internal void UpdateAim(Vector2 destination, Vector2 aim)
    {
        // Hold the bow's world pose while the arrow draws through it.
        if (_aimLocked) return;
        _destination = destination;
        _aim = aim;
        BuildFlightPath();
        _aimLocked = true;
    }

    private void BuildFlightPath()
    {
        var rear = RearPosition;
        Vector2[] points = _trajectory switch
        {
            // Two opposing sweeps give the S route a visible change of weight.
            1 => [_stage, _stage + new Vector2(125, 95), _stage + new Vector2(-240, 110),
                rear + new Vector2(-190, 155), rear + new Vector2(-125, -15), rear],
            // A smaller curl gathers just behind the bow.
            2 => [_stage, _stage + new Vector2(-50, 100), rear + new Vector2(-225, 155),
                rear + new Vector2(-170, -10), rear],
            // A single rising arc makes the nocking action the main gesture.
            3 => [_stage, new Vector2(rear.X - 175, _stage.Y - 55), rear],
            _ => [_stage, _stage + new Vector2(-15, 195), _stage + new Vector2(-390, 170),
                _stage + new Vector2(-470, -80), rear + new Vector2(-180, -15), rear]
        };
        _flightPath = new Curve2D { BakeInterval = 3 };
        for (int i = 0; i < points.Length; i++)
        {
            if (i != points.Length - 1)
                points[i] = points[i].Clamp(new Vector2(85, 170), _canvas - new Vector2(85, 150));
            var tangent = i == 0 ? (points[1] - points[0]) * .28f
                : i == points.Length - 1 ? AimDirection * 45
                : (points[i + 1] - points[i - 1]) * .18f;
            _flightPath.AddPoint(points[i], -tangent, tangent);
        }
    }

    private Vector2 FlightPosition(float progress)
    {
        // Ease at the two ends while preserving most of the travelling time at speed.
        float eased = .35f * Mathf.SmoothStep(0, 1, progress) + .65f * progress;
        return _flightPath.SampleBaked(_flightPath.GetBakedLength() * eased, true);
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
        float angle = AimDirection.Angle() - Mathf.Pi * .5f;
        float shaft = Mathf.SmoothStep(ArrowTime, CardGoneTime + .025f, _time);
        float shaftAlpha = shaft * (1 - Mathf.SmoothStep(HoldEndTime, (float)Duration, _time));
        float tail = Mathf.Lerp(38, ShaftLength, shaft);
        float bowAlpha = Mathf.SmoothStep(.18f, .32f, _time)
            * (1 - Mathf.SmoothStep(HoldEndTime, (float)Duration, _time));
        if (bowAlpha > 0)
        {
            DrawSetTransform(BowPosition, angle);
            var nock = _time < DockTime ? new Vector2(0, -BraceHeight)
                : new Vector2(0, (CurrentPosition - BowPosition).Dot(AimDirection) - tail + 2);
            DrawBow(nock, bowAlpha);
        }
        DrawSetTransform(CurrentPosition, ArrowDirection.Angle() - Mathf.Pi * .5f);
        if (shaftAlpha > 0)
        {
            var ink = new Color(.65f, .75f, .79f, .68f * shaftAlpha);
            DrawLine(new(0, -22), new(0, -tail), new Color(.20f, .27f, .30f, .38f * shaftAlpha), 3.5f, true);
            DrawLine(new(0, -22), new(0, -tail), ink, 1.5f, true);
            DrawLine(new(0, -tail + 24), new(-9, -tail + 8), ink, 2, true);
            DrawLine(new(0, -tail + 24), new(9, -tail + 8), ink, 2, true);
        }
        float begin = FeedTime - .045f, end = CardGoneTime + .10f;
        if (_time >= begin && _time < end)
        {
            float phase = (_time - begin) / (end - begin);
            int frame = Math.Min(7, (int)(phase * 8));
            var cell = _foldFrames.GetSize() / new Vector2(4, 2);
            var source = new Rect2(new Vector2(frame % 4, frame / 4) * cell, cell);
            var size = _cardSize * .55f;
            float alpha = Mathf.SmoothStep(0, .1f, phase) * (1 - Mathf.SmoothStep(.8f, 1, phase)) * .85f;
            DrawTextureRectRegion(_foldFrames, new Rect2(-size * .5f, size), source, new Color(1, 1, 1, alpha));
        }
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawBow(Vector2 nock, float alpha)
    {
        float draw = DrawProgress;
        float span = Mathf.Lerp(100, 78, draw);
        float bend = Mathf.Lerp(BraceHeight, 28, draw);
        foreach (int side in new[] { -1, 1 })
        {
            var grip = new Vector2(side * 4, 0);
            var tip = new Vector2(side * span, -bend);
            var limb = new Vector2[21];
            for (int index = 0; index < limb.Length; index++)
            {
                float progress = (float)index / (limb.Length - 1);
                limb[index] = grip.BezierInterpolate(new(side * 30, 18),
                    new(side * span * .9f, -bend * 1.3f), tip, progress);
            }
            DrawPolyline(limb, new Color(.38f, .66f, .76f, .12f * alpha), 6, true);
            DrawPolyline(limb, new Color(.58f, .79f, .86f, .60f * alpha), 2.2f, true);
            DrawPolyline(limb, new Color(.80f, .92f, .95f, .40f * alpha), .8f, true);
            DrawLine(tip, nock, new Color(.60f, .82f, .90f, .12f * alpha), 3, true);
            DrawLine(tip, nock, new Color(.72f, .87f, .92f, .52f * alpha), 1.1f, true);
        }
        DrawLine(new(-5, 0), new(5, 0), new Color(.74f, .85f, .88f, .65f * alpha), 4, true);
    }
}
