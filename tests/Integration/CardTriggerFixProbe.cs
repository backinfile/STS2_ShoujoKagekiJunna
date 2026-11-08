using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKageki.Core.V1;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class CardTriggerFixCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_trigger_fixes";
    public override string Args => "prepare";
    public override string Description => "Targeted opening, retarget and random acceleration regressions.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => args.Contains("encounters") ? new(true, string.Join(" ", ModelDb.AllEncounters.Select(e => e.Id.Entry))) : player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.Contains("prepare") ? CardTriggerFixProbe.Prepare(player) : CardTriggerFixProbe.Run(player), true, "Targeted checks started");
}

public static class CardTriggerFixProbe
{
    private sealed class NoSelection : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => throw new Exception("Unexpected selection: acceleration must choose a random arrow");
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    public static async Task Prepare(Player player)
    {
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray()) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies) { await CreatureCmd.SetMaxHp(enemy, 1000); await CreatureCmd.SetCurrentHp(enemy, 1000); }
        player.PlayerCombatState.Energy = 8;
        using var single = KnowledgeCombat.SingleCard();
        foreach (var id in new[] { "JK37", "JA118", "JK37C", "JK37B", "JB06", "JB03" })
            await CardPileCmd.Add(CardCatalog.Create(id, player), id == "JK37" ? PileType.Draw : PileType.Hand, skipVisuals: false);
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), player.Creature.CombatState!);
        await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), player.Creature, player.Creature.Block, null);
    }
    public static async Task Run(Player player)
    {
        string report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "trigger-fix-results.json"));
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        var hooks = ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>());
        var target = player.Creature.CombatState!.HittableEnemies.First();
        void Expect(bool pass, string message) { if (!pass) throw new Exception(message); }
        async Task Reset()
        {
            await hooks.BeforeCombatStart();
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var c in player.PlayerCombatState!.AllCards.ToArray())
                await CardPileCmd.RemoveFromCombat(c, skipVisuals: true);
            foreach (var enemy in player.Creature.CombatState!.HittableEnemies) { await CreatureCmd.SetMaxHp(enemy, 100000); await CreatureCmd.SetCurrentHp(enemy, 100000); }
            player.PlayerCombatState.Energy = 20;
        }
        async Task<CardModel> Card(string id, PileType pile = PileType.Hand, bool upgraded = false)
        {
            using var single = KnowledgeCombat.SingleCard();
            var c = CardCatalog.Create(id, player);
            if (upgraded) { c.UpgradeInternal(); c.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(c, pile, skipVisuals: true);
            return c;
        }
        Task<bool> Play(CardModel card) => CardActions.PaidPlay(context, card, target);
        async Task Case(string name, Func<Task> test)
        {
            try { await Reset(); await test(); rows.Add(new { name, pass = true }); }
            catch (Exception ex) { rows.Add(new { name, pass = false, error = ex.ToString() }); }
            File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        await Case("starting configuration", () =>
        {
            var character = ModelDb.Character<Junna>();
            var deck = character.StartingDeck.ToArray();
            Expect(character.StartingHp == 70 && character.StartingGold == 99, "Starting life and gold");
            Expect(deck.Length == 10 && deck.Count(c => CardCatalog.DesignId(c) == "JB01") == 4 && deck.Count(c => CardCatalog.DesignId(c) == "JB06") == 1 && deck.All(c => CardCatalog.DesignId(c) != "JH26"), "Starting deck configuration");
            Expect(character.StartingRelics.Single().Id.Entry == "JUNNA_GREEN_GEM", "Starting relic configuration");
            return Task.CompletedTask;
        });
        using var selector = CardSelectCmd.UseSelector(new NoSelection());
        foreach (bool up in new[] { false, true })
        {
            await Case("opening arrow " + up, async () =>
            {
                var arrow = await Card("JK37", PileType.Draw, up);
                var partner = await Card("JK37B", PileType.Draw);
                int energy = player.PlayerCombatState!.Energy;
                await hooks.BeforeHandDraw(player, context, player.Creature.CombatState!);
                Expect(ArrowCombat.Find(arrow)?.Remaining == 9, "Opening arrow was not deployed at combat start");
                Expect(ArrowCombat.Find(arrow)?.Target == target, "Opening arrow needs a legal enemy target");
                Expect(partner.Pile?.Type == PileType.Draw && player.PlayerCombatState.Energy == energy, "Opening play changed partner or energy");
                await hooks.BeforeHandDraw(player, context, player.Creature.CombatState!);
                Expect(ArrowCombat.Deployed.Count == 1, "Opening arrow played twice");
            });
            await Case("retarget attack " + up, async () =>
            {
                var other = player.Creature.CombatState!.HittableEnemies.First(e => e != target);
                var arrow = await Card("JK37"); await CardActions.PaidPlay(context, arrow, other);
                var self = await Card("JH11"); await Play(self);
                var group = await Card("JA77"); await Play(group);
                var attack = await Card("JK37B", upgraded: up);
                int hp = target.CurrentHp;
                await Play(attack);
                Expect(hp - target.CurrentHp == (up ? 6 : 4), "Attack damage mismatch");
                Expect(ArrowCombat.Find(arrow)?.Target == target && ArrowCombat.Find(arrow)?.Remaining == 9, "Retarget changed countdown or target");
                Expect(attack.Pile?.Type == PileType.Discard, "Attack did not finish");
                Expect(ArrowCombat.Find(self)?.Target == player.Creature && ArrowCombat.Find(group)?.TargetsAllEnemies == true, "Self and group arrows retain their target categories");
            });
            await Case("starter block and Shine " + up, async () =>
            {
                var card = await Card("JB06", upgraded: up);
                Expect(CardActions.Cost(card) == (up ? 0 : 1), "Starter upgrade changes cost only");
                int before = player.Creature.Block;
                await Play(card);
                Expect(player.Creature.Block - before == 9, "Starter grants nine block");
                Expect(Shine.GetState(card)?.Remaining == 8 && Shine.GetState(card)?.Maximum == 9, "Starter spends one of nine Shine");
            });
            await Case("arrow defense " + up, async () =>
            {
                int before = player.Creature.Block;
                await Play(await Card("JA118", upgraded: up));
                Expect(player.Creature.Block == before, "Power installs without granting block");
                var arrow = await Card("JB03"); await Play(arrow);
                Expect(player.Creature.Block - before == (up ? 4 : 3), "Arrow deployment grants configured block");
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(arrow)!, 3);
                Expect(player.Creature.Block - before == (up ? 4 : 3), "Arrow completion does not grant block again");
            });
            await Case("random acceleration " + up, async () =>
            {
                var a = await Card("JK37"); await Play(a);
                var b = await Card("JB03"); await Play(b);
                await Play(await Card("JK37C", upgraded: up));
                int before = ArrowCombat.Deployed.Sum(a => a.Remaining);
                await Play(await Card("JH26"));
                Expect(before - ArrowCombat.Deployed.Sum(a => a.Remaining) == 1, "Exactly one random arrow must accelerate by one");
            });
        }
        await Case("retarget attack killing its target", async () =>
        {
            var other = player.Creature.CombatState!.HittableEnemies.First(e => e != target);
            var arrow = await Card("JK37"); await Play(arrow);
            await CreatureCmd.SetCurrentHp(target, 4);
            var attack = await Card("JK37B"); await Play(attack);
            Expect(ArrowCombat.Find(arrow)?.Target == other && ArrowCombat.Find(arrow)?.Remaining == 9, "Killed target must retain natural arrow retargeting");
            Expect(attack.Pile?.Type == PileType.Discard, "Lethal attack must finish");
        });
    }
}
