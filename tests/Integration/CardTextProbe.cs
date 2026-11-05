using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class CardTextProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_text_audit";
    public override string Args => "[workbench-snapshot]";
    public override string Description => "Compare every active card face with a read-only workbench snapshot.";
    public override bool IsNetworked => false;

    private static string Normalize(string text)
    {
        text = Regex.Replace(text, @"\[img[^\]]*\][^\[]*energy_icon\.png\[/img\]", "@energy@");
        text = Regex.Replace(text, @"\[[^\]]*\]", "");
        text = Regex.Replace(text, @"(?<=\d)(?:@energy@)+", "");
        text = Regex.Replace(text, @"(?:@energy@)+", match => (match.Length / "@energy@".Length).ToString());
        text = text.Replace("点能量", "");
        text = Regex.Replace(text, @"(?:保留|消耗)。", "");
        return Regex.Replace(text, @"\s+", "");
    }

    public override CmdResult Process(Player? player, string[] args)
    {
        string directory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../..", "out/tmp/card-text-audit"));
        string path = args.FirstOrDefault() ?? Path.Combine(directory, "workbench-r2591.json");
        using var snapshot = JsonDocument.Parse(File.ReadAllText(path));
        var source = snapshot.RootElement.GetProperty("data").GetProperty("cards").EnumerateArray()
            .Where(card => !card.TryGetProperty("trashedAt", out _) && !card.TryGetProperty("reservedAt", out _)).ToArray();
        var rows = new List<object>();
        void Check(string id, string field, object actual, object expected) => rows.Add(new { id, field, actual, expected, pass = Equals(actual, expected) });
        foreach (var item in source)
        {
            string id = item.GetProperty("id").GetString()!;
            var definition = CardCatalog.Get(id);
            Check(id, "name", definition.Name, item.GetProperty("name").GetString()!);
            foreach (bool upgraded in new[] { false, true })
            {
                string face = id + (upgraded ? "+" : "");
                var card = (CardModel)CardCatalog.Model(id).MutableClone();
                if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
                if (id == "JA81B") ((CatalogCard)card).SetGeneratedDamage(7);
                string text = item.GetProperty(upgraded ? "upgradedText" : "text").GetString()!;
                string expected = id == "JA81B" ? text.Replace("X", "7") : text;
                Check(face, "description", Normalize(card.GetDescriptionForPile(PileType.None)), Normalize(expected));
                string cost = item.GetProperty(upgraded ? "upgradedCost" : "cost").ToString();
                Check(face, "cost", card.EnergyCost.CostsX ? "X" : card.EnergyCost.GetWithModifiers(CostModifiers.None).ToString(), cost);
                Check(face, "exhaust", card.Keywords.Contains(CardKeyword.Exhaust), text.Contains("消耗。"));
                Check(face, "retain", card.Keywords.Contains(CardKeyword.Retain), text.Contains("保留。"));
                if (item.GetProperty("type").GetString() == "箭矢")
                    Check(face, "turns", card.DynamicVars["Turns"].IntValue, item.GetProperty(upgraded ? "upgradedTurns" : "turns").GetInt32());
            }
        }
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "rendered-results.json"), JsonSerializer.Serialize(new
        {
            revision = snapshot.RootElement.GetProperty("revision").GetInt32(), cards = source.Length, rows
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        int failures = rows.Count(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!);
        return new(failures == 0, $"{source.Length} cards, {rows.Count} checks, {failures} differences");
    }
}
