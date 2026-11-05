using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowAttackPoseProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_attack_pose";
    public override string Args => "check";
    public override string Description => "Check arrow-only attack poses and uninterrupted consecutive releases.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
        => new(Check(player!), true, "Arrow attack pose checks completed");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../..", "out/tmp/arrow-attack-pose/checks"));
        Directory.CreateDirectory(directory);
        void Verify(string name, bool pass)
        {
            rows.Add(new { name, pass });
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            if (!pass) throw new InvalidOperationException(name);
        }
        var context = new ThrowingPlayerChoiceContext();
        var node = NCombatRoom.Instance!.GetCreatureNode(player.Creature)!;
        var animator = node.Visuals.GetNode<AnimationPlayer>("AnimationPlayer");
        var sprite = node.Visuals.GetNode<Sprite2D>("Visuals");
        var target = player.Creature.CombatState!.HittableEnemies.First();
        await CreatureCmd.SetMaxHp(target, 1000);
        await CreatureCmd.SetCurrentHp(target, 1000);
        foreach (var arrow in ArrowCombat.Deployed.ToArray())
        { ArrowCombat.Cancel(arrow.Card); await CardPileCmd.RemoveFromCombat(arrow.Card, skipVisuals: true); }
        animator.Play("Idle"); animator.Advance(0);
        var standing = sprite.Texture;
        node.SetAnimationTrigger("Attack"); animator.Advance(.2);
        Verify("ordinary attack retains standing artwork", sprite.Texture == standing);
        animator.Play("Idle"); animator.Advance(0);
        for (int i = 0; i < 3; i++) await CardActions.Fly(context, player, 4, target);
        Verify("deployment retains standing artwork", sprite.Texture == standing);
        var arrows = ArrowCombat.Deployed.ToArray();
        ArrowCombat.Deploy(arrows[0].Card, arrows[0].Play, 3, (_, _) => Task.CompletedTask);
        Verify("fixture includes a repeated arrow", arrows[0].PlayCount == 2);
        int starts = 0, samples = 0;
        bool stable = true, triggered = false;
        void Started(StringName animation) { if (animation == "ArrowAttack") starts++; }
        void Sample()
        {
            samples++;
            stable &= animator.CurrentAnimation == "ArrowAttack" && sprite.Texture != standing && sprite.Position == new Vector2(0, -153);
            if (!triggered)
            {
                triggered = true;
                foreach (string trigger in new[] { "Attack", "Cast", "Idle", "Relaxed" }) node.SetAnimationTrigger(trigger);
                stable &= animator.CurrentAnimation == "ArrowAttack";
            }
        }
        var tree = (SceneTree)Engine.GetMainLoop();
        animator.AnimationStarted += Started;
        try
        {
            var release = ArrowCombat.Accelerate(context, arrows[0], 99);
            Verify("pose switches before arrow flight", animator.CurrentAnimation == "ArrowAttack" && sprite.Texture != standing);
            tree.ProcessFrame += Sample;
            await release;
            await ArrowCombat.Accelerate(context, arrows[1], 99);
            await ArrowCombat.Accelerate(context, arrows[2], 99);
        }
        finally { tree.ProcessFrame -= Sample; animator.AnimationStarted -= Started; }
        Verify("consecutive releases were sampled", samples >= 10);
        Verify("pose and position stay fixed through all releases and native triggers", stable);
        Verify("consecutive releases start the pose once", starts == 1);
        Verify("all arrows leave the deployment pile", ArrowCombat.Deployed.Count == 0);
        Verify("repeated and consecutive arrows retain their damage", target.CurrentHp == 984);
        await Task.Delay(400);
        Verify("last release returns to idle", animator.CurrentAnimation == "Idle" && sprite.Texture == standing);

        await CardActions.Fly(context, player, 1, target);
        var interrupted = ArrowCombat.Deployed.Single();
        await ArrowCombat.Accelerate(context, interrupted, 99);
        node.SetAnimationTrigger("Hit");
        Verify("hit reaction interrupts the pose", animator.CurrentAnimation == "Hit");
        await Task.Delay(300);
        Verify("pending idle return preserves hit reaction", animator.CurrentAnimation == "Hit");
        animator.Play("Idle");
        await CardActions.Fly(context, player, 1, target);
        await ArrowCombat.Accelerate(context, ArrowCombat.Deployed.Single(), 99);
        node.SetAnimationTrigger("Dead");
        Verify("death interrupts the pose", animator.CurrentAnimation == "Dead");
        await Task.Delay(300);
        Verify("pending idle return preserves death animation", animator.CurrentAnimation == "Dead");
        animator.Play("Idle");

        await CardActions.Fly(context, player, 1, target);
        var failing = ArrowCombat.Deployed.Single();
        ArrowCombat.Cancel(failing.Card);
        ArrowCombat.Deploy(failing.Card, failing.Play, 1, (_, _) => throw new InvalidOperationException("pose fixture"));
        bool caught = false;
        try { await ArrowCombat.Accelerate(context, ArrowCombat.Deployed.Single(), 99); }
        catch (InvalidOperationException error) when (error.Message == "pose fixture") { caught = true; }
        await Task.Delay(400);
        Verify("failed resolution releases the pose", caught && animator.CurrentAnimation == "Idle");
        ArrowCombat.Cancel(failing.Card);
        await CardPileCmd.RemoveFromCombat(failing.Card, skipVisuals: true);
        Verify("complete", true);
    }
}
