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
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

/// <summary>Read-only native knowledge gallery in an isolated development run.</summary>
public sealed class NativeKnowledgeShowcaseCommand : AbstractConsoleCmd
{
    private sealed record Group(string Id, string[] CardIds, string Title, string Reason);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static readonly Group[] Groups = LoadGroups();
    private static CardModel[] _offers = [];
    private static Task<IEnumerable<CardModel>>? _selection;
    private static readonly List<object> TourRows = [];
    private static string Output => System.Environment.GetEnvironmentVariable("JUNNA_NATIVE_KNOWLEDGE_OUTPUT")
        ?? throw new InvalidOperationException("Set JUNNA_NATIVE_KNOWLEDGE_OUTPUT for this isolated instance.");

    public override string CmdName => "junna_native_knowledge";
    public override string Args => "prepare | tour | status";
    public override string Description => "Inspect twenty native-card knowledge groups without obtaining them.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        return args.FirstOrDefault() switch
        {
            "prepare" => Prepare(player),
            "tour" => new(Tour(), true, "UI-only tour starts in 5 seconds"),
            "status" => new(true, JsonSerializer.Serialize(new { groups = Groups.Length,
                prepared = _offers.Length, selectionPending = _selection is { IsCompleted: false }, completed = TourRows.Count }, Json)),
            _ => new(false, Args)
        };
    }

    private static Group[] LoadGroups()
    {
        string resource = System.Environment.GetEnvironmentVariable("JUNNA_NATIVE_KNOWLEDGE_BATCH") == "round2"
            ? "JunnaTests.NativeKnowledgeShowcase.Round2" : "JunnaTests.NativeKnowledgeShowcase";
        using var stream = typeof(NativeKnowledgeShowcaseCommand).Assembly.GetManifestResourceStream(resource)!;
        return JsonSerializer.Deserialize<Group[]>(stream, Json)!;
    }

    private static CmdResult Prepare(Player player)
    {
        if (player.PlayerCombatState == null) return new(false, "Open a combat room before the gallery.");
        if (Groups.Length != 20 || Groups.Select(group => group.Id).Distinct().Count() != 20)
            throw new InvalidOperationException("Expected twenty distinct choices.");
        if (Groups.SelectMany(group => group.CardIds.Distinct()).Distinct().Count()
            != Groups.Sum(group => group.CardIds.Distinct().Count()))
            throw new InvalidOperationException("Different choices must not share a card ID.");

        var models = ModelDb.AllCards.ToDictionary(card => card.Id.Entry);
        var relations = (List<KnowledgeRelation>)AccessTools.Method(typeof(DynamicKnowledge), "Groups").Invoke(null, [player])!;
        foreach (var group in Groups)
        {
            var counts = group.CardIds.GroupBy(id => models[id].Id.ToString()).ToDictionary(cards => cards.Key, cards => cards.Count());
            foreach (string entrance in counts.Keys)
            {
                relations.RemoveAll(relation => relation.EntranceId == entrance);
                relations.Add(new KnowledgeRelation(entrance, new Dictionary<string, int>(counts)));
            }
        }
        AccessTools.Method(typeof(DynamicKnowledge), "Normalize").Invoke(null, [player]);
        _offers = Groups.Select(group => player.RunState.CreateCard(models[group.CardIds[0]], player)).ToArray();
        var rows = Groups.Select((group, index) =>
        {
            var cards = KnowledgePreviewCards.Create(_offers[index]);
            bool pass = cards.Select(card => card.Id.Entry).Order().SequenceEqual(group.CardIds.Order())
                && cards.All(CardCatalog.IsKnowledge);
            return new { group.Id, group.Title, expected = group.CardIds,
                actual = cards.Select(card => card.Id.Entry).ToArray(), pass };
        }).ToArray();
        if (rows.Any(row => !row.pass)) throw new InvalidOperationException("Knowledge preview definitions differ from the fixture.");

        var prefs = new CardSelectorPrefs(new LocString("events", "BRAIN_LEECH.pages.SHARE_KNOWLEDGE.selectionScreenPrompt"), 1)
            { Cancelable = true };
        _selection = CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), _offers, player, prefs);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "prepared.json"), JsonSerializer.Serialize(rows, Json));
        return new(true, JsonSerializer.Serialize(new { groups = rows.Length,
            cards = Groups.Sum(group => group.CardIds.Length), checksPassed = rows.All(row => row.pass) }, Json));
    }

    private static async Task Tour()
    {
        Directory.CreateDirectory(Output);
        TourRows.Clear();
        if (File.Exists(Path.Combine(Output, "error.json"))) File.Delete(Path.Combine(Output, "error.json"));
        int deckCount = _offers[0].Owner.Deck.Cards.Count;
        try
        {
            await Task.Delay(5000);
            var selection = NOverlayStack.Instance!.Peek() as NSimpleCardSelectScreen
                ?? throw new InvalidOperationException("Open the native selection gallery first.");
            Capture("00-overview.png");
            for (int index = 0; index < Groups.Length; index++)
            {
                var group = Groups[index];
                var holder = await FindVisibleOffer(selection, _offers[index]);
                await Click(holder.Hitbox, MouseButton.Right);
                var inspect = NGame.Instance!.GetInspectCardScreen();
                if (!inspect.Visible) throw new InvalidOperationException(group.Id + ": native inspect screen did not open.");
                await Move(inspect, new Vector2(36, 36));
                await Task.Delay(1100);
                var preview = inspect.GetNode<KnowledgeGroupPreview>("JunnaKnowledgePreview");
                var grid = preview.GetNode<ScrollContainer>("Cards");
                var cards = Descendants(grid).OfType<NCard>().Where(card => card.IsVisibleInTree()).ToArray();
                bool pass = grid.IsVisibleInTree()
                    && cards.Select(card => card.Model!.Id.Entry).Order().SequenceEqual(group.CardIds.Order())
                    && cards.All(card => CardCatalog.IsKnowledge(card.Model!))
                    && _offers[0].Owner.Deck.Cards.Count == deckCount
                    && _selection is { IsCompleted: false };
                Capture(group.Id + "-base.png");
                TourRows.Add(new { group.Id, group.Title, group.Reason, expected = group.CardIds,
                    actual = cards.Select(card => card.Model!.Id.Entry).ToArray(), pass,
                    screen = "NInspectCardScreen/JunnaKnowledgePreview", image = group.Id + "-base.png" });
                WriteReport(false);
                if (!pass) throw new InvalidOperationException(group.Id + ": rendered cards or read-only state differed.");
                await Task.Delay(4200);
                await ClickAt(inspect, new Vector2(36, 36), MouseButton.Left);
                await Task.Delay(500);
                if (inspect.Visible) throw new InvalidOperationException(group.Id + ": native Backstop did not close inspection.");
            }
            WriteReport(true);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Output, "error.json"), JsonSerializer.Serialize(new { error = error.ToString() }, Json));
            WriteReport(false);
            throw;
        }
    }

    private static async Task<NGridCardHolder> FindVisibleOffer(Control selection, CardModel model)
    {
        var viewportSize = selection.GetViewport().GetVisibleRect().Size;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var holder = Descendants(selection).OfType<NGridCardHolder>().FirstOrDefault(node => node.CardModel == model);
            float centerY = holder?.Hitbox.GetGlobalRect().GetCenter().Y ?? viewportSize.Y;
            if (holder != null && centerY > 110 && centerY < viewportSize.Y - 110) return holder;
            await ClickAt(selection, new Vector2(viewportSize.X - 70, viewportSize.Y * .48f),
                centerY < 110 ? MouseButton.WheelUp : MouseButton.WheelDown);
            await Task.Delay(250);
        }
        throw new InvalidOperationException(model.Id + ": native scrolling did not reveal the offer.");
    }
    private static void WriteReport(bool completed) => File.WriteAllText(Path.Combine(Output, "report.json"),
        JsonSerializer.Serialize(new { completed, groups = Groups.Length, rows = TourRows }, Json));

    private static void Capture(string name) => NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(Output, name));

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static Task Click(Control control, MouseButton button) => ClickAt(control, control.GetGlobalRect().GetCenter(), button);

    private static async Task Move(Control control, Vector2 point)
    {
        control.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await Task.Delay(100);
    }

    private static async Task ClickAt(Control control, Vector2 point, MouseButton button)
    {
        await Move(control, point);
        var viewport = control.GetViewport();
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true }, true);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = false }, true);
        await Task.Delay(400);
    }
}
