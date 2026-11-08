using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class BundleKnowledgeProbe : AbstractConsoleCmd
{
    private static NChooseABundleSelectionScreen? _screen;
    private static readonly List<object> Results = [];
    private static string Report => Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory,
        "../../../out/tmp/scroll-box-stacks-20261107/results.json"));
    public override string CmdName => "junna_bundle";
    public override string Args => "show | check | check-preview | check-return";
    public override string Description => "Check knowledge stacks in the native Scroll Boxes bundle UI.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        if (args.FirstOrDefault() == "show")
        {
            CardModel Card(string id) => player.RunState.CreateCard(CardCatalog.Model(id), player);
            Results.Clear();
            _screen = NChooseABundleSelectionScreen.ShowScreen(new IReadOnlyList<CardModel>[]
            {
                new[] { Card("JB01"), Card("JK122"), Card("JA80") },
                new[] { Card("A"), Card("JK32"), Card("JB02") }
            });
            return new(true, "Opened native bundle selection");
        }
        if (_screen == null) return new(false, "Open the bundle fixture first");
        string phase = args.FirstOrDefault() ?? "check";
        if (phase == "check-preview")
        {
            foreach (var holder in Descendants(_screen).OfType<NPreviewCardHolder>().Where(node => node.IsVisibleInTree()))
                CheckCard(phase, holder.CardNode!);
        }
        else
        {
            foreach (var bundle in Descendants(_screen).OfType<NCardBundle>())
            {
                Check(phase + " native bundle retains its three offered cards", bundle.Bundle.Count, 3);
                Check(phase + " native bundle retains its three root card nodes", bundle.CardNodes.Count, 3);
                foreach (var card in bundle.CardNodes) CheckCard(phase, card);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Report)!);
        File.WriteAllText(Report, JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
        return new(true, "Bundle checks saved");
    }
    private static void CheckCard(string phase, NCard card)
    {
        int expected = Math.Max(0, KnowledgePreviewCards.Create(card.Model!).Count - 1);
        var stack = card.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack");
        int actual = stack?.GetChildren().OfType<NCard>().Count() ?? 0;
        Check(phase + " " + card.Model!.Id.Entry + " companion count", actual, expected);
        if (expected > 0) Check(phase + " " + card.Model.Id.Entry + " stack visible", stack?.Visible == true, true);
    }
    private static void Check(string name, object actual, object expected) =>
        Results.Add(new { name, actual, expected, pass = Equals(actual, expected) });
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
}
