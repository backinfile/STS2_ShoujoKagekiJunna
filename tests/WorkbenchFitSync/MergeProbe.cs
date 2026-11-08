using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class MergeCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_merge_check";
    public override string Args => "check";
    public override string Description => "Scoped checks of merged runtime fixes and current card rules.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Started");
    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        async Task Reset()
        {
            foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
                if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
            foreach (var enemy in player.Creature.CombatState!.Enemies)
            {
                await CreatureCmd.SetMaxHp(enemy, 100000); await CreatureCmd.SetCurrentHp(enemy, 100000);
                await CreatureCmd.LoseBlock(context, enemy, enemy.Block, null);
            }
            await CreatureCmd.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState.Energy = 99;
        }
        async Task<MegaCrit.Sts2.Core.Models.CardModel> Card(string id, bool upgraded = false)
        {
            using var single = KnowledgeCombat.SingleCard();
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            return card;
        }
        var foe = player.Creature.CombatState!.Enemies.First();
        try
        {
            foreach (bool upgraded in new[] { false, true })
            {
                await Reset();
                await CardActions.PaidPlay(context, await Card("JA38"), foe);
                await CardActions.PaidPlay(context, await Card("JA15"));
                Expect("two waiting arrows " + upgraded, ArrowCombat.Deployed.Count == 2);
                await CardActions.PaidPlay(context, await Card("JA71", upgraded));
                Expect("current completion rule " + upgraded, ArrowCombat.Deployed.Count == 0);
                Expect("current block value " + upgraded, player.Creature.Block == (upgraded ? 16 : 12));
                await Reset();
                var repeating = await Card("JA67", upgraded);
                await CardActions.PaidPlay(context, repeating, foe);
                var old = ArrowCombat.Find(repeating)!; int energy = player.PlayerCombatState!.Energy;
                await ArrowCombat.Accelerate(context, old, int.MaxValue);
                var next = ArrowCombat.Find(repeating);
                Expect("free redeployment " + upgraded, next != null && next != old && player.PlayerCombatState.Energy == energy);
                Expect("redeployment target " + upgraded, next?.Target == foe);
            }
            await Reset();
            await Card("B"); await Card("B"); var argument = await Card("JK48");
            int hp = foe.CurrentHp;
            await CardActions.PaidPlay(context, argument, foe);
            Expect("merged knowledge damage calculation", hp - foe.CurrentHp == 8);
            Expect("current catalog revision", CardCatalog.Revision == 4011 && CardCatalog.Cards.Count == 95);
        }
        catch (Exception ex) { rows.Add(new { name = "exception", pass = false, error = ex.ToString() }); }
        File.WriteAllText(Path.GetFullPath("../merge-results.json", Environment.CurrentDirectory), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
