using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class KnowledgeGroupRegressionProbe : AbstractConsoleCmd
{
    private static CardModel? _source;
    private static string Report(string file) => Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory,
        "../../../out/tmp/scroll-box-stacks-20261107/", file));
    public override string CmdName => "junna_group_regression";
    public override string Args => "dynamic | layout | inspect";
    public override string Description => "Check merged knowledge groups and fitting preview layouts.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        if (args.FirstOrDefault() == "generated") return new(CheckGenerated(player), true, "Generated knowledge checks started");
        if (args.FirstOrDefault() == "inspect" && _source != null)
        { NGame.Instance!.GetInspectCardScreen().Open([_source], 0); return new(true, "Opened acquired dynamic group"); }
        return new(args.FirstOrDefault() == "layout" ? CheckLayout() : CheckDynamic(player), true, "Knowledge regression started");
    }
    private static async Task CheckGenerated(Player player)
    {
        var rows = new List<object>();
        async Task Reset()
        {
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
        }
        foreach (var id in new[] { "A", "JK148" })
        {
            await Reset();
            var card = CardCatalog.Create(id, player);
            card.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            int expected = KnowledgeCombat.Group(card)!.Members.Sum(member => member.Count);
            rows.Add(new { name = id + " native combat generation grants the whole knowledge group", pass = player.PlayerCombatState!.Hand.Cards.Count == expected });
            rows.Add(new { name = id + " keeps the generated source free this turn", pass = CardActions.Cost(card) == 0 });
        }
        await Reset();
        await CardActions.Generate(player, "B", 1, PileType.Hand);
        rows.Add(new { name = "explicit single-card generation still creates exactly one card", pass = player.PlayerCombatState!.Hand.Cards.Count == 1 });
        await Reset();
        var original = CardCatalog.Create("JB01", player);
        await CardPileCmd.AddGeneratedCardToCombat(original, PileType.Hand, player);
        var replacement = CardCatalog.Create("A", player);
        await CardCmd.Transform(original, replacement, CardPreviewStyle.None);
        rows.Add(new { name = "native combat transform grants knowledge companions", pass = player.PlayerCombatState!.Hand.Cards.Count == 3 });
        File.WriteAllText(Report("generated-knowledge-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task CheckDynamic(Player player)
    {
        NGame.Instance!.GetInspectCardScreen().Close();
        var source = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
        var paper = player.RunState.CreateCard(CardCatalog.Model("JK148"), player);
        var speed = player.RunState.CreateCard(CardCatalog.Model("JA105"), player);
        int before = player.Deck.Cards.Count;
        await DynamicKnowledge.Prepare(new ThrowingPlayerChoiceContext(), source, [source, paper, speed]);
        await CardPileCmd.Add(source, PileType.Deck, skipVisuals: true);
        _source = source;
        var rows = new List<object>();
        void Check(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        Check("acquisition grants source, speed and six paper cards once", player.Deck.Cards.Count - before, 8);
        var members = player.Deck.Cards.Where(card => card == source || card == speed || card == paper ||
            KnowledgeCombat.Group(card)?.MainCardId == "JK148" && player.Deck.Cards.ToList().IndexOf(card) >= before).ToArray();
        Check("merged source sees every acquired member", DynamicKnowledge.Members(source).Count, 8);
        Check("paper preview includes the merged group", KnowledgePreviewCards.Create(paper).Count, 8);
        Check("speed description displays knowledge", speed.GetDescriptionForPile(PileType.Deck).Contains("知识"), true);
        foreach (var member in members)
        {
            Check(member.Title + " shares the complete group", DynamicKnowledge.Members(member).Count, 8);
            Check(member.Title + " has one relation tooltip", member.HoverTips.OfType<HoverTip>().Count(tip =>
                tip.Id?.StartsWith("junna_knowledge_group") == true || tip.Id == "junna_dynamic_knowledge_group"), 1);
        }
        var copy = (CardModel)paper.MutableClone();
        Check("preview clone does not duplicate its source in members", DynamicKnowledge.Members(copy).Count, 8);
        var inspect = NGame.Instance!.GetInspectCardScreen();
        inspect.Open([source], 0);
        await Task.Delay(500);
        var scroll = inspect.GetNodeOrNull<ScrollContainer>("JunnaKnowledgePreview/Cards");
        Check("eight-card merged group defaults to spread", scroll?.Visible == true, true);
        Check("eight-card merged group fits without scrollbar", scroll?.GetVScrollBar().Visible == false, true);
        File.WriteAllText(Report("dynamic-group-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task CheckLayout()
    {
        var rows = new List<object>();
        var inspect = NGame.Instance!.GetInspectCardScreen();
        inspect.Close();
        await Task.Delay(200);
        foreach (var id in new[] { "JK122", "A", "JK148" })
        {
            var source = (CardModel)CardCatalog.Model(id).MutableClone();
            inspect.Open([source], 0);
            await Task.Delay(500);
            var scroll = inspect.GetNode<ScrollContainer>("JunnaKnowledgePreview/Cards");
            rows.Add(new { name = id + " defaults to the spread layout", pass = scroll.Visible });
            if (!scroll.Visible)
            {
                var preview = inspect.GetNode<KnowledgeGroupPreview>("JunnaKnowledgePreview");
                AccessTools.Field(typeof(KnowledgeGroupPreview), "_spread").SetValue(preview, true);
                AccessTools.Method(typeof(KnowledgeGroupPreview), "RenderCards").Invoke(preview, null);
                await Task.Delay(200);
            }
            var grid = scroll.FindChildren("*", "GridContainer", true, false).OfType<GridContainer>().Single();
            var bounds = scroll.GetGlobalRect();
            var cards = grid.GetChildren().OfType<Control>().ToArray();
            rows.Add(new { name = id + " fits without vertical scrollbar", pass = !scroll.GetVScrollBar().Visible });
            rows.Add(new { name = id + " shows every card fully", pass = cards.All(slot => bounds.Encloses(slot.GetGlobalRect())) });
            inspect.Close();
            await Task.Delay(200);
        }
        inspect.Close();
        File.WriteAllText(Report("layout-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
