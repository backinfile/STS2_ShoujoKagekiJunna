using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class EventCopiesProbe : AbstractConsoleCmd
{
    private static CardModel[] _originals = [];
    private static string[] _states = [];
    public override string CmdName => "junna_event_copies";
    public override string Args => "setup | check";
    public override string Description => "Check physical card copies through the native Reflections event.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player == null
        ? new(false, "Run required")
        : new(args.FirstOrDefault() == "setup" ? Setup(player) : Check(player), true, "Event copy probe started");

    private static string State(CardModel card) => JsonSerializer.Serialize(new
    {
        id = card.Id.ToString(), card.IsUpgraded, shine = Shine.GetState(card),
    });

    private static async Task Setup(Player player)
    {
        NMapScreen.Instance?.Close(animateOut: false);
        await CardPileCmd.RemoveFromDeck(player.Deck.Cards.ToArray(), showPreview: false);
        var ordinary = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
        await CardPileCmd.Add(ordinary, PileType.Deck, skipVisuals: true);
        CardCmd.Upgrade(ordinary);
        var knowledge = player.RunState.CreateCard(CardCatalog.Model("JK314"), player);
        await AddGroup(knowledge);
        var member = player.Deck.Cards.First(card => CardCatalog.DesignId(card) == "JK122B");
        CardCmd.Upgrade(member);
        Shine.SetState(member, 2, 3);
        using (KnowledgeCombat.SingleCard())
            await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model("JK122B"), player),
                PileType.Deck, skipVisuals: true);
        await AddGroup(player.RunState.CreateCard(CardCatalog.Model("JK32"), player));
        // A missing member must stay missing when the remaining physical cards are copied.
        await CardPileCmd.RemoveFromDeck(player.Deck.Cards.First(card => CardCatalog.DesignId(card) == "JK32B"), showPreview: false);
        _originals = player.Deck.Cards.ToArray();
        _states = _originals.Select(State).ToArray();
        async Task AddGroup(CardModel main)
        {
            await CardPileCmd.Add(main, PileType.Deck, skipVisuals: true);
            foreach (var (id, count) in KnowledgeCombat.Group(main)!.Members)
                for (int i = CardCatalog.DesignId(main) == id ? 1 : 0; i < count; i++)
                    await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model(id), player), PileType.Deck, skipVisuals: true);
        }
    }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        try
        {
            Expect("setup captured a deck", _originals.Length > 0);
            var gained = player.Deck.Cards.Except(_originals).ToArray();
            rows.Add(new { name = "native event doubles the deck and adds one curse",
                originalCount = _originals.Length, actualCount = player.Deck.Cards.Count,
                pass = player.Deck.Cards.Count == 2 * _originals.Length + 1 });
            Expect("native event adds exactly one Bad Luck", gained.Count(card => card is BadLuck) == 1);
            Expect("original cards and their state are preserved", _originals.All(player.Deck.Cards.Contains)
                && _originals.Select(State).SequenceEqual(_states));
            var expected = _states.Order().ToArray();
            var actual = gained.Where(card => card is not BadLuck).Select(State).Order().ToArray();
            Expect("each physical card is copied once with upgrades and Shine intact", actual.SequenceEqual(expected));
            Expect("absent knowledge companions are not regenerated", !player.Deck.Cards.Any(card => CardCatalog.DesignId(card) == "JK32B"));
            var source = _originals.First(card => CardCatalog.DesignId(card) == "JK314");
            int before = player.Deck.Cards.Count;
            var copy = player.RunState.CloneCard(source);
            await Task.Yield();
            await CardPileCmd.Add(copy, PileType.Deck, skipVisuals: true);
            Expect("native single-card copy adds only one card", player.Deck.Cards.Count == before + 1);
            before = player.Deck.Cards.Count;
            await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model("JK314"), player), PileType.Deck, skipVisuals: true);
            Expect("unadapted acquisition grants only the specified card", player.Deck.Cards.Count == before + 1);
        }
        catch (Exception ex) { rows.Add(new { name = "probe exception", pass = false, error = ex.ToString() }); }
        string directory = Path.GetFullPath("../../../out/tmp/event-copies-20261110");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "integration-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
