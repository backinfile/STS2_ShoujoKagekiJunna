using System.Text.Json;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Rewards;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class RecallAllCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_recall_all";
    public override string Args => "check | prepare";
    public override string Description => "Checks that arguments recall every existing counterpart.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private sealed class Selector : ICardSelector
    {
        public int Calls;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        { Calls++; return Task.FromResult(options.Take(maxSelect)); }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static async Task Reset(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            await CreatureCmd.SetMaxHp(enemy, 100000);
            await CreatureCmd.SetCurrentHp(enemy, 100000);
        }
        player.PlayerCombatState.Energy = 99;
    }
    private static async Task<CardModel> Card(Player player, string id, PileType pile, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, skipVisuals: false);
        return card;
    }
    private static Task<bool> Play(CardModel card) => CardActions.PaidPlay(Context, card, card.Owner.Creature.CombatState!.Enemies.First());
    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        var selector = new Selector();
        using var choices = CardSelectCmd.UseSelector(selector);
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 3;
        void Expect(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        try
        {
            foreach (var id in new[] { "JK11", "JK11B" })
            foreach (bool upgraded in new[] { false, true })
            {
                await Reset(player);
                selector.Calls = 0;
                string partner = id == "JK11" ? "JK11B" : "JK11";
                var counterparts = new List<CardModel>();
                foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Draw, PileType.Discard, PileType.Exhaust })
                    counterparts.Add(await Card(player, partner, pile, counterparts.Count % 2 == 1));
                var versions = counterparts.Select(c => c.IsUpgraded).ToArray();
                var executing = await Card(player, partner, PileType.Play);
                var unrelated = await Card(player, "JB01", PileType.Discard);
                var source = await Card(player, id, PileType.Hand, upgraded);
                int before = player.PlayerCombatState!.AllCards.Count();
                string label = id + " upgraded=" + upgraded;
                Expect(label + " paid play", await Play(source), true);
                Expect(label + " every actual counterpart in hand", counterparts.All(c => c.Pile?.Type == PileType.Hand), true);
                Expect(label + " versions preserved", counterparts.Select(c => c.IsUpgraded).SequenceEqual(versions), true);
                Expect(label + " no selection", selector.Calls, 0);
                Expect(label + " executing card excluded", executing.Pile!.Type, PileType.Play);
                Expect(label + " unrelated card preserved", unrelated.Pile!.Type, PileType.Discard);
                Expect(label + " no extra cards generated", player.PlayerCombatState.AllCards.Count(), before);
                Expect(label + " normal source destination", source.Pile!.Type, PileType.Discard);
                await Reset(player);
                source = await Card(player, id, PileType.Hand, upgraded);
                Expect(label + " no counterpart paid play", await Play(source), true);
                Expect(label + " no missing counterpart generated", CardActions.Named(player, partner).Count(), 0);
            }
        }
        catch (Exception error) { rows.Add(new { name = "exception", pass = false, error = error.ToString() }); }
        finally
        {
            Godot.Engine.TimeScale = speed;
            string path = Path.GetFullPath("../../../out/tmp/recall-all/results.json", Environment.CurrentDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player);
        await Card(player, "JK11", PileType.Hand);
        foreach (var pile in new[] { PileType.Draw, PileType.Discard, PileType.Exhaust })
            await Card(player, "JK11B", pile);
        foreach (var pile in new[] { PileType.Draw, PileType.Discard, PileType.Exhaust })
            await Card(player, "JK11", pile);
    }
}
