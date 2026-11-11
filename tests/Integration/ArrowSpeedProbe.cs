using System.Diagnostics;
using System.Text.Json;
using Godot;
using HarmonyLib;
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

public sealed class ArrowSpeedProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_speed";
    public override string Args => "check | prepare";
    public override string Description => "Measure real Stardust deployment and child-card resolution.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Arrow speed checks started");

    private static async Task Prepare(Player player)
    {
        foreach (var card in CardActions.All(player).ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        }
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            await CreatureCmd.SetMaxHp(enemy, 1000);
            await CreatureCmd.SetCurrentHp(enemy, 1000);
        }
        player.PlayerCombatState!.Energy = 30;
        foreach (string id in new[] { "U_CARD_005", "JB03", "JA81", "JA71", "JA71", "JA71" })
            await CardPileCmd.Add(CardCatalog.Create(id, player), PileType.Hand, skipVisuals: false);
    }

    private static async Task Check(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
        var target = player.Creature.CombatState!.HittableEnemies.First();
        await CreatureCmd.SetMaxHp(target, 100000);
        await CreatureCmd.SetCurrentHp(target, 100000);
        player.PlayerCombatState!.Energy = 30;
        using var single = KnowledgeCombat.SingleCard();
        var star = CardCatalog.Create("U_CARD_005", player);
        await CardPileCmd.Add(star, PileType.Hand, skipVisuals: true);
        var watch = Stopwatch.StartNew();
        await CardActions.PaidPlay(context, star, target);
        double deploymentSeconds = watch.Elapsed.TotalSeconds;
        var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
        var effects = hud.GetChildren().OfType<ArrowDeploymentVfx>().ToArray();
        int activeAfterPlay = effects.Length;
        bool ownsFlyingCard = effects.Any(e => e.GetChildren()
            .OfType<MegaCrit.Sts2.Core.Nodes.Cards.NCard>().Any(c => !c.IsQueuedForDeletion()));
        double effectSeconds = effects.Length == 0 ? 0 :
            (float)AccessTools.Field(typeof(ArrowDeploymentVfx), "_time").GetValue(effects[0])! /
            (float)AccessTools.Field(typeof(ArrowHud).Assembly.GetType("ShoujoKagekiJunna.Combat.ArrowLayoutMotion")!, "SpeedMultiplier").GetRawConstantValue()!;
        double inputReleaseSeconds = effects.Length == 0 ? 0 :
            (double)AccessTools.Property(typeof(ArrowDeploymentVfx), "InputReleaseTime").GetValue(effects[0])!;
        double fullDuration = effects.FirstOrDefault()?.Duration ?? 0;
        decimal hp = target.CurrentHp;
        watch.Restart();
        var resolution = ArrowCombat.Accelerate(context, ArrowCombat.Find(star)!, 3);
        int peakDeployments = 0;
        while (!resolution.IsCompleted)
        {
            peakDeployments = Math.Max(peakDeployments, hud.GetChildren().OfType<ArrowDeploymentVfx>().Count());
            await Task.Delay(10);
        }
        await resolution;
        double resolutionSeconds = watch.Elapsed.TotalSeconds;
        int activeAfterChildren = hud.GetChildren().OfType<ArrowDeploymentVfx>().Count();
        var children = ArrowCombat.Deployed.ToArray();
        var rows = new List<object>();
        void Verify(string name, bool pass, object actual) => rows.Add(new { name, pass, actual });
        Verify("Stardust play releases within 1.0 seconds", deploymentSeconds < 1.0, deploymentSeconds);
        Verify("deployment animation is under 1.1 seconds", fullDuration > 0 && fullDuration < 1.1, fullDuration);
        Verify("action queue releases during the first 0.1 seconds of the visual", inputReleaseSeconds <= .1, inputReleaseSeconds);
        Verify("deployment returns before 0.4 seconds of its visual", effectSeconds > 0 && effectSeconds < .4, new { effectSeconds, nativeSeconds = deploymentSeconds - effectSeconds });
        Verify("in-flight card is independently owned when play returns", ownsFlyingCard, ownsFlyingCard);
        Verify("visual continues after play returns", activeAfterPlay > 0, activeAfterPlay);
        Verify("Stardust resolution and two children finish within 2.7 seconds", resolutionSeconds < 2.7, resolutionSeconds);
        Verify("Stardust creates two independently deployed children", children.Length == 2, children.Length);
        Verify("Stardust damage resolves exactly once", hp - target.CurrentHp == 8, hp - target.CurrentHp);
        Verify("parent exhausts after resolving", star.Pile?.Type == PileType.Exhaust, star.Pile?.Type.ToString() ?? "none");
        Verify("child deployment animations overlap during resolution", peakDeployments >= 2, peakDeployments);
        Verify("child visual continues after resolution", activeAfterChildren > 0, activeAfterChildren);
        // Finish a child while its deployment is still running, then finish its sibling.
        await ArrowCombat.Accelerate(context, children[0], 3);
        await ArrowCombat.Accelerate(context, children[1], 3);
        Verify("both overlapping children resolve without losing descendants", ArrowCombat.Deployed.Count == 4, ArrowCombat.Deployed.Count);
        await Task.Delay(1400);
        Verify("completed deployment nodes are reclaimed", !hud.GetChildren().OfType<ArrowDeploymentVfx>().Any(), hud.GetChildren().OfType<ArrowDeploymentVfx>().Count());
        Verify("completed release nodes are reclaimed", !hud.GetChildren().OfType<ArrowReleaseVfx>().Any(), hud.GetChildren().OfType<ArrowReleaseVfx>().Count());
        string path = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../..", "out/tmp/arrow-play-speed/checks.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!))
            throw new InvalidOperationException("Arrow speed regression failed; see checks.json");
    }
}
