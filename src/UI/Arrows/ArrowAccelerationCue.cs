using Godot;
using ShoujoKageki.Core.V1.Presentation;

namespace ShoujoKagekiJunna.Combat;

internal enum AccelerationCueKind { None, Advances, Completes }

internal partial class ArrowAccelerationCue : Node2D
{
    // Outside the 42 × 73 arrow frame, in the same local space as its tilt.
    private static readonly Vector2[] Edge = [new(21, -3), new(45, 23), new(21, 77), new(-3, 23), new(21, -3)];
    private static readonly Vector2[] Star =
    [
        new(35, 55), new(37, 63), new(44, 65), new(37, 67),
        new(35, 75), new(33, 67), new(26, 65), new(33, 63)
    ];
    private static readonly Vector2[] StarEdge = [.. Star, Star[0]];
    private static readonly Color Shadow = new("142334D9");
    private static readonly Color Blue = new("9ADFFB");
    private static readonly Color StarColor = new("E6F9FF");
    private const float IntroSeconds = .26f;
    private float _intro;
    private AccelerationCueKind _cue;

    internal void SetCue(AccelerationCueKind cue)
    {
        if (_cue == cue) return;
        _cue = cue;
        Visible = cue != AccelerationCueKind.None;
        bool animate = Visible && CoreEffects.AmbientEffects;
        _intro = animate ? 0 : 1;
        SetProcess(animate);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _intro = CoreEffects.AmbientEffects ? Math.Min(1, _intro + (float)delta / IntroSeconds) : 1;
        QueueRedraw();
        if (_intro >= 1) SetProcess(false);
    }

    public override void _Draw()
    {
        if (_cue == AccelerationCueKind.None) return;
        bool completes = _cue == AccelerationCueKind.Completes;
        DrawPolyline(Edge, Shadow, 3.65f, true);
        if (completes)
            DrawPolyline(Edge, new Color(Blue, .26f + .42f * (1 - _intro)), 6.5f, true);
        DrawPolyline(Edge, new Color(Blue, .85f), 1.65f, true);
        if (_intro < 1)
        {
            // A short pass from the tail to the tip down both sides.
            DrawSweep(Edge[0], Edge[1], Edge[2]);
            DrawSweep(Edge[0], Edge[3], Edge[2]);
        }
        if (completes)
        {
            if (_intro < 1)
                DrawArc(new(35, 65), 5 + 13 * _intro, 0, Mathf.Tau, 24, new Color(StarColor, .8f * (1 - _intro)), 1.6f, true);
            DrawPolyline(StarEdge, Shadow, 2.5f, true);
            DrawColoredPolygon(Star, StarColor);
        }
    }

    private void DrawSweep(Vector2 start, Vector2 shoulder, Vector2 end)
    {
        const float shoulderTime = .38f, length = .25f;
        float from = Math.Max(0, _intro - length);
        if (from < shoulderTime)
            DrawLine(start.Lerp(shoulder, from / shoulderTime), start.Lerp(shoulder, Math.Min(_intro / shoulderTime, 1)),
                Colors.White, 2.5f, true);
        if (_intro > shoulderTime)
            DrawLine(shoulder.Lerp(end, Math.Max(0, (from - shoulderTime) / (1 - shoulderTime))),
                shoulder.Lerp(end, (_intro - shoulderTime) / (1 - shoulderTime)), Colors.White, 2.5f, true);
    }
}
