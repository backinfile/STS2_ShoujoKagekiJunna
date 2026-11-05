using System.Collections;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowPivotProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_pivot";
    public override string Args => "check | demo";
    public override string Description => "Check portrait-body rotation center and prepare a normal retarget demonstration.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
        => new(args.FirstOrDefault() == "demo" ? Prepare(player!) : Check(player!), true, "Arrow pivot check completed");

    private static async Task Prepare(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray())
        { ArrowCombat.Cancel(arrow.Card); await CardPileCmd.RemoveFromCombat(arrow.Card, skipVisuals: true); }
        foreach (var card in player.PlayerCombatState!.Hand.Cards.ToArray())
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        var targets = player.Creature.CombatState!.HittableEnemies.ToArray();
        for (int i = 0; i < targets.Length; i++)
        { await CreatureCmd.SetMaxHp(targets[i], 1000); await CreatureCmd.SetCurrentHp(targets[i], i == 0 ? 6 : 1000); }
        player.PlayerCombatState.Energy = 20;
        foreach (string id in new[] { "JB01", "JB03", "JB03", "JA71" })
            await CardPileCmd.Add(CardCatalog.Create(id, player), PileType.Hand, skipVisuals: true);
    }

    private static async Task Check(Player player)
    {
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../..", "out/tmp/arrow-attack-pose/pivot-checks"));
        Directory.CreateDirectory(directory);
        var rows = new List<object>();
        void Verify(string name, bool pass)
        {
            rows.Add(new { name, pass });
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            if (!pass) throw new InvalidOperationException(name);
        }
        var targets = player.Creature.CombatState!.HittableEnemies.ToArray();
        Verify("fixture has a replacement target", targets.Length >= 2);
        var context = new ThrowingPlayerChoiceContext();
        await CardActions.Fly(context, player, 1, targets[0]);
        var arrow = ArrowCombat.Deployed.Single();
        await Task.Delay(1200);
        var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
        var views = (IDictionary)AccessTools.Field(typeof(ArrowHud), "_views").GetValue(hud)!;
        var view = views[arrow]!;
        var root = (Control)view.GetType().GetProperty("Root")!.GetValue(view)!;
        var frame = root.GetNode<Control>("TiltedFrame");
        var count = root.GetNode<Label>("Countdown");
        var motion = view.GetType().GetProperty("Motion")!.GetValue(view)!;
        var motionType = motion.GetType();
        Vector2 Center() => (Vector2)motionType.GetProperty("Center")!.GetValue(motion)!;
        var body = new Vector2(120, 138) * (42f / 240);
        var counter = new Vector2(120, 58) * (42f / 240);
        var tip = new Vector2(120, 409) * (42f / 240);
        Verify("pivot matches portrait-body visual center", frame.PivotOffset.IsEqualApprox(body));
        hud.SetProcess(false);
        try
        {
            var fixedCenter = hud.GetGlobalTransform() * Center();
            bool fixedPivot = true, fixedRadius = true, alignedCount = true, uprightCount = true;
            float radius = (frame.GetGlobalTransform() * tip).DistanceTo(fixedCenter);
            foreach (float angle in new[] { -2.5f, -1.2f, 0, .8f, 2.6f })
            {
                motionType.GetMethod("Reset")!.Invoke(motion, [Center(), angle]);
                hud._Process(0);
                var actual = frame.GetGlobalTransform() * body;
                fixedPivot &= actual.DistanceTo(fixedCenter) < .001f;
                fixedRadius &= Mathf.Abs((frame.GetGlobalTransform() * tip).DistanceTo(actual) - radius) < .001f;
                alignedCount &= (count.GetGlobalTransform() * (count.Size * .5f)).DistanceTo(frame.GetGlobalTransform() * counter) < .001f;
                uprightCount &= Mathf.Abs(count.GetGlobalTransform().X.Angle()) < .001f;
            }
            Verify("body center stays fixed through large turns", fixedPivot);
            Verify("tip turns on a constant radius", fixedRadius);
            Verify("countdown follows its frame position", alignedCount);
            Verify("countdown stays upright", uprightCount);
            await CreatureCmd.Kill(targets[0]);
            Verify("target death retargets to living replacement", arrow.Target == targets[1]);
            bool anchored = true;
            float before = frame.Rotation;
            for (int i = 0; i < 60; i++)
            {
                hud._Process(1.0 / 60);
                anchored &= (frame.GetGlobalTransform() * body).DistanceTo(hud.GetGlobalTransform() * Center()) < .001f;
            }
            Verify("actual retarget keeps body center on the moving slot", anchored);
            Verify("actual retarget turns toward the replacement", Mathf.Abs(Mathf.AngleDifference(before, frame.Rotation)) > .1f);
            var origin = frame.GetGlobalTransform() * body;
            var release = ArrowCombat.Accelerate(context, arrow, 99);
            Verify("release handoff preserves body center", (frame.GetGlobalTransform() * body).DistanceTo(origin) < .001f);
            await release;
        }
        finally { hud.SetProcess(true); }
        Verify("complete", true);
    }
}
