using System.Text.Json;
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

public sealed class ArrowTargetDeathProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_target_death";
    public override string Args => "check | prepare";
    public override string Description => "Check arrows survive enemies that summon replacements on death.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.Contains("centipede") ? CheckCentipede(player)
            : Check(player, args.Contains("prepare")), true, "Arrow death checks started");

    private static async Task CheckCentipede(Player player)
    {
        var state = player.Creature.CombatState!;
        var enemies = state.Enemies.Where(enemy => enemy.IsAlive).ToArray();
        var cards = new[] { CardCatalog.Create("JB03", player), CardCatalog.Create("JB03", player) };
        var resolved = new List<MegaCrit.Sts2.Core.Entities.Creatures.Creature?>();
        for (int index = 0; index < cards.Length; index++)
        {
            var play = new CardPlay { Card = cards[index], Target = enemies[index],
#if STS2_BETA
            Player = player,
#endif

                ResultPile = PileType.Discard, Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            ArrowCombat.Deploy(cards[index], play, 1, (_, payload) => { resolved.Add(payload.Target); return Task.CompletedTask; });
            await CardPileCmd.AddGeneratedCardToCombat(cards[index], ArrowPile.PileId, player);
        }
        await CreatureCmd.Kill(enemies[0]);
        await CreatureCmd.Kill(enemies[1]);
        var rows = new List<object>
        {
            new { name = "dead centipede segments remain in combat", pass = state.ContainsCreature(enemies[0]) && !enemies[0].IsAlive },
            new { name = "both arrows retarget the living segment", pass = cards.All(card => ArrowCombat.Find(card)?.Target == enemies[2]) }
        };
        foreach (var card in cards) await ArrowCombat.Accelerate(new ThrowingPlayerChoiceContext(), ArrowCombat.Find(card)!, 1);
        rows.Add(new { name = "zero countdown resolves every arrow on a living target", pass = resolved.Count == 2 && resolved.All(target => target == enemies[2]) });
        rows.Add(new { name = "no zero-count arrows remain deployed", pass = cards.All(card => ArrowCombat.Find(card) == null && card.Pile?.Type == PileType.Discard) });
        string report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/scroll-box-stacks-20261107/centipede-results.json"));
        File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task Check(Player player, bool prepare)
    {
        var state = player.Creature.CombatState!;
        var target = state.Enemies.Single(c => c.Monster?.Id.Entry == "PHROG_PARASITE");
        var cards = new[] { CardCatalog.Create("JB03", player), CardCatalog.Create("JA77", player) };
        foreach (var card in cards)
        {
            var play = new CardPlay { Card = card, Target = target,
#if STS2_BETA
            Player = player,
#endif
 ResultPile = PileType.Discard,
                Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            ArrowCombat.Deploy(card, play, 3, static (_, _) => Task.CompletedTask);
            await CardPileCmd.AddGeneratedCardToCombat(card, ArrowPile.PileId, player);
        }
        if (prepare)
        {
            await CreatureCmd.SetCurrentHp(target, 1);
            player.PlayerCombatState!.Energy = 9;
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JB01", player), PileType.Hand, player);
            return;
        }
        await CreatureCmd.Kill(target);
        var live = state.Enemies.Where(c => c.IsAlive).ToArray();
        var rows = new List<object>
        {
            new { name = "parasite spawns living replacements", pass = live.Length > 0 },
            new { name = "single-target arrow retargets a new living enemy", pass = ArrowCombat.Find(cards[0]) is { } arrow && live.Contains(arrow.Target) && arrow.Remaining == 3 },
            new { name = "all-enemy arrow retains deployment and countdown", pass = ArrowCombat.Find(cards[1])?.Remaining == 3 }
        };
        if (ArrowCombat.Find(cards[0])?.Target is { } next)
        {
            await CreatureCmd.Kill(next);
            rows.Add(new { name = "ordinary death retargets without changing countdown", pass = ArrowCombat.Find(cards[0]) is { } retargeted && retargeted.Target != next && retargeted.Target?.IsAlive == true && retargeted.Remaining == 3 });
        }
        await CreatureCmd.Kill(state.Enemies.Where(creature => creature.IsAlive).ToArray());
        await Task.Delay(700);
        rows.Add(new { name = "combat end clears all deployments", pass = ArrowCombat.Deployed.Count == 0 });
        string report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/knowledge-transform-20261107/arrow-death-results.json"));
        File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
