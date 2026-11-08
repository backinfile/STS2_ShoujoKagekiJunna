using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class KnowledgeTransformProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_transform";
    public override string Args => "check";
    public override string Description => "Check native deck transformations acquire knowledge companions once.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player == null
        ? new(false, "Run required") : new(Check(player), true, "Transform checks started");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        var starters = ModelDb.Character<Junna>().StartingDeck.ToArray();
        rows.Add(new { name = "starting deck is five strikes, four defends and one full draw",
            pass = starters.Length == 10 && starters.Count(card => CardCatalog.DesignId(card) == "JB01") == 5 &&
                starters.Count(card => CardCatalog.DesignId(card) == "JB02") == 4 && starters.Count(card => CardCatalog.DesignId(card) == "JB03") == 1 });
        rows.Add(new { name = "surplus star is a common reward card", pass = CardCatalog.Get("JH26").Pool && CardCatalog.Get("JH26").CardRarity == CardRarity.Common });
        string report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/knowledge-transform-20261107/transform-results.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        async Task Case(string name, Func<Task> check)
        {
            var before = player.Deck.Cards.ToHashSet();
            try { await check(); rows.Add(new { name, pass = true }); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            finally
            {
                foreach (var card in player.Deck.Cards.Where(card => !before.Contains(card)).ToArray())
                    card.RemoveFromCurrentPile();
                File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        void Expect(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
        CardModel Create(string id) => player.RunState.CreateCard(CardCatalog.Model(id), player);
        async Task<CardModel> Original()
        {
            var card = Create("JB01");
            await CardPileCmd.Add(card, PileType.Deck);
            return card;
        }
        foreach (bool upgraded in new[] { false, true })
            await Case("native deck transform grants the complete group; upgraded=" + upgraded, async () =>
            {
                var original = await Original();
                var before = player.Deck.Cards.ToHashSet();
                var replacement = Create("A");
                if (upgraded) { replacement.UpgradeInternal(); replacement.FinalizeUpgradeInternal(); }
                var result = await CardCmd.Transform(original, replacement, CardPreviewStyle.None);
                var gained = player.Deck.Cards.Where(card => !before.Contains(card)).ToArray();
                Expect(result?.success == true && !player.Deck.Cards.Contains(original), "Native replacement failed");
                Expect(gained.Length == 3 && gained.Count(card => CardCatalog.DesignId(card) == "B") == 2,
                    "Transform should grant the acquired knowledge plus its two companions; actual=" + gained.Length);
                Expect(replacement.IsUpgraded == upgraded && gained.Where(card => card != replacement).All(card => !card.IsUpgraded), "Companion upgrade state");
                var order = player.Deck.Cards.ToList();
                int index = order.IndexOf(replacement);
                Expect(order.Skip(index).Take(3).SequenceEqual(gained), "New group must remain adjacent");
            });
        await Case("ordinary replacement adds one card", async () =>
        {
            var original = await Original(); int count = player.Deck.Cards.Count;
            await CardCmd.Transform(original, Create("JB02"), CardPreviewStyle.None);
            Expect(player.Deck.Cards.Count == count, "Ordinary transform changed deck size");
        });
        await Case("explicit single-card scope suppresses companions", async () =>
        {
            var original = await Original(); int count = player.Deck.Cards.Count;
            using var single = KnowledgeCombat.SingleCard();
            await CardCmd.Transform(original, Create("A"), CardPreviewStyle.None);
            Expect(player.Deck.Cards.Count == count, "Single-card transform granted companions");
        });
    }
}
