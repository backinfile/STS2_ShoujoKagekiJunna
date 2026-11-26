using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKageki.Core.V1.Presentation;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal sealed partial class JunnaIdleGesture : Node
{
    internal const string HairTuck = "HairTuck";
    private const string Idle = "Idle";
    private const string NodeName = "JunnaIdleGesture";
    private const int IdleCyclesBeforeGesture = 2;

    private AnimationPlayer _animator = null!;
    private Player _player = null!;
    private double _idleTime;
    private bool _played;

    internal static void Attach(Player player)
    {
        if (player.Character is not Junna || !LocalContext.IsMe(player)) return;
        var animator = ArrowAttackAnimation.AnimatorFor(player);
        if (animator == null || animator.HasNode(NodeName) || !animator.HasAnimation(HairTuck)) return;
        animator.AddChild(new JunnaIdleGesture
        {
            Name = NodeName,
            _animator = animator,
            _player = player,
        });
    }

    internal static void ResumeFromAction(Player player)
        => ArrowAttackAnimation.AnimatorFor(player)?.GetNodeOrNull<JunnaIdleGesture>(NodeName)?.Reset();

    public override void _Process(double delta)
    {
        var animation = _animator.CurrentAnimation;
        if (!CanPlayGesture() || animation is not (Idle or HairTuck))
        {
            Reset();
            return;
        }

        if (_played || animation != Idle) return;
        _idleTime += delta;
        var idleCycleDuration = _animator.CurrentAnimationLength;
        if (_idleTime < idleCycleDuration * IdleCyclesBeforeGesture) return;

        // Switch at the neutral pose shared by the idle loop and the gesture.
        var phase = _animator.CurrentAnimationPosition;
        if (phase > 0.12 && idleCycleDuration - phase > 0.12) return;

        _played = true;
        _animator.Play(HairTuck);
    }

    private bool CanPlayGesture()
    {
        var combat = CombatManager.Instance;
        return CoreEffects.Enabled && !_player.Creature.IsDead
            && _player.PlayerCombatState?.Phase == PlayerTurnPhase.Play
            && combat.IsInProgress && !combat.PlayerActionsDisabled
            && combat.DebugOnlyGetState()?.CurrentSide == CombatSide.Player
            && NCombatRoom.Instance?.Ui?.Hand is { InCardPlay: false, CurrentMode: NPlayerHand.Mode.Play };
    }

    private void Reset()
    {
        _idleTime = 0;
        _played = false;
        if (_animator.CurrentAnimation == HairTuck) _animator.Play(Idle);
    }
}
