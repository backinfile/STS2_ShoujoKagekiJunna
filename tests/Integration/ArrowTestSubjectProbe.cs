using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowTestSubjectProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_test_subject";
    public override string Args => "check [single|group|support|pending] | prepare | inspect";
    public override string Description => "Check arrow completion while Test Subject waits to revive.";
    public override bool IsNetworked => false;
    private static readonly List<CardModel> Cards = [];
    private static readonly List<object> Checks = [];
    private static bool _passed;
    private static string Output => Path.GetFullPath("../../../out/tmp/experiment-death-zero-arrows");

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "inspect") { Save(player); return new(true, "Saved"); }
        return new(Run(player, args.Contains("prepare"), args.ElementAtOrDefault(1) ?? "single"), true, "Started");
    }

    private static void Verify(string name, bool pass)
    {
        Checks.Add(new { name, pass });
        _passed &= pass;
    }

    private static void Save(Player player, string? error = null)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "results.json"), JsonSerializer.Serialize(new
        {
            completed = true, error, checks = Checks,
            enemies = player.Creature.CombatState?.Enemies.Select(e => new { id = e.Monster?.Id.Entry, e.CurrentHp, e.IsAlive, e.IsHittable }),
            cards = Cards.Select(c => new { id = CardCatalog.DesignId(c), pile = c.Pile?.Type.ToString(), remaining = ArrowCombat.Find(c)?.Remaining }),
            pass = error == null && _passed && !ArrowCombat.Deployed.Any(a => a.Remaining == 0)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task Run(Player player, bool prepare, string scenario)
    {
        try
        {
            var context = new ThrowingPlayerChoiceContext();
            var target = player.Creature.CombatState!.Enemies.Single(e => e.Monster?.Id.Entry == "TEST_SUBJECT");
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: !prepare); }
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            player.PlayerCombatState!.Energy = 20;
            Cards.Clear(); Checks.Clear(); _passed = true;
            using var single = KnowledgeCombat.SingleCard();
            async Task<CardModel> Play(string id)
            {
                var card = CardCatalog.Create(id, player);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
                await TestGameApi.PaidPlay(context, card, card.TargetType == TargetType.AnyEnemy ? target : null);
                return card;
            }
            Cards.Add(await Play("JB03"));
            Cards.Add(await Play(scenario == "group" ? "JA77" : scenario == "support" ? "JA80" : "JB03"));
            await CreatureCmd.SetCurrentHp(target, 1);
            if (prepare)
            {
                await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JA71", player), PileType.Hand, player);
                Directory.CreateDirectory(Output);
                File.WriteAllText(Path.Combine(Output, "prepared.json"), "{}");
                return;
            }
            if (scenario is "group" or "support") await Play("JB01");
            int energy = player.PlayerCombatState.Energy;
            if (scenario == "pending")
            {
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(Cards[0])!, 99);
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(Cards[1])!, 1);
                Verify("unfinished arrow keeps its original reviving target and countdown",
                    ArrowCombat.Find(Cards[1]) is { Remaining: 2 } pending && pending.Target == target && Cards[1].Pile?.Type == ArrowPile.PileId);
            }
            else
            {
                await ArrowCombat.AccelerateAll(context, player, 99);
                Verify("all ready arrows leave deployment and reach discard",
                    Cards.All(c => ArrowCombat.Find(c) == null && c.Pile?.Type == PileType.Discard));
            }
            Verify("native knockout keeps combat running with no hittable target",
                !target.IsAlive && !target.IsHittable && target.CombatState != null && !CombatManager.Instance.IsOverOrEnding);
            if (scenario == "support") Verify("self-target arrow still grants its energy during knockout", player.PlayerCombatState.Energy == energy + 3);
            await Task.Delay(500);
            Save(player);
        }
        catch (Exception error) { Save(player, error.ToString()); }
    }
}
