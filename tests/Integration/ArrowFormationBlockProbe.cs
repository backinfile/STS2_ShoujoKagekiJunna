using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowFormationBlockProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_formation_block";
    public override string Args => "check <phase>";
    public override string Description => "Verify Arrow Formation's actual triggered block with and without Frail.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player, args.ElementAtOrDefault(1) ?? "block"), true, "Checking formation block");
    private static async Task Check(Player player, string phase)
    {
        string root = System.Environment.GetEnvironmentVariable("JUNNA_KNOWLEDGE_STACK_OUTPUT")!;
        var rows = new List<object>();
        void Expect(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.First(enemy => enemy.IsAlive);
        async Task Reset(bool skipVisuals = true)
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).ToArray()) { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: skipVisuals); }
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState!.Energy = 30;
        }
        async Task<CardModel> Add(string id, bool upgraded = false)
        {
            var card = player.Creature.CombatState!.CreateCard(CardCatalog.Model(id), player);
            if (upgraded) card.UpgradeInternal();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            return card;
        }
        if (phase == "prepare")
        {
            await Reset(skipVisuals: false);
            await PowerCmd.Apply<FrailPower>(context, player.Creature, 2, target, null);
            var formation = await Add("JA118");
            await TestGameApi.PaidPlay(context, formation, target);
            await Add("JB03");
            await Add("JB03");
            player.PlayerCombatState!.Energy = 3;
            return;
        }
        try
        {
            foreach (bool upgraded in new[] { false, true }) foreach (bool frail in new[] { false, true })
            {
                await Reset();
                var formation = await Add("JA118", upgraded);
                await TestGameApi.PaidPlay(context, formation, target);
                if (frail) await PowerCmd.Apply<FrailPower>(context, player.Creature, 2, target, null);
                int before = player.Creature.Block;
                var arrow = await Add("JB03");
                await TestGameApi.PaidPlay(context, arrow, target);
                Expect($"Arrow Formation upgraded={upgraded} frail={frail} actual block", player.Creature.Block - before, upgraded ? 3 : 2);
                Expect($"Arrow Formation upgraded={upgraded} frail={frail} arrow deployed", ArrowCombat.Find(arrow) != null, true);
            }
            await Reset();
            await PowerCmd.Apply<DexterityPower>(context, player.Creature, 3, player.Creature, null);
            var dexFormation = await Add("JA118");
            await TestGameApi.PaidPlay(context, dexFormation, target);
            var dexArrow = await Add("JB03");
            await TestGameApi.PaidPlay(context, dexArrow, target);
            Expect("Arrow Formation ignores Dexterity", player.Creature.Block, 2);
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            var dexDefend = await Add("JB02");
            await TestGameApi.PaidPlay(context, dexDefend, target);
            Expect("ordinary Defend still receives Dexterity", player.Creature.Block, 8);
            await Reset();
            await PowerCmd.Apply<FrailPower>(context, player.Creature, 2, target, null);
            var defend = await Add("JB02");
            await TestGameApi.PaidPlay(context, defend, target);
            Expect("ordinary Defend still receives Frail", player.Creature.Block, 3);
        }
        catch (Exception error) { rows.Add(new { name = "probe exception", pass = false, error = error.ToString() }); }
        File.WriteAllText(Path.Combine(root, phase + ".json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
