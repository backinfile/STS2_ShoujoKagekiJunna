using ShoujoKageki.Core.V1.Presentation;
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
    private const string PreparePose = "ArrowPrepare";
    private const string RelaxedPose = "Relaxed";
    private const string IdlePose = "Idle";

    private static AnimationPlayer? Animator(NCreature? creature)
        => creature?.Entity.Player?.Character is Junna
            ? creature.Visuals.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") : null;

    internal static AnimationPlayer? AnimatorFor(Player player)
        => Animator(NCombatRoom.Instance?.GetCreatureNode(player.Creature));

    internal static void PlayEntrance(Player player) => Play(player, "Entrance");

    internal static void PlayPreparation(Player player) => Play(player, PreparePose);

    internal static void Shoot(Player player) => Play(player, Pose);

    internal static void Relax(Player player) => Play(player, RelaxedPose);

    private static void Play(Player player, string animation)
    {
        if (!CoreEffects.Enabled || player.Creature.IsDead) return;
        var animator = AnimatorFor(player);
        if (animator == null) return;
        var current = animator.CurrentAnimation;
        if (current == animation || current == "Dead") return;
        if (animation == RelaxedPose && current != JunnaIdleGesture.HairTuck
            && animator.IsPlaying() && animator.AnimationGetNext(current) == IdlePose)
        {
            animator.ClearQueue();
            animator.Queue(RelaxedPose);
            return;
        }
        if (current == Pose && animation != RelaxedPose) return;
        if (current == "Hit" && animation == PreparePose) return;
        // Apply the atlas and its transform during the AnimationPlayer's frame update.
        animator.Play(animation);
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool KeepPose(NCreature __instance, string trigger)
    {
        if (trigger is not ("Attack" or "Cast" or IdlePose)) return true;
        var animator = Animator(__instance);
        if (trigger == IdlePose && animator?.GetQueue().Contains(RelaxedPose) == true) return false;
        return animator?.CurrentAnimation switch
        {
            Pose or "Dead" => false,
            PreparePose => trigger == "Attack",
            RelaxedPose => trigger != IdlePose,
            _ => true
        };
    }
}
