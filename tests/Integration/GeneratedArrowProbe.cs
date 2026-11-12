using System.Diagnostics;
using System.Text.Json;
using ShoujoKageki.Core.V1;
using ShoujoKageki.Core.V1.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class GeneratedArrowProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_generated_arrows";
    public override string Args => "check";
    public override string Description => "Verify generated Meteor and Stardust cadence and native events.";
    public override bool IsNetworked => false;
    private static int _generated, _autoPlayed, _beforePlay, _afterPlay;
    private static bool IsGenerated(CardModel card) => CardCatalog.DesignId(card) is "JA81B" or "U_CARD_005";
    private static void Generated(CardModel card) { if (IsGenerated(card)) _generated++; }
    private static void AutoPlayed(CardModel card) { if (IsGenerated(card)) _autoPlayed++; }
    private static void BeforePlay(CardPlay __1) { if (IsGenerated(__1.Card)) _beforePlay++; }
    private static void AfterPlay(CardPlay __2) { if (IsGenerated(__2.Card)) _afterPlay++; }
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Generated arrow checks started");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Verify(string name, bool pass, object actual) => rows.Add(new { name, pass, actual });
        void ResetEvents() => _generated = _autoPlayed = _beforePlay = _afterPlay = 0;
        void Events(string name, int count) => Verify(name + ": native generation and play hooks fire once per child",
            _generated == count && _autoPlayed == count && _beforePlay == count && _afterPlay == count,
            new { generated = _generated, autoPlayed = _autoPlayed, beforePlay = _beforePlay, afterPlay = _afterPlay });
        var harmony = new Harmony("junna.generated-arrow.observer");
        harmony.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.AfterCardGeneratedForCombat)), prefix: new HarmonyMethod(typeof(GeneratedArrowProbe), nameof(Generated)));
        harmony.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardAutoPlayed)), prefix: new HarmonyMethod(typeof(GeneratedArrowProbe), nameof(AutoPlayed)));
        harmony.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardPlayed)), prefix: new HarmonyMethod(typeof(GeneratedArrowProbe), nameof(BeforePlay)));
        harmony.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.AfterCardPlayed)), prefix: new HarmonyMethod(typeof(GeneratedArrowProbe), nameof(AfterPlay)));
        try
        {
            var context = new ThrowingPlayerChoiceContext();
            using var single = KnowledgeCombat.SingleCard();
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
            ResetEvents();
            var watch = Stopwatch.StartNew();
            await CardActions.Fly(context, player, 7, target);
            Verify("Meteor: generation and deployment return within 0.35 seconds", watch.Elapsed.TotalSeconds < .35, watch.Elapsed.TotalSeconds);
            Events("Meteor", 1);
            var meteor = ArrowCombat.Deployed.Single();
            int meteorVisuals = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows").GetChildren().OfType<ArrowDeploymentVfx>().Count();
            Verify("Meteor: actual deployment visual survives early return", meteorVisuals == 1, meteorVisuals);
            Verify("Meteor: countdown and target are intact", meteor.Remaining == 2 && meteor.Target == target, meteor.Remaining);
            await ArrowCombat.Accelerate(context, meteor, 99);
            Verify("Meteor: exact damage, free play and exhaust", target.CurrentHp == 99993 && player.PlayerCombatState.Energy == 30 && meteor.Card.Pile?.Type == PileType.Exhaust, new { target.CurrentHp, player.PlayerCombatState.Energy });

            await CardPileCmd.Add(meteor.Card, PileType.Hand, skipVisuals: true);
            watch.Restart();
            await TestGameApi.PaidPlay(context, meteor.Card, target);
            Verify("a reused Meteor no longer has the generated-only fast flag", watch.Elapsed.TotalSeconds > .5, watch.Elapsed.TotalSeconds);
            await ArrowCombat.Accelerate(context, ArrowCombat.Find(meteor.Card)!, 99);

            var star = CardCatalog.Create("U_CARD_005", player);
            await CardPileCmd.Add(star, PileType.Hand, skipVisuals: true);
            watch.Restart();
            await TestGameApi.PaidPlay(context, star, target);
            double handPlaySeconds = watch.Elapsed.TotalSeconds;
            Verify("hand-played Stardust retains its ordinary presentation", handPlaySeconds > .5, handPlaySeconds);
            ResetEvents();
            decimal hp = target.CurrentHp;
            watch.Restart();
            await ArrowCombat.Accelerate(context, ArrowCombat.Find(star)!, 3);
            Verify("Stardust: hit and two generated children return within 1.1 seconds", watch.Elapsed.TotalSeconds < 1.1, watch.Elapsed.TotalSeconds);
            Events("Stardust children", 2);
            var children = ArrowCombat.Deployed.ToArray();
            Verify("Stardust: two free children, one hit and parent exhaust", children.Length == 2 && children.All(a => a.Remaining == 3 && a.Target == target) &&
                hp - target.CurrentHp == 8 && player.PlayerCombatState.Energy == 27 && star.Pile?.Type == PileType.Exhaust,
                new { children = children.Length, damage = hp - target.CurrentHp, player.PlayerCombatState.Energy });
            int childVisuals = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows").GetChildren().OfType<ArrowDeploymentVfx>().Count();
            Verify("Stardust: child visuals continue together after resolution", childVisuals == 2, childVisuals);
            foreach (var child in children) await ArrowCombat.Accelerate(context, child, 99);
            Verify("Stardust: early child completion preserves all descendants", ArrowCombat.Deployed.Count == 4, ArrowCombat.Deployed.Count);
            await Task.Delay(1200);
            var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
            Verify("all finished generation and release nodes are reclaimed", !hud.GetChildren().Any(n => n is ArrowDeploymentVfx or ArrowReleaseVfx),
                hud.GetChildren().Count(n => n is ArrowDeploymentVfx or ArrowReleaseVfx));
            var configuration = CoreApi.Configuration.Current;
            try
            {
                foreach (var level in new[] { CoreVfxLevel.Reduced, CoreVfxLevel.Off })
                {
                    CoreApi.Configuration.Update(configuration with { VfxLevel = level });
                    decimal beforeHp = target.CurrentHp;
                    await CardActions.Fly(context, player, 5, target);
                    var arrow = ArrowCombat.Deployed.Last();
                    await ArrowCombat.Accelerate(context, arrow, 99);
                    await Task.Delay(700);
                    Verify(level + ": generated arrow retains damage and exhaust", beforeHp - target.CurrentHp == 5 && arrow.Card.Pile?.Type == PileType.Exhaust, beforeHp - target.CurrentHp);
                    Verify(level + ": generated card and animation nodes are reclaimed",
                        !NCombatRoom.Instance.Ui.PlayContainer.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Cards.NCard>().Any(c => c.Model == arrow.Card) &&
                        !hud.GetChildren().Any(n => n is ArrowDeploymentVfx or ArrowReleaseVfx), hud.GetChildren().Count(n => n is ArrowDeploymentVfx or ArrowReleaseVfx));
                }
            }
            finally { CoreApi.Configuration.Update(configuration); }
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        string path = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/arrow-play-speed/generated-checks.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!))
            throw new InvalidOperationException("Generated arrow cadence regression failed");
    }
}
