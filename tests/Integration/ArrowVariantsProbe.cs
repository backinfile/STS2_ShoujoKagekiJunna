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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowVariantsProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_variants";
    public override string Args => "before | after | prepare | status";
    public override string Description => "Targeted Meteor, single-target, group and support arrow checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        string mode = args.FirstOrDefault() ?? "after";
        if (mode == "status")
        {
            var hud = NCombatRoom.Instance!.GetNodeOrNull<ArrowHud>("JunnaArrows");
            return new(true, JsonSerializer.Serialize(new {
                deployed = ArrowCombat.Deployed.Count,
                deployments = hud?.GetChildren().OfType<ArrowDeploymentVfx>().Count() ?? 0,
                releases = hud?.GetChildren().OfType<ArrowReleaseVfx>().Count() ?? 0
            }));
        }
        if (mode == "prepare") return new(Prepare(player), true, "Arrow demonstration prepared");
        if (mode is not ("before" or "after")) return new(false, Args);
        return new(Check(player, mode), true, "Arrow variant checks started");
    }

    private static async Task Clear(Player player, bool visuals)
    {
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: !visuals);
        }
    }

    private static async Task Prepare(Player player)
    {
        await Clear(player, true);
        var targets = player.Creature.CombatState!.HittableEnemies.ToArray();
        for (int i = 0; i < targets.Length; i++)
        {
            await CreatureCmd.SetMaxHp(targets[i], i == 0 ? 40 : 1000);
            await CreatureCmd.SetCurrentHp(targets[i], i == 0 ? 40 : 1000);
        }
        foreach (var creature in targets.Append(player.Creature))
            foreach (var power in creature.Powers.ToArray()) await PowerCmd.Remove(power);
        player.PlayerCombatState!.Energy = 30;
        foreach (string id in new[] { "JA76", "JB03", "JA58", "JH11", "JA108B", "JA81", "JA71" })
            await CardPileCmd.Add(CardCatalog.Create(id, player), PileType.Hand);
    }

    private static async Task Check(Player player, string label)
    {
        var context = new ThrowingPlayerChoiceContext();
        var targets = player.Creature.CombatState!.HittableEnemies.ToArray();
        if (targets.Length != 2) throw new InvalidOperationException("Use fight NIBBITS_NORMAL");
        using var single = KnowledgeCombat.SingleCard();
        var checks = new List<object>();
        var metrics = new List<object>();
        void Verify(string name, bool pass, object actual) => checks.Add(new { name, pass, actual });
        foreach (string scenario in new[] { "meteor", "meteor-pair", "linked-shot", "sweep", "full-draw", "glimmer", "restrain", "group-attack" })
        {
            await Clear(player, false);
            foreach (var creature in targets.Append(player.Creature))
                foreach (var power in creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var target in targets)
            {
                await CreatureCmd.SetMaxHp(target, 100000);
                await CreatureCmd.SetCurrentHp(target, 100000);
            }
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState!.Energy = 100;
            await Task.Delay(700);
            var watch = Stopwatch.StartNew();
            var play = Play();
            int peakDeployments = 0;
            while (!play.IsCompleted && watch.Elapsed.TotalSeconds < 15)
            {
                peakDeployments = Math.Max(peakDeployments, Hud()?.GetChildren().OfType<ArrowDeploymentVfx>().Count() ?? 0);
                await Task.Delay(10);
            }
            if (!play.IsCompleted) throw new TimeoutException(scenario);
            await play;
            double deploySeconds = watch.Elapsed.TotalSeconds;
            var arrows = ArrowCombat.Deployed.ToArray();
            var effects = Hud()!.GetChildren().OfType<ArrowDeploymentVfx>().ToArray();
            int expectedCount = scenario is "meteor-pair" or "sweep" ? 2 : 1;
            Verify(scenario + ": deployment count", arrows.Length == expectedCount, arrows.Length);
            Verify(scenario + ": visual survives action return", effects.Length > 0, effects.Length);
            if (scenario is "meteor-pair" or "sweep")
                Verify(scenario + ": deployment animations overlap", peakDeployments >= 2, peakDeployments);
            if (scenario == "meteor")
                Verify("meteor: generated damage, countdown and assigned target", arrows[0].Card.DynamicVars.Damage.BaseValue == 7 && arrows[0].Remaining == 2 && arrows[0].Target == targets[0], arrows[0].Remaining);
            if (scenario == "meteor-pair")
                Verify("meteor-pair: independent targets and upgraded countdown", arrows[0].Target == targets[0] && arrows[1].Target == targets[1] && arrows[0].Remaining == 2 && arrows[1].Remaining == 1, arrows.Select(a => a.Remaining).ToArray());
            if (scenario == "linked-shot")
                Verify("linked-shot: original damage and target are preserved", targets[1].CurrentHp == 99990 && arrows[0].Target == targets[1] && arrows[0].Card.DynamicVars.Damage.BaseValue == 10, targets[1].CurrentHp);
            if (scenario == "sweep")
                Verify("sweep: one Meteor per target after the original hit", targets.All(t => t.CurrentHp == 99988 && arrows.Count(a => a.Target == t && a.Card.DynamicVars.Damage.BaseValue == 12) == 1), targets.Select(t => t.CurrentHp).ToArray());
            if (scenario is "restrain" or "group-attack")
                Verify(scenario + ": group target survives early return", arrows[0].TargetsGroup && arrows[0].Targets.Count() == 2, arrows[0].Targets.Count());

            watch.Restart();
            foreach (var arrow in arrows) await ArrowCombat.Accelerate(context, arrow, 99);
            double resolveSeconds = watch.Elapsed.TotalSeconds;
            Verify(scenario + ": no zero-count deployments remain", ArrowCombat.Deployed.Count == 0, ArrowCombat.Deployed.Count);
            switch (scenario)
            {
                case "meteor":
                    Verify("meteor: damage and exhaust", targets[0].CurrentHp == 99993 && arrows[0].Card.Pile?.Type == PileType.Exhaust, targets[0].CurrentHp);
                    break;
                case "meteor-pair":
                    Verify("meteor-pair: each target takes its own damage once", targets[0].CurrentHp == 99993 && targets[1].CurrentHp == 99992 && arrows.All(a => a.Card.Pile?.Type == PileType.Exhaust), targets.Select(t => t.CurrentHp).ToArray());
                    break;
                case "linked-shot":
                    Verify("linked-shot: generated Meteor deals the second hit", targets[1].CurrentHp == 99980, targets[1].CurrentHp);
                    break;
                case "sweep":
                    Verify("sweep: both Meteors resolve once", targets.All(t => t.CurrentHp == 99976), targets.Select(t => t.CurrentHp).ToArray());
                    break;
                case "full-draw":
                    Verify("full-draw: damage and discard", targets[0].CurrentHp == 99975 && arrows[0].Card.Pile?.Type == PileType.Discard, targets[0].CurrentHp);
                    break;
                case "glimmer":
                    Verify("glimmer: block and energy resolve once", player.Creature.Block == 4 && player.PlayerCombatState.Energy == 101, new { player.Creature.Block, player.PlayerCombatState.Energy });
                    break;
                case "restrain":
                    Verify("restrain: both enemies gain exactly two debuffs", targets.All(t => t.GetPower<VulnerablePower>()?.Amount == 2 && t.GetPower<WeakPower>()?.Amount == 2), targets.Select(t => new { vulnerable = t.GetPower<VulnerablePower>()?.Amount, weak = t.GetPower<WeakPower>()?.Amount }).ToArray());
                    break;
                case "group-attack":
                    Verify("group-attack: both enemies take damage once", targets.All(t => t.CurrentHp == 99901), targets.Select(t => t.CurrentHp).ToArray());
                    break;
            }
            await Task.Delay(700);
            Verify(scenario + ": deployment and release nodes are reclaimed", !Hud()!.GetChildren().Any(n => n is ArrowDeploymentVfx or ArrowReleaseVfx), Hud()!.GetChildren().Count(n => n is ArrowDeploymentVfx or ArrowReleaseVfx));
            metrics.Add(new { scenario, deploySeconds, resolveSeconds, peakDeployments, fullDuration = effects.FirstOrDefault()?.Duration ?? 0 });
            Write();
            continue;

            ArrowHud? Hud() => NCombatRoom.Instance!.GetNodeOrNull<ArrowHud>("JunnaArrows");
            async Task Play()
            {
                if (scenario == "meteor") { await CardActions.Fly(context, player, 7, targets[0]); return; }
                if (scenario == "meteor-pair")
                {
                    await CardActions.Fly(context, player, 7, targets[0]);
                    await CardActions.Fly(context, player, 8, targets[1], upgraded: true);
                    return;
                }
                string id = scenario switch {
                    "linked-shot" => "JA81", "sweep" => "JA76", "full-draw" => "JB03",
                    "glimmer" => "JH11", "restrain" => "JA58", _ => "JA108B"
                };
                var card = CardCatalog.Create(id, player);
                await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
                await CardActions.PaidPlay(context, card, card.TargetType == TargetType.AnyEnemy ? targets[scenario == "linked-shot" ? 1 : 0] : null);
            }
        }
        Write();
        if (checks.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!))
            throw new InvalidOperationException("Arrow variant checks failed");
        void Write()
        {
            string path = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/arrow-play-speed", "variants-" + label + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { label, completed = metrics.Count == 8, checks, metrics }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
