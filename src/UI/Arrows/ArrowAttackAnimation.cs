using ShoujoKageki.Core.V1.Presentation;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class ArrowAttackAnimation
{
    private const string Pose = "ArrowAttack";
    private static readonly ConditionalWeakTable<AnimationPlayer, State> States = new();

    private static AnimationPlayer? Animator(NCreature? creature)
        => creature?.Entity.Player?.Character is Junna
            ? creature.Visuals.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") : null;

    internal static IDisposable? Begin(Player player)
    {
        if (!CoreEffects.Enabled) return null;
        var animator = Animator(NCombatRoom.Instance?.GetCreatureNode(player.Creature));
        if (animator == null || player.Creature.IsDead) return null;
        var state = States.GetValue(animator, _ => new State());
        state.ReturnToIdle?.Kill();
        state.ReturnToIdle = null;
        state.Depth++;
        if (animator.CurrentAnimation != Pose)
        {
            animator.Play(Pose);
            animator.Advance(0);
        }
        return new Resolution(animator, state);
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool KeepPose(NCreature __instance, string trigger)
        => trigger is not ("Attack" or "Cast" or "Idle" or "Relaxed")
            || Animator(__instance)?.CurrentAnimation != Pose;

    private sealed class State
    {
        internal int Depth;
        internal Tween? ReturnToIdle;
    }

    private sealed class Resolution(AnimationPlayer animator, State state) : IDisposable
    {
        public void Dispose()
        {
            if (--state.Depth > 0 || !GodotObject.IsInstanceValid(animator) || !animator.IsInsideTree()) return;
            // Bridge adjacent resolutions without restarting the pose or delaying combat.
            state.ReturnToIdle = animator.CreateTween();
            state.ReturnToIdle.TweenInterval(.2);
            state.ReturnToIdle.TweenCallback(Callable.From(() =>
            {
                state.ReturnToIdle = null;
                if (animator.CurrentAnimation == Pose) animator.Play("Idle");
            }));
        }
    }
}
