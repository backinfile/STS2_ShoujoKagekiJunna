using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using BaseLib.Patches.Content;
using Godot;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.addons.mega_text;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class KeywordProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_keywords";
    public override string Args => "check";
    public override string Description => "Audit published keyword references and visible knowledge tip ordering.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => new(Check(player!), true, "Keyword audit completed");

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static bool HasArrowKeyword(CardModel card) => card.Keywords.Any(keyword =>
        CustomKeywords.KeywordIDs.TryGetValue((int)keyword, out var info) && info.Key == "SHOUJOKAGEKIJUNNA-ARROW");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void CheckValue(string card, string name, object actual, object expected) => rows.Add(new { card, name, actual, expected, pass = Equals(actual, expected) });
        foreach (string key in new[] { "ARROW", "DAMAGE_BONUS" })
            CheckValue("registry", key, CustomKeywords.KeywordIDs.Values.Any(info => info.Key == "SHOUJOKAGEKIJUNNA-" + key), true);
        foreach (var (keyword, expected) in new[]
        {
            (JunnaKeywords.Arrow, "打出后布置，等待数个回合后触发其卡牌效果。"),
            (JunnaKeywords.Knowledge, "获得此牌时，同时获得关联的知识牌。"),
            (JunnaKeywords.DamageBonus, "本场战斗中额外获得的伤害数值。")
        })
            CheckValue("workbench mechanism", keyword.ToString(), ((HoverTip)HoverTipFactory.FromKeyword(keyword)).Description, expected);
        foreach (var definition in CardCatalog.Cards)
        foreach (bool up in new[] { false, true })
        {
            var card = (CardModel)CardCatalog.Model(definition.Id).MutableClone();
            if (up) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            string id = definition.Id + (up ? "+" : "");
            string text = up ? definition.UpgradedText : definition.Text;
            var tips = IHoverTip.RemoveDupes(card.HoverTips).OfType<HoverTip>().ToArray();
            string[] titles = tips.Select(tip => tip.Title ?? "").ToArray();
            void HasTip(string title) => CheckValue(id, title + " appears once", titles.Count(value => value == title), 1);
            CheckValue(id, "arrow keyword matches actual type", HasArrowKeyword(card), CardCatalog.IsArrow(card));
            if (CardCatalog.IsArrow(card) || text.Contains("箭矢")) HasTip("箭矢");
            if (text.Contains("伤害加成")) HasTip("伤害加成");
            if (text.Contains("易伤")) HasTip("易伤");
            if (text.Contains("虚弱")) HasTip("虚弱");
            if (text.Contains("能量") || text.Contains("耗能") || Regex.IsMatch(text, @"\d+费")) HasTip("能量");
            if (card.Keywords.Contains(CardKeyword.Exhaust)) HasTip("消耗");
            if (card.Keywords.Contains(CardKeyword.Retain)) HasTip("保留");
            else if (text.Contains("保留") && text.Contains("手牌")) HasTip("保留");
            if (text.Contains("知识") || CardCatalog.IsKnowledge(card)) HasTip("知识");
            if (KnowledgeCombat.Group(card) != null)
            {
                int knowledge = Array.IndexOf(titles, "知识"), group = Array.IndexOf(titles, "关联牌"), background = Array.IndexOf(titles, "知识背景");
                CheckValue(id, "related cards immediately follow knowledge", group == knowledge + 1 && knowledge >= 0, true);
                if (background >= 0) CheckValue(id, "background follows related cards", background, group + 1);
            }
            CheckValue(id, "all tip text resolves", tips.All(tip => !string.IsNullOrWhiteSpace(tip.Description) && !tip.Description.Contains("ERROR") && !tip.Description.Contains('{')), true);
        }
        foreach (string id in new[] { "JB03", "JA38", "JK32B" })
        {
            var card = player.RunState.CreateCard(CardCatalog.Model(id), player);
            _ = card.Keywords;
            ArrowConversion.Convert(card);
            CheckValue(id, "converted card removes arrow keyword", HasArrowKeyword(card), false);
            CheckValue(id, "converted clone removes arrow keyword", HasArrowKeyword((CardModel)card.MutableClone()), false);
            CheckValue(id, "converted attack has no arrow tip when not referenced", card.HoverTips.OfType<HoverTip>().Any(tip => tip.Title == "箭矢"), false);
        }
        CheckValue("combat", "damage bonus case has combat state", player.PlayerCombatState != null, true);
        if (player.PlayerCombatState != null)
        {
            var invested = CardCatalog.Create("JB01", player);
            DamageModifiers.Add(invested, 2);
            CheckValue("JB01", "invested card explains damage bonus", invested.HoverTips.OfType<HoverTip>().Count(tip => tip.Title == "伤害加成"), 1);
        }
        var inspect = NGame.Instance!.GetInspectCardScreen();
        inspect.Open([(CardModel)CardCatalog.Model("JK43B").MutableClone()], 0);
        await Task.Delay(650);
        var visibleTitles = Descendants(NGame.Instance).OfType<Control>()
            .Where(node => node.IsVisibleInTree() && node.SceneFilePath == "res://scenes/ui/hover_tip.tscn")
            .Select(node => node.GetNode<MegaLabel>("%Title").Text).ToArray();
        CheckValue("visible", "knowledge then related cards then background", string.Join("|", visibleTitles.Take(3)), "知识|关联牌|知识背景");
        inspect.Close();
        await Task.Delay(400);
        string directory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../..", "out/tmp/keyword-audit"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "keyword-results.json"), JsonSerializer.Serialize(rows,
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Keyword audit failed");
    }
}
