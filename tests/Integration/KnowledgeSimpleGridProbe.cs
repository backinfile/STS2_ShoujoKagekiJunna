using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class KnowledgeSimpleGridProbe : AbstractConsoleCmd
{
    private static Task<IEnumerable<CardModel>>? _selection;
    private static CardModel[] _offered = [];
    private static int _deckBefore; private static string _mode = "single";
    private static string Root => System.Environment.GetEnvironmentVariable("JUNNA_KNOWLEDGE_STACK_OUTPUT")!;
    public override string CmdName => "junna_simple_stack";
    public override string Args => "show | check <phase>";
    public override string Description => "Reproduce Brain Leech's actual reward grid and verify knowledge stacks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        if (args.FirstOrDefault() == "show")
        {
            _mode = args.ElementAtOrDefault(1) ?? "single"; _deckBefore = player.Deck.Cards.Count;
            _offered = new[] { "JH27", "JG27", "JA82", "JK11B", "JA75" }
                .Select(id => player.RunState.CreateCard(CardCatalog.Model(id), player)).ToArray();
            if (_mode == "existing") return new(ShowExisting(player), true, "Opening actual deck-card selection");
            var options = _offered.Select(card => new CardCreationResult(card)).ToList();
            var prefs = new CardSelectorPrefs(new LocString("events", "BRAIN_LEECH.pages.SHARE_KNOWLEDGE.selectionScreenPrompt"), 1) { Cancelable = false };
            if (_mode == "multi") { prefs = new CardSelectorPrefs(prefs.Prompt, 0, _offered.Length) { RequireManualConfirmation = true }; }
            _selection = CardSelectCmd.FromSimpleGridForRewards(new BlockingPlayerChoiceContext(), options, player, prefs);
            return new(true, "Opened native reward grid with the five cards from the annotation");
        }
        if (args.FirstOrDefault() == "interact") return new(Interactions(player), true, "Checking native input interactions");
        string phase = args.ElementAtOrDefault(1) ?? "baseline";
        var screen = NOverlayStack.Instance!.Peek() as NSimpleCardSelectScreen;
        if (screen == null) return new(false, "Expected native simple selection screen");
        var rows = new List<object>();
        void Check(string name, object? actual, object? expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var holders = Descendants(screen).OfType<NGridCardHolder>().Where(node => node.CardModel != null).ToArray();
        Check("native grid retains all five offers", holders.Length, 5);
        Check("opening preview grants no cards", player.Deck.Cards.Count, _deckBefore);
        Check("selection is pending", _selection?.IsCompleted, false);
        foreach (var holder in holders)
        {
            var card = holder.CardNode!;
            string id = CardCatalog.DesignId(holder.CardModel!)!;
            int expected = holder.CardModel!.Pile == null ? Math.Max(0, KnowledgePreviewCards.Create(holder.CardModel).Count - 1) : 0;
            var stack = card.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack");
            Check(id + " visible companions", stack?.GetChildren().OfType<NCard>().Count() ?? 0, expected);
            if (expected > 0) Check(id + " stack visible", stack?.IsVisibleInTree() == true, true);
            Check(id + " original offered model retained", _offered.Contains(holder.CardModel!), true);
        }
        Directory.CreateDirectory(Root);
        NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(Root, phase + ".png"));
        File.WriteAllText(Path.Combine(Root, phase + ".json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        return new(rows.All(row => (bool)row.GetType().GetProperty("pass")!.GetValue(row)!), JsonSerializer.Serialize(rows));
    }
    private static async Task ShowExisting(Player player)
    {
        await CardPileCmd.Add(_offered, PileType.Deck, skipVisuals: true);
        _deckBefore = player.Deck.Cards.Count;
        var prefs = new CardSelectorPrefs(new LocString("events", "BRAIN_LEECH.pages.SHARE_KNOWLEDGE.selectionScreenPrompt"), 1);
        _selection = CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), _offered, player, prefs);
    }
    private static async Task Click(Control control, MouseButton button = MouseButton.Left)
    {
        var point = control.GetGlobalRect().GetCenter();
        var viewport = control.GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await Task.Delay(250);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true }, true);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = false }, true);
        await Task.Delay(450);
    }
    private static async Task Interactions(Player player)
    {
        var rows = new List<object>();
        void Check(string name, object? actual, object? expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        try
        {
            var screen = (NSimpleCardSelectScreen)NOverlayStack.Instance!.Peek()!;
            var holders = Descendants(screen).OfType<NGridCardHolder>().Where(node => node.CardModel != null).ToArray();
            var source = holders.Single(holder => holder.CardModel == _offered[0]);
            if (_mode != "existing")
            {
                var stack = source.CardNode!.GetNode<KnowledgeCardStack>("JunnaKnowledgeStack");
                CardModel? Preview() => (CardModel?)AccessTools.Property(typeof(KnowledgeCardStack), "PreviewModel").GetValue(stack);
                await Click(source.Hitbox, MouseButton.WheelDown);
                Check("wheel shows companion", Preview()?.Id != _offered[0].Id, true);
                Check("wheel keeps original offer", source.CardModel == _offered[0], true);
                Check("wheel grants nothing", player.Deck.Cards.Count, _deckBefore);
                Check("wheel does not finish selection", _selection!.IsCompleted, false);
                Check("hover hides duplicate card previews", Descendants(NGame.Instance!).OfType<NCard>().Count(card => card.IsVisibleInTree() && card.GetPath().ToString().Contains("HoverTips")), 0);
                await Click(source.Hitbox, MouseButton.Right);
                var inspect = NGame.Instance!.GetInspectCardScreen();
                Check("right click opens group details", inspect.Visible, true);
                var preview = inspect.GetNodeOrNull<KnowledgeGroupPreview>("JunnaKnowledgePreview");
                Check("group tiles visible", preview?.GetNode<ScrollContainer>("Cards").IsVisibleInTree() == true, true);
                Check("detail contains exactly the selected group", Descendants(preview!.GetNode("Cards")).OfType<NCard>().Count(), KnowledgePreviewCards.Create(_offered[0]).Count);
                Check("right click leaves selection pending", _selection!.IsCompleted, false);
                Check("details grant nothing", player.Deck.Cards.Count, _deckBefore);
                inspect.GetNode<NButton>("Backstop").ForceClick();
                await Task.Delay(500);
                Check("close returns to original grid", !inspect.Visible && screen.IsVisibleInTree(), true);
                Check("close preserves all offers", holders.All(holder => _offered.Contains(holder.CardModel!)), true);
                Check("close restores stack", stack.GetChildren().OfType<NCard>().Count(), 1);
                await Click(source.Hitbox, MouseButton.WheelDown);
            }
            await Click(source.Hitbox);
            if (_mode == "multi")
            {
                Check("multi-select waits for confirmation", _selection!.IsCompleted, false);
                await Click(holders.Single(holder => holder.CardModel == _offered[3]).Hitbox);
                Check("two selections wait for confirmation", _selection!.IsCompleted, false);
                screen.GetNode<NButton>("%Confirm").ForceClick();
            }
            var chosen = (await _selection!.WaitAsync(TimeSpan.FromSeconds(3))).ToArray();
            Check("native result selects original material", chosen.Contains(_offered[0]), true);
            Check("native result has exact chosen count", chosen.Length, _mode == "multi" ? 2 : 1);
            if (_mode == "multi") Check("multi-select retains second original", chosen.Contains(_offered[3]), true);
            Check("selection alone grants no extra cards", player.Deck.Cards.Count, _deckBefore);
            await Task.Delay(150);
            Check("selection closes native grid", !GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree(), true);
        }
        catch (Exception error) { rows.Add(new { name = "probe exception", pass = false, error = error.ToString() }); }
        File.WriteAllText(Path.Combine(Root, _mode + "-interactions.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        { yield return child; foreach (var item in Descendants(child)) yield return item; }
    }
}
