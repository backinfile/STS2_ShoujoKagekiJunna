using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using ShoujoKagekiJunna.Models;

namespace JunnaAncientPortraitProbe;

public sealed class KnowledgeTipPresentationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_knowledge_tip_presentation";
    public override string Args => "check";
    public override string Description => "Check association tip order and native rendered background colors.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        return new(Check(player), true, "Knowledge tip presentation checks started");
    }

    private static async Task Check(Player player)
    {
        var checks = new List<object>();
        void Expect(string name, bool pass, string actual) => checks.Add(new { name, pass, actual });
        var tree = (SceneTree)Engine.GetMainLoop();
        var owner = new Control { Position = new Vector2(100, 100) };
        tree.Root.AddChild(owner);
        try
        {
            var acquired = player.Deck.Cards.First(card => CardCatalog.DesignId(card) == "JK126");
            var native = player.RunState.CreateCard(CardCatalog.Model("JK49"), player);
            foreach (var (name, card) in new (string, CardModel)[]
            {
                ("unacquired reward entrance", CardCatalog.Model("JK126")),
                ("acquired reward entrance", acquired),
                ("unacquired native knowledge", CardCatalog.Model("JK49")),
                ("owned native knowledge", native)
            })
            {
                var tips = IHoverTip.RemoveDupes(card.HoverTips).OfType<HoverTip>().ToArray();
                string knowledgeId = HoverTipFactory.FromKeyword(JunnaKeywords.Knowledge).Id;
                int knowledgeIndex = Array.FindIndex(tips, tip => tip.Id == knowledgeId);
                int relationIndex = Array.FindIndex(tips, tip => tip.Title == "关联牌");
                Expect(name + " keeps associations directly after knowledge", knowledgeIndex >= 0 && relationIndex == knowledgeIndex + 1,
                    string.Join(" / ", tips.Select(tip => tip.Title)));
                var set = NHoverTipSet.CreateAndShow(owner, tips.Cast<IHoverTip>())!;
                for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var nodes = set.GetNode<Control>("textHoverTipContainer").GetChildren();
                var background = nodes[relationIndex].GetNode<CanvasItem>("%Bg");
                string color = background.SelfModulate.ToHtml(false);
                Expect(name + " retains its original association background", color == "ffffff", color);
                if (CardCatalog.DesignId(card) == "JK126")
                    Expect(name + " retains its highlighted first line", tips[relationIndex].Description.StartsWith("[b][gold]"),
                        tips[relationIndex].Description);
                if (card == acquired)
                {
                    string keywordColor = nodes[knowledgeIndex].GetNode<CanvasItem>("%Bg").SelfModulate.ToHtml(false);
                    Expect("knowledge keyword retains its original background", keywordColor == "ffffff", keywordColor);
                    var titleColor = nodes[relationIndex].GetNode<Control>("%Title").GetThemeColor("font_color");
                    Expect("association title retains its original color", titleColor == nodes[knowledgeIndex].GetNode<Control>("%Title").GetThemeColor("font_color"), titleColor.ToHtml(false));
                }
                if (CardCatalog.DesignId(card) == "JK49")
                {
                    int backgroundIndex = Array.FindIndex(tips, tip => tip.Id.StartsWith("junna_knowledge_background_"));
                    string backgroundColor = nodes[backgroundIndex].GetNode<CanvasItem>("%Bg").SelfModulate.ToHtml(false);
                    Expect(name + " renders a purple knowledge background", backgroundColor == "c18cdd", backgroundColor);
                }
                NHoverTipSet.Remove(owner);
            }
            var strike = player.Deck.Cards.First(card => card.Id == ModelDb.Card<StrikeIronclad>().Id);
            var original = player.RunState.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
            Expect("captured strike retains its original tips and keywords",
                strike.HoverTips.Select(tip => tip.Id).SequenceEqual(original.HoverTips.Select(tip => tip.Id)) &&
                strike.Keywords.SetEquals(original.Keywords), string.Join(" / ", strike.HoverTips.Select(tip => tip.Id)));
        }
        catch (Exception error) { checks.Add(new { name = "knowledge tip presentation checks", pass = false, actual = error.ToString() }); }
        finally
        {
            NHoverTipSet.Remove(owner);
            owner.QueueFree();
        }
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/knowledge-tips"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(checks,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
