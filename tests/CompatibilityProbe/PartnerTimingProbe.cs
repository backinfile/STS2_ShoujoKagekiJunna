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
            var argument = await Add("JK11", PileType.Hand);
            var counter = await Add("JK11B", location);
            var other = await Add("JB01", PileType.Hand);
            player.PlayerCombatState!.Energy = 20;
            if (prepare) return;
            await CardActions.PaidPlay(context, fusion);
            int before = target.CurrentHp;
            await CardActions.PaidPlay(context, argument);
            bool initiallyHeld = location == PileType.Hand;
            Expect(location + " counterargument follows pre-play hand membership", (counter.Pile?.Type == PileType.Hand) != initiallyHeld);
            Expect(location + " only an initially held counterargument deals damage", target.CurrentHp == before - (initiallyHeld ? 10 : 0));
            Expect(location + " unrelated card remains held", other.Pile?.Type == PileType.Hand);
        }
        Save();
    }
}
