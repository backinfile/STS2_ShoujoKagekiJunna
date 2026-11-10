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

namespace JunnaCompatibilityProbe;

public sealed class PartnerTimingCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_partner_timing";
    public override string Args => "check | prepare";
    public override string Description => "Check that integrated knowledge uses partners already held before play.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Run(player, args.Contains("prepare")), true, "Partner timing checks started");
    private static async Task Run(Player player, bool prepare)
    {
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/partner-timing"));
        Directory.CreateDirectory(directory);
        var rows = new List<object>();
        void Save() => File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        void Expect(string name, bool pass) { rows.Add(new { name, pass }); Save(); }
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.First();
        await CreatureCmd.SetMaxHp(target, 10000);
        await CreatureCmd.SetCurrentHp(target, 10000);
        foreach (bool upgraded in prepare ? new[] { true } : new[] { false, true })
        foreach (var location in prepare ? new[] { PileType.Draw } : new[] { PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Hand })
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
            using var single = KnowledgeCombat.SingleCard();
            async Task<CardModel> Add(string id, PileType pile)
            {
                var card = CardCatalog.Create(id, player);
                await CardPileCmd.Add(card, pile);
                return card;
            }
            var fusion = await Add("JK254", PileType.Hand);
            if (upgraded) { fusion.UpgradeInternal(); fusion.FinalizeUpgradeInternal(); }
            var argument = await Add("JK11B", PileType.Hand);
            var counter = await Add("JK11", location);
            var other = await Add("JB01", PileType.Hand);
            player.PlayerCombatState!.Energy = 20;
            if (prepare) { await Add("JK11", PileType.Hand); return; }
            await CardActions.PaidPlay(context, fusion);
            string scenario = (upgraded ? "upgraded " : "base ") + location;
            bool initiallyHeld = location == PileType.Hand;
            if (upgraded && initiallyHeld)
            {
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(power => power.DesignId == "JK254");
                await power.BeforeCardPlayed(new CardPlay
                {
                    Card = argument, Player = player, Target = target, ResultPile = PileType.Discard,
                    Resources = new() { EnergySpent = 1, EnergyValue = 1, StarsSpent = 0, StarValue = 0 },
                    IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
                });
                Expect(scenario + " discount applies before the card effect", CardActions.Cost(counter) == 0);
                Expect(scenario + " unrelated card keeps its cost before the effect", CardActions.Cost(other) == 1);
            }
            int before = target.CurrentHp;
            int block = player.Creature.Block;
            await CardActions.PaidPlay(context, argument);
            Expect(scenario + " counterargument follows pre-play hand membership", (counter.Pile?.Type == PileType.Hand) == (upgraded || !initiallyHeld));
            Expect(scenario + " source deals its normal damage", target.CurrentHp == before - argument.DynamicVars.Damage.IntValue);
            Expect(scenario + " only base fusion plays an initially held partner", player.Creature.Block == block + (!upgraded && initiallyHeld ? counter.DynamicVars.Block.IntValue : 0));
            Expect(scenario + " unrelated card remains held", other.Pile?.Type == PileType.Hand);
            if (upgraded)
            {
                Expect(scenario + " only an initially held partner receives a discount", CardActions.Cost(counter) == (initiallyHeld ? 0 : 1));
                int energy = player.PlayerCombatState!.Energy;
                await CardActions.PaidPlay(context, counter, target);
                Expect(scenario + " partner pays its expected cost", player.PlayerCombatState.Energy == energy - (initiallyHeld ? 0 : 1));
                Expect(scenario + " discount expires after use", CardActions.Cost(counter) == 1);
                Expect(scenario + " recalled argument keeps its original cost", CardActions.Cost(argument) == 1);
            }
        }
        Save();
    }
}
