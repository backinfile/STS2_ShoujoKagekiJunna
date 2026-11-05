using Godot;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Critically damped layout motion; retargeting keeps the current velocity.</summary>
internal sealed class ArrowLayoutMotion
{
    public Vector2 Center { get; private set; }
    public float Angle { get; private set; }
    private Vector2 _velocity;
    private float _angularVelocity;
    private bool _initialized;

    public void Reset(Vector2 center, float angle)
    {
        Center = center; Angle = angle;
        _velocity = Vector2.Zero; _angularVelocity = 0;
        _initialized = true;
    }

    public void Advance(Vector2 destination, Vector2 aim, float delta)
    {
        if (!_initialized) Reset(destination, (aim - destination).Angle() - Mathf.Pi * .5f);
        if (delta <= 0) return;
        // Exact damped-spring integration stays stable across frame rates and frame hitches.
        const float positionOmega = 14;
        var offset = Center - destination;
        var momentum = _velocity + positionOmega * offset;
        float decay = Mathf.Exp(-positionOmega * delta);
        Center = destination + (offset + momentum * delta) * decay;
        _velocity = (_velocity - positionOmega * momentum * delta) * decay;

        float desiredAngle = (aim - Center).Angle() - Mathf.Pi * .5f;
        float error = -Mathf.AngleDifference(Angle, desiredAngle);
        // Small corrections settle sooner; large turns carry more weight. At rest, angular
        // speed rises from zero, peaks, then decays. No elastic bounce or full-circle wrap.
        float omega = Mathf.Lerp(18, 10, Mathf.Clamp(Mathf.Abs(error) / Mathf.Pi, 0, 1));
        float angularMomentum = _angularVelocity + omega * error;
        float angularDecay = Mathf.Exp(-omega * delta);
        Angle += (error + angularMomentum * delta) * angularDecay - error;
        _angularVelocity = (_angularVelocity - omega * angularMomentum * delta) * angularDecay;
    }
}
