using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class CloningKnowledgeProbe : AbstractConsoleCmd
{
    private static CardModel[] _originals = [];
    private static string[] _states = [];
    private static CardModel _main = null!;
    private static string State(CardModel card) => JsonSerializer.Serialize(new
    {
        id = card.Id.ToString(), card.IsUpgraded, shine = Shine.GetState(card),
        enchantment = card.Enchantment?.Id.ToString()
    });
    public override string CmdName => "junna_cloning_knowledge";
    public override string Args => "prepare | run | check";
    public override string Description => "Check native rest-site cloning of associated knowledge cards.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player == null
        ? new(false, "Run required") : new(args.FirstOrDefault() switch
        {
            "prepare" => Prepare(player), "check" => Check(player), _ => Run(player)
        }, true, "Cloning knowledge probe started");

    private static async Task Prepare(Player player)
    {
        await CardPileCmd.RemoveFromDeck(player.Deck.Cards.ToArray(), showPreview: false);
        CardModel Create(string id) => player.RunState.CreateCard(CardCatalog.Model(id), player);
        _main = Create("JK314");
        CardCmd.Upgrade(_main);
        Shine.SetState(_main, 2, 3);
        var member = Create("JK32B");
        var ordinary = Create("JB01");
        var dynamic = Create("JK126");
        using (KnowledgeCombat.SingleCard())
            await CardPileCmd.Add(new[] { _main, member, ordinary, dynamic }, PileType.Deck, skipVisuals: true);
        var external = player.RunState.CreateCard<StrikeIronclad>(player);
        await DynamicKnowledge.Prepare(new ThrowingPlayerChoiceContext(), dynamic, new[] { dynamic, Create("JB02"), external });
        await DynamicKnowledge.Acquire(dynamic, PileType.Deck);
        foreach (var card in new[] { _main, member, ordinary, dynamic }) CardCmd.Enchant<Clone>(card, 4m);
        _originals = player.Deck.Cards.ToArray();
        _states = _originals.Select(State).ToArray();
    }
    private static async Task Run(Player player)
    {
        await Prepare(player);
        await new CloneRestSiteOption(player).OnSelect();
        await Check(player);
    }
    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        try
        {
            Expect("setup captured six original cards", _originals.Length == 6);
            var gained = player.Deck.Cards.Except(_originals).ToArray();
            int Count(string id) => gained.Count(card => CardCatalog.DesignId(card) == id);
            Expect("native clone adds four selected cards and seven companions", gained.Length == 11);
            Expect("knowledge root grants three companions once", Count("JK314") == 1 && Count("JK122B") == 3);
            Expect("associated knowledge card grants its own relation", Count("JK32B") == 1 && Count("JK32") == 1 && Count("JK32C") == 1);
            Expect("dynamic relation grants both Junna and foreign companions", Count("JK126") == 1 && Count("JB02") == 1
                && gained.Count(card => card is StrikeIronclad) == 1);
            Expect("ordinary enchanted card is still copied once", Count("JB01") == 1);
            Expect("all original cards retain their state and order", player.Deck.Cards.Where(_originals.Contains).SequenceEqual(_originals)
                && _originals.Select(State).SequenceEqual(_states));
            Expect("native knowledge clone preserves upgrade Shine and enchantment", gained.Any(card => State(card) == State(_main)));
            Expect("granted companions use their base versions", gained.Where(card => CardCatalog.DesignId(card) == "JK122B")
                .All(card => !card.IsUpgraded && card.Enchantment == null));
            int before = player.Deck.Cards.Count;
            await Task.Yield();
            await CardPileCmd.Add(player.RunState.CloneCard(_main), PileType.Deck, skipVisuals: true);
            Expect("unadapted native copy and completed scope stay single-card", player.Deck.Cards.Count == before + 1);
            before = player.Deck.Cards.Count;
            int enchanted = player.Deck.Cards.Count(card => card.Enchantment is Clone);
            using (KnowledgeCombat.SingleCard()) await new CloneRestSiteOption(player).OnSelect();
            Expect("explicit single-card scope suppresses companions", player.Deck.Cards.Count == before + enchanted);
        }
        catch (Exception error) { rows.Add(new { name = "probe exception", pass = false, error = error.ToString() }); }
        string directory = Path.GetFullPath("../../../out/tmp/cloning-linked-knowledge-20261117");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
