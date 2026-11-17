using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.addons.mega_text;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaAncientPortraitProbe;

public sealed class KnowledgeTipPresentationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_knowledge_tip_presentation";
    public override string Args => "check";
    public override string Description => "Check merged knowledge tips and native rendered background colors.";
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
            var acquired = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
            var strike = player.RunState.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
            await DynamicKnowledge.Prepare(new ThrowingPlayerChoiceContext(), acquired, [acquired, strike]);
            await DynamicKnowledge.Acquire(acquired, MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck);
            await CardPileCmd.Add(acquired, MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck, skipVisuals: true);
            var native = player.RunState.CreateCard(CardCatalog.Model("JK49"), player);
            foreach (var (name, card) in new (string, CardModel)[]
            {
                ("unacquired reward entrance", CardCatalog.Model("JK126")),
                ("acquired reward entrance", acquired),
                ("unacquired native knowledge", CardCatalog.Model("JK49")),
                ("owned native knowledge", native),
                ("repeated native members", CardCatalog.Model("JK137"))
            })
            {
                var tips = IHoverTip.RemoveDupes(card.HoverTips).OfType<HoverTip>().ToArray();
                bool chain = CardCatalog.DesignId(card) == "JK126";
                string title = chain ? "连锁知识" : "知识";
                string knowledgeId = HoverTipFactory.FromKeyword(chain ? JunnaKeywords.ChainKnowledge : JunnaKeywords.Knowledge).Id;
                int knowledgeIndex = Array.FindIndex(tips, tip => tip.Id == knowledgeId);
                int relationIndex = Array.FindIndex(tips, tip => tip.Title == "关联牌");
                var set = NHoverTipSet.CreateAndShow(owner, tips.Cast<IHoverTip>())!;
                for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var nodes = set.GetNode<Control>("textHoverTipContainer").GetChildren();
                string[] titles = nodes.Select(node => node.GetNode<MegaLabel>("%Title").Text).ToArray();
                int mergedIndex = Array.IndexOf(titles, title);
                Expect(name + " merges associations into one knowledge tip", titles.Count(value => value == title) == 1 &&
                    !titles.Contains("关联牌") && (!chain || !titles.Contains("知识")) &&
                    nodes.Count == tips.Length - (chain ? 2 : 1), string.Join(" / ", titles));
                string description = nodes[mergedIndex].GetNode<MegaRichTextLabel>("%Description").Text;
                Expect(name + " retains the complete rule and association list", description ==
                    tips[knowledgeIndex].Description + "\n关联牌：\n" + tips[relationIndex].Description, description);
                var background = nodes[mergedIndex].GetNode<CanvasItem>("%Bg");
                string color = background.SelfModulate.ToHtml(false);
                Expect(name + " retains its original association background", color == "ffffff", color);
                if (chain)
                {
                    Expect(name + " remains a knowledge card", CardCatalog.IsKnowledge(card) &&
                        card.Keywords.Contains(JunnaKeywords.Knowledge) && card.Keywords.Contains(JunnaKeywords.ChainKnowledge),
                        string.Join(" / ", card.Keywords));
                    Expect(name + " renders the exact chain knowledge rule", tips[knowledgeIndex].Description ==
                        "获得连锁知识时，同时获得同一奖励页面的其他卡牌，将这些牌设置为关联牌。", tips[knowledgeIndex].Description);
                }
                if (card == acquired)
                {
                    Expect("dynamic association retains its captured card", description.Contains(strike.Title), description);
                    string keywordColor = nodes[mergedIndex].GetNode<CanvasItem>("%Bg").SelfModulate.ToHtml(false);
                    Expect("knowledge keyword retains its original background", keywordColor == "ffffff", keywordColor);
                }
                if (CardCatalog.DesignId(card) == "JK49")
                {
                    int backgroundIndex = Array.IndexOf(titles, "知识背景");
                    string backgroundColor = nodes[backgroundIndex].GetNode<CanvasItem>("%Bg").SelfModulate.ToHtml(false);
                    Expect(name + " renders a purple knowledge background", backgroundColor == "c18cdd", backgroundColor);
                }
                NHoverTipSet.Remove(owner);
            }
            var original = player.RunState.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
            Expect("captured strike retains its original tips and keywords",
                strike.HoverTips.Select(tip => tip.Id).SequenceEqual(original.HoverTips.Select(tip => tip.Id)) &&
                strike.Keywords.SetEquals(original.Keywords), string.Join(" / ", strike.HoverTips.Select(tip => tip.Id)));
            var keyword = (HoverTip)HoverTipFactory.FromKeyword(JunnaKeywords.Knowledge);
            var keywordSet = NHoverTipSet.CreateAndShow(owner, new IHoverTip[] { keyword })!;
            var keywordNodes = keywordSet.GetNode<Control>("textHoverTipContainer").GetChildren();
            Expect("standalone knowledge keyword keeps its rule", keywordNodes.Count == 1 &&
                keywordNodes[0].GetNode<MegaRichTextLabel>("%Description").Text == keyword.Description, keyword.Description);
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
