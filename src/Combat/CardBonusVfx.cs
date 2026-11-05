using Godot;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Warm motes clinging to the frame of a card that received combat investment.</summary>
public partial class CardBonusVfx : Node2D
{
    private NCard? _cardNode;
    private CardModel? _arrowCard, _observedCard;
    private Vector2[]? _outline;
    private CpuParticles2D? _glow, _sparks, _flare;
    private DamageModifierSnapshot _previous;
    private bool _active;
    private float _pulse;
    private static Texture2D? _moteTexture;
    private static CanvasItemMaterial? _additive;
    private bool Compact => _arrowCard != null;

    internal static void Attach(NCard cardNode)
    {
        if (!cardNode.IsNodeReady() || cardNode.Body == null) return;
        var effect = cardNode.Body.GetNodeOrNull<CardBonusVfx>("JunnaBonusVfx");
        if (effect == null)
        {
            effect = new CardBonusVfx { Name = "JunnaBonusVfx", _cardNode = cardNode };
            // Keep the effect in the card body's draw order, including hand overlap and hover.
            cardNode.Body.AddChild(effect);
        }
        effect.Refresh(0);
    }

    internal static CardBonusVfx ForArrow(CardModel card, Vector2[] outline) => new()
    {
        Name = "JunnaBonusVfx", _arrowCard = card, _outline = outline
    };

    public override void _Ready() => Refresh(0);
    public override void _Process(double delta) => Refresh((float)delta);

    private void Refresh(float delta)
    {
        var card = _arrowCard ?? _cardNode?.Model;
        bool active = card != null && DamageModifiers.HasReceivedBonus(card)
            && (_cardNode == null || _cardNode.Visibility == ModelVisibility.Visible);
        var snapshot = card == null ? default : DamageModifiers.CaptureDamageModifierSnapshot(card);
        bool gained = active && card == _observedCard
            && (snapshot.DamageBonus > _previous.DamageBonus || snapshot.ExtraHits > _previous.ExtraHits);
        if (card != _observedCard) _pulse = 0;
        _observedCard = card;
        _previous = snapshot;
        Visible = active;
        if (active && _glow == null) CreateParticles();
        if (_glow == null) return;
        bool emitting = active && IsVisibleInTree();
        if (emitting != _glow.Emitting)
        {
            _glow.Emitting = _sparks!.Emitting = emitting;
            if (emitting) { _glow.Restart(); _sparks.Restart(); }
        }
        if (gained)
        {
            _pulse = .7f;
            _flare!.Restart();
            _flare.Emitting = true;
        }
        if (!active && _active) _flare!.Emitting = false;
        _active = active;
        _pulse = Math.Max(0, _pulse - delta);
        _glow.Modulate = new Color(1, 1, 1, .7f + _pulse);
    }

    private void CreateParticles()
    {
        if (_outline == null)
        {
            var rect = _cardNode!.GetNode<Control>("%Frame").GetRect().Grow(-7);
            float left = rect.Position.X, top = rect.Position.Y, right = rect.End.X, bottom = rect.End.Y;
            const float corner = 14;
            _outline = [new(left + corner, top), new(right - corner, top), new(right, top + corner),
                new(right, bottom - corner), new(right - corner, bottom), new(left + corner, bottom),
                new(left, bottom - corner), new(left, top + corner)];
        }
        var points = new List<Vector2>();
        var normals = new List<Vector2>();
        for (int edge = 0; edge < _outline.Length; edge++)
        {
            var start = _outline[edge];
            var end = _outline[(edge + 1) % _outline.Length];
            var tangent = (end - start).Normalized();
            var normal = new Vector2(tangent.Y, -tangent.X);
            int steps = Math.Max(1, (int)Math.Ceiling(start.DistanceTo(end) / (Compact ? 2 : 7)));
            for (int step = 0; step < steps; step++)
            {
                points.Add(start.Lerp(end, (float)step / steps));
                // Curl along the frame before drifting outward.
                normals.Add((normal * .35f - tangent).Normalized());
            }
        }
        _moteTexture ??= new GradientTexture2D
        {
            Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new(.5f, .5f), FillTo = new(1, .5f),
            Gradient = new Gradient
            {
                Offsets = [0, .16f, .46f, 1],
                Colors = [Colors.White, new Color(1, 1, 1, .85f), new Color(1, 1, 1, .22f), new Color(1, 1, 1, 0)]
            }
        };
        _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _glow = MakeEmitter("FrameGlow", points, normals, Compact ? 18 : 54, 1.5, .3f, .62f, 3, 8);
        _sparks = MakeEmitter("FrameMotes", points, normals, Compact ? 10 : 30, 1.1, .07f, .14f, 7, 17);
        _flare = MakeEmitter("GainFlare", points, normals, Compact ? 16 : 48, .65, .12f, .28f, 16, 32);
        _flare.OneShot = true;
        _flare.Explosiveness = 1;
        _flare.Preprocess = 0;
        _flare.Emitting = false;
    }

    private CpuParticles2D MakeEmitter(string name, List<Vector2> points, List<Vector2> normals,
        int amount, double lifetime, float minScale, float maxScale, float minSpeed, float maxSpeed)
    {
        float unit = Compact ? .3f : 1;
        var particles = new CpuParticles2D
        {
            Name = name, Emitting = false, Amount = amount, Lifetime = lifetime, Preprocess = lifetime,
            LocalCoords = true, Texture = _moteTexture, Material = _additive,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.DirectedPoints,
            EmissionPoints = points.ToArray(), EmissionNormals = normals.ToArray(),
            Direction = Vector2.Up, Spread = 24, Gravity = new(0, -5 * unit),
            InitialVelocityMin = minSpeed * unit, InitialVelocityMax = maxSpeed * unit,
            ScaleAmountMin = minScale * unit, ScaleAmountMax = maxScale * unit,
            ColorRamp = new Gradient
            {
                Offsets = [0, .18f, .55f, 1],
                Colors = [new Color(1, .62f, .17f, 0), new Color(1, .8f, .35f, .8f),
                    new Color(1, .62f, .14f, .55f), new Color(.85f, .33f, .06f, 0)]
            }
        };
        AddChild(particles);
        return particles;
    }
}
