using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class KnowledgeUiProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_knowledge_ui";
    public override string Args => "check | reward | discover | library | inspect <id> | upgrade | focus <index> | snapshot | close";
    public override string Description => "Knowledge group UI checks in the isolated test save.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        var inspect = NGame.Instance!.GetInspectCardScreen();
        string mode = args.FirstOrDefault() ?? "snapshot";
        CardModel Card(string id) => (CardModel)CardCatalog.Model(id).MutableClone();
        switch (mode)
        {
            case "check": return new(Check(player!), true, "Knowledge checks completed");
            case "collection-check": return new(CheckCollection(), true, "Collection checks completed");
            case "layout-check": return new(CheckLayouts(), true, "Layout checks completed");
            case "hover-check": return new(CheckHoverUi(), true, "Hover checks completed");
            case "stack-wheel-check": return new(CheckStackWheel(), true, "Stack wheel checks started");
            case "wheel-demo": return new(RecordWheelDemo(player!), true, "Knowledge recording started");
            case "acquisition-check": return new(CheckAcquisition(player!, args.Contains("slow")), true, "Acquisition checks completed");
            case "layout": return new(Click(inspect.GetNode<Control>("JunnaKnowledgePreview/LayoutToggle")), true, "Changed knowledge layout");
            case "many":
                var group = KnowledgeCombat.Group(Card("A"))!;
                int count = group.GrantedCardCounts["B"];
                try
                {
                    group.GrantedCardCounts["B"] = 9;
                    inspect.Open([Card("A")], 0);
                }
                finally { group.GrantedCardCounts["B"] = count; }
                break;
            case "reward":
                var reward = NCardRewardSelectionScreen.ShowScreen(new[] { "A", "JK32", "JB01" }.Select(id => new CardCreationResult(Card(id))).ToArray(), [])!;
                _ = reward.OptionSelected();
                break;
            case "discover":
                var discover = NChooseACardSelectionScreen.ShowScreen(new[] { "A", "JK32", "JK48" }.Select(Card).ToArray(), true)!;
                _ = discover.CardsSelected();
                break;
            case "library":
                foreach (var definition in CardCatalog.Cards) SaveManager.Instance.MarkCardAsSeen(CardCatalog.Model(definition.Id));
                NRun.Instance!.GlobalUi.SubmenuStack.ShowScreen(CapstoneSubmenuType.Compendium);
                var library = NRun.Instance!.GlobalUi.SubmenuStack.Stack.PushSubmenuType<NCardLibrary>();
                break;
            case "filter":
                Descendants(NGame.Instance!).OfType<NCardLibraryGrid>().Single(grid => grid.IsVisibleInTree())
                    .FilterCards(card => CardCatalog.DesignId(card) is "A" or "JK32" or "JK48" or "JB01");
                break;
            case "close-library":
                while (NRun.Instance!.GlobalUi.SubmenuStack.Stack.Peek() != null) NRun.Instance.GlobalUi.SubmenuStack.Stack.Pop();
                break;
            case "inspect": inspect.Open([Card(args[1])], 0); break;
            case "upgrade":
                var tickbox = inspect.GetNode<NTickbox>("%Upgrade");
                tickbox.IsTicked = !tickbox.IsTicked;
                AccessTools.Method(typeof(NInspectCardScreen), "ToggleShowUpgrade").Invoke(inspect, [tickbox]);
                break;
            case "focus":
                AccessTools.Method(typeof(NInspectCardScreen), "SetCard").Invoke(inspect, [int.Parse(args[1])]);
                break;
            case "inspect-reward":
                var inspectionHolder = Descendants((Node)NOverlayStack.Instance!.Peek()!).OfType<NGridCardHolder>()
                    .First(node => CardCatalog.DesignId(node.CardModel!) == "A");
                return new(Click(inspectionHolder.Hitbox, MouseButton.Right), true, "Inspected reward card");
            case "click-card":
                var grid = Descendants(NGame.Instance!).OfType<NCardLibraryGrid>().Single(node => node.IsVisibleInTree());
                var holder = Descendants(grid).OfType<NGridCardHolder>().First(node => node.IsVisibleInTree() && node.CardModel is JunnaCard &&
                    (args.Length == 1 || CardCatalog.DesignId(node.CardModel) == args[1]));
                return new(Click(holder.Hitbox), true, "Clicked library card");
            case "click-next": return new(Click(inspect.GetNode<Control>("RightArrow")), true, "Clicked native next card");
            case "click-previous": return new(Click(inspect.GetNode<Control>("LeftArrow")), true, "Clicked native previous card");
            case "wheel-up": Wheel(inspect, MouseButton.WheelUp); break;
            case "wheel-down": Wheel(inspect, MouseButton.WheelDown); break;
            case "close": inspect.Close(); break;
            case "pop": NOverlayStack.Instance!.Remove(NOverlayStack.Instance.Peek()!); break;
            case "snapshot": return new(true, JsonSerializer.Serialize(Snapshot()));
            case "inspect-info": return new(true, JsonSerializer.Serialize(new {
                cards = ((List<CardModel>)AccessTools.Field(typeof(NInspectCardScreen), "_cards").GetValue(inspect)!).Count,
                front = CardCatalog.DesignId(inspect.GetNode<NCard>("Card").Model!),
                copy = inspect.GetNodeOrNull<Label>("Card/JunnaInspectStack/CopyIndex")?.Text,
                arrows = new[] { "LeftArrow", "RightArrow" }.Select(name => {
                    var arrow = inspect.GetNode<Control>(name);
                    return new { name, arrow.Visible, position = arrow.Position.ToString(), size = arrow.Size.ToString(), alpha = arrow.Modulate.A };
                }).ToArray()
            }));
            default: return new(false, "Unknown mode");
        }
        return new(true, mode);
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static object Snapshot() => Descendants(NGame.Instance!).OfType<NCard>()
        .Where(card => card.IsVisibleInTree() && card.Model is JunnaCard)
        .Select(card => new { path = card.GetPath().ToString(), id = CardCatalog.DesignId(card.Model!),
            upgraded = card.Model!.IsUpgraded, position = card.GlobalPosition.ToString(), scale = card.GetGlobalTransform().Scale.ToString() }).ToArray();

    private async Task CheckHoverUi()
    {
        var rows = new List<object>();
        void CheckValue(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../..", "out/tmp/knowledge-ui-hover-fixes"));
        System.IO.Directory.CreateDirectory(directory);
        Task Settle() => Task.Delay(650);
        async Task Hover(Control control)
        {
            Vector2 point = control.GetGlobalRect().GetCenter();
            control.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            await Settle();
        }
        void Capture(string name) => NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        foreach (string id in new[] { "A", "B", "JK32", "JK32C", "JK43", "JK43B" })
        {
            var model = (CardModel)CardCatalog.Model(id).MutableClone();
            var tips = model.HoverTips.OfType<HoverTip>().ToArray();
            CheckValue(id + " registered knowledge keyword", model.Keywords.Contains(JunnaKeywords.Knowledge), true);
            CheckValue(id + " knowledge rule tooltip", tips.Any(tip => tip.Title == "知识"), true);
            var members = tips.SingleOrDefault(tip => tip.Title == "关联牌");
            var group = KnowledgeCombat.Group(model)!;
            var names = group.GrantedCardIds.Prepend(group.MainCardId).Select(member => CardCatalog.Model(member).Title).ToArray();
            CheckValue(id + " group list includes every member", names.All(name => members.Description?.Contains(name) == true), true);
            CheckValue(id + " group list starts with main card", members.Description?.StartsWith("[gold]" + names[0]) == true, true);
            CheckValue(id + " literary background only when supplied", tips.Any(tip => tip.Title == "知识背景"), id.StartsWith("JK43"));
        }
        var inspect = NGame.Instance!.GetInspectCardScreen();
        inspect.Open([(CardModel)CardCatalog.Model("A").MutableClone()], 0);
        await Settle();
        var preview = inspect.GetNode<KnowledgeGroupPreview>("JunnaKnowledgePreview");
        var toggle = preview.GetNode<Control>("LayoutToggle");
        CheckValue("layout toggle uses native game button", toggle is NButton, true);
        Capture("stack");
        await Click(toggle);
        await Settle();
        var scroll = preview.GetNode<ScrollContainer>("Cards");
        var grid = Descendants(scroll).OfType<GridContainer>().Single();
        var slot = grid.GetChildren().OfType<Control>().First();
        var card = Descendants(slot).OfType<NCard>().First();
        await Hover(toggle);
        float normal = card.GetGlobalTransform().Scale.X;
        await Hover(slot);
        float focused = card.GetGlobalTransform().Scale.X;
        CheckValue("spread card hover enlarges like collection", focused / normal > 1.2f, true);
        CheckValue("spread tooltips stay outside enlarged card", Descendants(NGame.Instance!).OfType<Control>()
            .Where(node => node.IsVisibleInTree() && node.SceneFilePath == "res://scenes/ui/hover_tip.tscn")
            .All(tip => !tip.GetGlobalRect().Intersects(card.Body.GetGlobalRect())), true);
        Capture("spread-hover");
        await Hover(toggle);
        CheckValue("spread card exit restores scale", Math.Abs(card.GetGlobalTransform().Scale.X - normal) < .01f, true);
        CheckValue("spread exit removes owned hover tips", !Descendants(NGame.Instance!).Any(node => node is MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet && node.IsInsideTree() && ((Control)node).IsVisibleInTree()), true);
        inspect.Close();
        await Settle();
        Process(null, ["library"]);
        await Settle();
        Process(null, ["filter"]);
        await Settle();
        var libraryGrid = Descendants(NGame.Instance!).OfType<NCardLibraryGrid>().Single(node => node.IsVisibleInTree());
        var holder = Descendants(libraryGrid).OfType<NGridCardHolder>().First(node => node.IsVisibleInTree() && CardCatalog.DesignId(node.CardModel!) == "A");
        await Hover(holder.Hitbox);
        CheckValue("collection hover has no companion card previews", Descendants(NGame.Instance!).OfType<NCard>().Count(node => node.IsVisibleInTree() && node.GetPath().ToString().Contains("HoverTips")), 0);
        Capture("collection-hover");
        Process(null, ["close-library"]);
        await Settle();
        foreach (string id in new[] { "JK05", "JK27" })
        {
            var model = CardCatalog.Model(id);
            CheckValue(id + " references knowledge without acquiring keyword", model.Keywords.Contains(JunnaKeywords.Knowledge), false);
            CheckValue(id + " explains referenced knowledge keyword", model.HoverTips.OfType<HoverTip>().Any(tip => tip.Title == "知识"), true);
        }
        inspect.Open([(CardModel)CardCatalog.Model("JK43B").MutableClone()], 0);
        await Settle();
        Capture("background");
        inspect.Close();
        await Settle();
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "hover-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Knowledge hover check failed");
    }

    private static async Task Click(Control control, MouseButton button = MouseButton.Left)
    {
        var viewport = control.GetViewport();
        Vector2 point = control.GetGlobalRect().GetCenter();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await control.ToSignal(control.GetTree(), SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true }, true);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = false }, true);
    }

    private static Task ClosePreview(NInspectCardScreen inspect)
    {
        inspect.GetNode<NButton>("Backstop").ForceClick();
        return Task.CompletedTask;
    }
    private async Task CheckAcquisition(Player player, bool slow = false)
    {
        var rows = new List<object>();
        void CheckValue(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../..", "out/tmp/knowledge-ui-hover-fixes"));
        System.IO.Directory.CreateDirectory(directory);
        Task Settle() => Task.Delay(slow ? 1700 : 650);
        var inspect = NGame.Instance!.GetInspectCardScreen();
        foreach (bool discover in new[] { false, true })
        {
            string name = discover ? "discover" : "reward";
            var cards = new[] { "A", "JK32", "JB01" }.Select(id => player.RunState.CreateCard(CardCatalog.Model(id), player)).ToArray();
            Control screen;
            Task selection;
            if (discover)
            {
                var choice = NChooseACardSelectionScreen.ShowScreen(cards, true)!;
                screen = choice;
                selection = choice.CardsSelected();
            }
            else
            {
                var reward = NCardRewardSelectionScreen.ShowScreen(cards.Select(card => new CardCreationResult(card)).ToArray(), [])!;
                screen = reward;
                selection = reward.OptionSelected();
            }
            await Settle();
            var holder = Descendants(screen).OfType<NGridCardHolder>().First(node => CardCatalog.DesignId(node.CardModel!) == "A");
            Vector2 point = holder.Hitbox.GetGlobalRect().GetCenter();
            screen.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            await Settle();
            CheckValue(name + " hover hides companion card previews", Descendants(NGame.Instance!).OfType<NCard>().Count(node => node.IsVisibleInTree() && node.GetPath().ToString().Contains("HoverTips")), 0);
            int before = player.Deck.Cards.Count;
            await Click(holder.Hitbox, MouseButton.Right);
            await Settle();
            var preview = inspect.GetNodeOrNull<KnowledgeGroupPreview>("JunnaKnowledgePreview");
            var confirm = preview?.GetNodeOrNull<NButton>("Confirm");
            var cancel = preview?.GetNodeOrNull<NButton>("Cancel");
            CheckValue(name + " right click keeps the original choice pending", !selection.IsCompleted, true);
            CheckValue(name + " right click opens tiled group", inspect.Visible && preview?.GetNode<ScrollContainer>("Cards").IsVisibleInTree() == true, true);
            CheckValue(name + " has no confirm or cancel buttons", confirm == null && cancel == null, true);
            CheckValue(name + " preview grants no cards", player.Deck.Cards.Count, before);
            if (preview != null && confirm == null && cancel == null)
            {
                CheckValue(name + " tiles include repeated copies", Descendants(preview!.GetNode("Cards")).OfType<NCard>().Count(), 3);
                await ClosePreview(inspect);
                await Settle();
                CheckValue(name + " native close returns to original choices", !inspect.Visible && screen.IsVisibleInTree() && !selection.IsCompleted, true);
                CheckValue(name + " native close grants no cards", player.Deck.Cards.Count, before);
                var stack = holder.CardNode!.GetNode<KnowledgeCardStack>("JunnaKnowledgeStack");
                CardModel? Front() => (CardModel?)AccessTools.Property(typeof(KnowledgeCardStack), "PreviewModel").GetValue(stack);
                async Task Scroll(MouseButton button)
                {
                    await Click(holder.Hitbox, button);
                    await Settle();
                }
                await Scroll(MouseButton.WheelDown);
                CheckValue(name + " wheel shows next physical card", CardCatalog.DesignId(Front()!)!, "B");
                CheckValue(name + " wheel retains original offer", holder.CardModel == cards[0], true);
                CheckValue(name + " wheel does not open inspection", inspect.Visible, false);
                await Scroll(MouseButton.WheelDown);
                CheckValue(name + " wheel reaches duplicate copy", (int)AccessTools.Field(typeof(KnowledgeCardStack), "_previewIndex").GetValue(stack)!, 2);
                await Scroll(MouseButton.WheelDown);
                CheckValue(name + " wheel wraps to original", CardCatalog.DesignId(Front()!)!, "A");
                await Scroll(MouseButton.WheelUp);
                CheckValue(name + " wheel up cycles backward", CardCatalog.DesignId(Front()!)!, "B");
                await Click(holder.Hitbox);
                await selection.WaitAsync(TimeSpan.FromSeconds(2));
                await Settle();
                CheckValue(name + " left click acquires directly", inspect.Visible, false);
                CheckValue(name + " left click selects original offered model", discover ? ((Task<IEnumerable<CardModel>>)selection).Result.Single() == cards[0] : ((Task<int?>)selection).Result == 0, true);

                await CardPileCmd.Add(cards[0], PileType.Deck);
                CheckValue(name + " direct grant includes entire group once", player.Deck.Cards.Count - before, 3);
            }
            inspect.Close();
            if (GodotObject.IsInstanceValid(screen) && screen.IsInsideTree() && NOverlayStack.Instance!.Peek() == screen) NOverlayStack.Instance.Remove((IOverlayScreen)screen);
            await Settle();
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "acquisition-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Knowledge acquisition check failed");
    }

    private async Task CheckStackWheel()
    {
        var rows = new List<object>();
        void Verify(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        Process(null, ["library"]);
        await Task.Delay(600);
        Process(null, ["filter"]);
        await Task.Delay(600);
        var grid = Descendants(NGame.Instance!).OfType<NCardLibraryGrid>().Single(g => g.IsVisibleInTree());
        var holder = Descendants(grid).OfType<NGridCardHolder>().First(h => h.IsVisibleInTree() && CardCatalog.DesignId(h.CardModel!) == "A");
        var model = holder.CardModel!;
        var stack = holder.CardNode!.GetNode<KnowledgeCardStack>("JunnaKnowledgeStack");
        int Index() => (int)AccessTools.Field(typeof(KnowledgeCardStack), "_previewIndex").GetValue(stack)!;
        await Click(holder.Hitbox, MouseButton.WheelDown);
        await Task.Delay(250);
        Verify("library wheel reaches companion", Index(), 1);
        Verify("library keeps original model", holder.CardModel == model, true);
        var preview = Descendants(stack).OfType<NCard>().Single(c => c.ZIndex == 1);
        Verify("front companion is drawn at full size", preview.Scale, Vector2.One);
        Verify("original visual is hidden while companion is front", holder.CardNode.Body.Visible, false);
        grid.GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.Zero, GlobalPosition = Vector2.Zero }, true);
        await Task.Delay(250);
        Verify("leaving restores original front", Index(), 0);
        Verify("leaving restores original body", holder.CardNode.Body.Visible, true);
        await Click(holder.Hitbox, MouseButton.WheelUp);
        await Task.Delay(200);
        Verify("library wheel up wraps to last copy", Index(), 2);
        holder.ReassignToCard((CardModel)CardCatalog.Model("JB01").MutableClone(), PileType.None, null, ModelVisibility.Visible);
        Verify("reassign removes temporary source preview", AccessTools.Field(typeof(KnowledgeCardStack), "_sourcePreview").GetValue(stack) == null, true);
        Verify("reassigned card body remains visible", holder.CardNode.Body.Visible, true);
        Verify("reassigned ordinary card hides stack", stack.Visible, false);
        Process(null, ["close-library"]);
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../..", "out/tmp/card-bonus-border"));
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "stack-wheel-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Stack wheel check failed");
    }

    private async Task RecordWheelDemo(Player player)
    {
        var cards = new[] { "A", "JK32", "JB01" }.Select(id => player.RunState.CreateCard(CardCatalog.Model(id), player)).ToArray();
        var reward = NCardRewardSelectionScreen.ShowScreen(cards.Select(c => new CardCreationResult(c)).ToArray(), [])!;
        var selection = reward.OptionSelected();
        await Task.Delay(900);
        var holder = Descendants(reward).OfType<NGridCardHolder>().First(h => h.CardModel == cards[0]);
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../..", "out/tmp/card-bonus-border/video"));
        using var client = new System.Net.Http.HttpClient();
        async Task Post(string path, object body)
        {
            using var response = await client.PostAsync("http://127.0.0.1:15737/api/v1/" + path,
                new System.Net.Http.StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }
        await Post("recording/start", new { output_dir = directory, fps = 30, width = 1280, height = 720, audio = true, max_duration_seconds = 40 });
        try
        {
            Vector2 point = holder.Hitbox.GetGlobalRect().GetCenter();
            reward.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            await Task.Delay(1700);
            await Click(holder.Hitbox, MouseButton.WheelDown); await Task.Delay(1700);
            await Click(holder.Hitbox, MouseButton.WheelDown); await Task.Delay(1700);
            await Click(holder.Hitbox, MouseButton.WheelUp); await Task.Delay(1700);
            await Click(holder.Hitbox, MouseButton.Right); await Task.Delay(2200);
            var inspect = NGame.Instance!.GetInspectCardScreen();
            await ClosePreview(inspect); await Task.Delay(1300);
            await Click(holder.Hitbox); await selection.WaitAsync(TimeSpan.FromSeconds(3)); await Task.Delay(1400);
        }
        finally { await Post("recording/stop", new { }); }
    }
    private static void Wheel(NInspectCardScreen inspect, MouseButton button)
    {
        Vector2 point = inspect.GetNode<NCard>("Card").GlobalPosition;
        inspect.GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true }, true);
    }

    private async Task CheckCollection()
    {
        var rows = new List<object>();
        void CheckValue(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var inspect = NGame.Instance!.GetInspectCardScreen();
        Task Settle() => Task.Delay(450);
        inspect.Open([(CardModel)CardCatalog.Model("JB01").MutableClone()], 0);
        await Settle();
        var front = inspect.GetNode<NCard>("Card");
        Vector2 ordinaryPosition = front.Position;
        Vector2 ordinaryScale = front.Scale;
        Vector2 leftPosition = inspect.GetNode<Control>("LeftArrow").Position;
        Vector2 rightPosition = inspect.GetNode<Control>("RightArrow").Position;
        inspect.Close();
        await Settle();
        Process(null, ["library"]);
        await Settle();
        Process(null, ["filter"]);
        await Settle();
        var grid = Descendants(NGame.Instance!).OfType<NCardLibraryGrid>().Single(node => node.IsVisibleInTree());
        var holder = Descendants(grid).OfType<NGridCardHolder>().Single(node => node.IsVisibleInTree() && CardCatalog.DesignId(node.CardModel!) == "A");
        var listStack = holder.CardNode!.GetNode<KnowledgeCardStack>("JunnaKnowledgeStack");
        var listCopies = Descendants(listStack).OfType<NCard>().ToArray();
        CheckValue("collection thumbnail has two backing cards", listCopies.Length, 2);
        CheckValue("collection thumbnail exposes both upper edges", listCopies.All(card => card.Position.Y < 0) && listCopies.Select(card => card.Position.Y).Distinct().Count() == 2, true);
        await Click(holder.Hitbox);
        await Settle();
        var stack = front.GetNode<Control>("JunnaInspectStack");
        string CopyIndex() => stack.GetNode<Label>("CopyIndex").Text;
        CheckValue("collection click keeps native card visible", front.Visible, true);
        CheckValue("collection click shows three stacked cards", 1 + Descendants(stack).OfType<NCard>().Count(), 3);
        CheckValue("native card position retained", front.Position, ordinaryPosition);
        CheckValue("native card scale retained", front.Scale, ordinaryScale);
        CheckValue("native left arrow position retained", inspect.GetNode<Control>("LeftArrow").Position, leftPosition);
        CheckValue("native right arrow position retained", inspect.GetNode<Control>("RightArrow").Position, rightPosition);
        CheckValue("initial copy index", CopyIndex(), "1 / 3");
        await Click(inspect.GetNode<Control>("RightArrow"));
        await Settle();
        CheckValue("native button selects first duplicate", CopyIndex(), "2 / 3");
        CheckValue("selected duplicate is native front card", CardCatalog.DesignId(front.Model!)!, "B");
        Wheel(inspect, MouseButton.WheelDown);
        await Settle();
        CheckValue("mouse wheel selects second duplicate", CopyIndex(), "3 / 3");
        CheckValue("last copy hides next button", inspect.GetNode<Control>("RightArrow").Visible, false);
        Wheel(inspect, MouseButton.WheelUp);
        await Settle();
        CheckValue("reverse mouse wheel selects previous copy", CopyIndex(), "2 / 3");
        Process(null, ["upgrade"]);
        await Settle();
        CheckValue("upgrade keeps current copy", CopyIndex(), "2 / 3");
        CheckValue("upgrade includes all visible copies", Descendants(stack).OfType<NCard>().Append(front).All(card => card.Model!.IsUpgraded), true);
        inspect.Close();
        await Settle();
        CheckValue("close releases collection backing cards", Descendants(stack).OfType<NCard>().Count(), 0);
        CheckValue("close hides collection index", stack.Visible, false);
        inspect.Open([(CardModel)CardCatalog.Model("JB01").MutableClone()], 0);
        await Settle();
        CheckValue("ordinary card inspection has no stack", front.Visible && !stack.Visible, true);
        inspect.Close();
        await Settle();
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../..", "out/tmp/collection-stack"));
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Collection stack check failed");
    }

    private async Task CheckLayouts()
    {
        var rows = new List<object>();
        void CheckValue(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        Task Settle() => Task.Delay(450);
        var inspect = NGame.Instance!.GetInspectCardScreen();
        Process(null, ["reward"]);
        await Settle();
        var reward = (NCardRewardSelectionScreen)NOverlayStack.Instance!.Peek()!;
        var holder = Descendants(reward).OfType<NGridCardHolder>().Single(node => CardCatalog.DesignId(node.CardModel!) == "A");
        var backing = Descendants(holder.CardNode!.GetNode<KnowledgeCardStack>("JunnaKnowledgeStack")).OfType<NCard>().ToArray();
        CheckValue("reward thumbnail has three stacked cards", 1 + backing.Length, 3);
        CheckValue("reward thumbnail exposes both backing edges", backing.All(card => card.Position.Y < 0), true);
        await Click(holder.Hitbox, MouseButton.Right);
        await Settle();
        await ClosePreview(inspect);
        await Settle();
        inspect.Open([holder.CardModel!], 0);
        await Settle();
        var preview = inspect.GetNode<KnowledgeGroupPreview>("JunnaKnowledgePreview");
        var toggle = preview.GetNode<Control>("LayoutToggle");
        var front = inspect.GetNode<NCard>("Card");
        var stack = front.GetNode<Control>("JunnaInspectStack");
        var scroll = preview.GetNode<ScrollContainer>("Cards");
        var grid = Descendants(scroll).OfType<GridContainer>().Single();
        CheckValue("reward inspection starts with native stack", front.Visible && !scroll.Visible, true);
        CheckValue("reward inspection exposes layout button", toggle.IsVisibleInTree(), true);
        await Click(inspect.GetNode<Control>("RightArrow"));
        await Settle();
        await Click(toggle);
        await Settle();
        var copies = Descendants(grid).OfType<NCard>().ToArray();
        CheckValue("reward layout button shows all three copies", copies.Length, 3);
        CheckValue("three copies are displayed in one row", grid.Columns, 3);
        CheckValue("spread preserves both duplicate copies", copies.Count(card => CardCatalog.DesignId(card.Model!) == "B"), 2);
        CheckValue("spread hides underlying native card", front.Visible, false);
        var bounds = grid.GetChildren().OfType<Control>().Select(slot => slot.GetGlobalRect()).ToArray();
        CheckValue("spread card bounds do not overlap", !bounds.SelectMany((rect, index) => bounds.Skip(index + 1).Select(other => rect.Intersects(other))).Any(overlap => overlap), true);
        Process(null, ["upgrade"]);
        await Settle();
        CheckValue("upgrade preserves spread layout", scroll.Visible && !front.Visible, true);
        CheckValue("spread upgrades every copy", Descendants(grid).OfType<NCard>().All(card => card.Model!.IsUpgraded), true);
        await Click(toggle);
        await Settle();
        CheckValue("stack toggle restores selected duplicate", stack.GetNode<Label>("CopyIndex").Text, "2 / 3");
        CheckValue("stack toggle restores native navigation", front.Visible && inspect.GetNode<Control>("LeftArrow").Visible && inspect.GetNode<Control>("RightArrow").Visible, true);
        inspect.Close();
        await Settle();
        NOverlayStack.Instance.Remove(reward);
        Process(null, ["many"]);
        await Settle();
        await Click(toggle);
        await Settle();
        CheckValue("long group retains all ten copies", Descendants(grid).OfType<NCard>().Count(), 10);
        CheckValue("long group uses multiple grid rows", grid.GetChildren().OfType<Control>().Select(slot => slot.Position.Y).Distinct().Count() > 1, true);
        scroll.ScrollVertical = 10000;
        await Settle();
        CheckValue("long grid can scroll to its last row", scroll.ScrollVertical > 0, true);
        inspect.Close();
        await Settle();
        CheckValue("close releases grid cards", Descendants(grid).OfType<NCard>().Count(), 0);
        CheckValue("close hides layout toggle", toggle.IsVisibleInTree(), false);
        await CheckCollection();
        Process(null, ["click-card", "A"]);
        await Settle();
        await Click(toggle);
        await Settle();
        CheckValue("collection opens the same spread layout", scroll.Visible && Descendants(grid).OfType<NCard>().Count() == 3, true);
        inspect.Close();
        await Settle();
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../..", "out/tmp/knowledge-layouts"));
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Knowledge layout check failed");
    }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        string directory = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "evidence");
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "results.json"), "[]");
        void CheckValue(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        foreach (var group in CardCatalog.Groups)
        {
            foreach (string sourceId in group.GrantedCardIds.Prepend(group.MainCardId))
            {
                var source = CardCatalog.Model(sourceId);
                var cards = KnowledgePreviewCards.Create(source);
                CheckValue(sourceId + " count", cards.Count, 1 + group.GrantedCardIds.Sum(id => group.GrantedCardCounts.GetValueOrDefault(id, 1)));
                foreach (string id in group.GrantedCardIds.Prepend(group.MainCardId))
                    CheckValue(sourceId + " copies of " + id, cards.Count(c => CardCatalog.DesignId(c) == id), id == group.MainCardId ? 1 : group.GrantedCardCounts.GetValueOrDefault(id, 1));
                CheckValue(sourceId + " independent instances", cards.Distinct().Count(), cards.Count);
            }
        }
        CheckValue("ordinary card has no group", KnowledgePreviewCards.Create(CardCatalog.Model("JB01")).Count, 0);
        var upgraded = (CardModel)CardCatalog.Model("A").MutableClone();
        upgraded.UpgradeInternal();
        CheckValue("acquisition keeps companions basic", KnowledgePreviewCards.Create(upgraded).Count(c => c.IsUpgraded), 1);
        CheckValue("collection upgrade previews every copy", KnowledgePreviewCards.Create(upgraded, true).Count(c => c.IsUpgraded), 3);

        var inspect = NGame.Instance!.GetInspectCardScreen();
        inspect.Open([(CardModel)CardCatalog.Model("A").MutableClone()], 0);
        var preview = inspect.GetNode<KnowledgeGroupPreview>("JunnaKnowledgePreview");
        var nativeCard = inspect.GetNode<NCard>("Card");
        var inspectStack = nativeCard.GetNode<Control>("JunnaInspectStack");
        CheckValue("inspect shows three stacked cards", 1 + Descendants(inspectStack).OfType<NCard>().Count(), 3);
        inspect.GetNode<NButton>("RightArrow").ForceClick();
        inspect.GetNode<NButton>("RightArrow").ForceClick();
        CheckValue("second duplicate can be selected", inspectStack.GetNode<Label>("CopyIndex").Text, "3 / 3");
        inspect.Close();
        CheckValue("closing clears backing cards", Descendants(inspectStack).OfType<NCard>().Count(), 0);
        inspect.Open([(CardModel)CardCatalog.Model("JB01").MutableClone()], 0);
        CheckValue("ordinary inspection restores native card", nativeCard.Visible, true);
        CheckValue("ordinary inspection hides group", preview.Visible, false);
        inspect.Close();
        var actual = player.RunState.CreateCard(CardCatalog.Model("A"), player);
        using (KnowledgeCombat.SingleCard()) await CardPileCmd.Add(actual, PileType.Deck);
        inspect.Open([actual], 0);
        CheckValue("owned deck instance remains single", inspect.GetNode<NCard>("Card").Visible && !preview.Visible, true);
        inspect.Close();
        await CardPileCmd.RemoveFromDeck(actual);

        var reward = NCardRewardSelectionScreen.ShowScreen([new CardCreationResult((CardModel)CardCatalog.Model("A").MutableClone())], [])!;
        _ = reward.OptionSelected();
        var holder = Descendants(reward).OfType<NGridCardHolder>().Single();
        var stack = holder.CardNode!.GetNode<KnowledgeCardStack>("JunnaKnowledgeStack");
        CheckValue("reward has two companion nodes", Descendants(stack).OfType<NCard>().Count(), 2);
        holder.ReassignToCard((CardModel)CardCatalog.Model("JB01").MutableClone(), PileType.None, null, ModelVisibility.Visible);
        CheckValue("recycled holder clears companions", Descendants(stack).OfType<NCard>().Count(), 0);
        CheckValue("recycled holder hides count", stack.Visible, false);
        holder.ReassignToCard((CardModel)CardCatalog.Model("JK48").MutableClone(), PileType.None, null, ModelVisibility.Visible);
        CheckValue("recycled holder rebuilds new group", Descendants(stack).OfType<NCard>().Count(), 1);
        holder.ReassignToCard((CardModel)CardCatalog.Model("A").MutableClone(), PileType.None, null, ModelVisibility.NotSeen);
        CheckValue("unseen card hides group", stack.Visible, false);
        CheckValue("unseen card clears companions", Descendants(stack).OfType<NCard>().Count(), 0);
        NOverlayStack.Instance!.Remove(reward);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Knowledge UI check failed; see evidence/results.json");
    }
}
