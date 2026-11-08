using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;
using SmartFormat;

// Load the game's formatters without starting Godot or loading localization assets.
var manager = RuntimeHelpers.GetUninitializedObject(typeof(LocManager));
typeof(LocManager).GetMethod("LoadLocFormatters", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(manager, null);
var card = new JunnaCardJK82();
typeof(AbstractModel).GetProperty(nameof(AbstractModel.IsMutable))!.SetValue(card, true);
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
var descriptions = new[] { "zhs", "eng" }.ToDictionary(language => language, language =>
{
    using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
        "ShoujoKagekiJunna", "localization", language, "cards.json")));
    return file.RootElement.GetProperty("JUNNA_JK82.description").GetString()!;
});
int checks = 0;
for (int level = 0; level <= 4; level++)
{
    if (level > 0) card.UpgradeInternal();
    int expected = 2 + level;
    Expect($"upgrade {level} energy", card.Number(0) == expected);
    if (level > 0) CheckDescriptions(UpgradeDisplay.UpgradePreview, expected, highlighted: true);
    card.FinalizeUpgradeInternal();
    CheckDescriptions(level > 0 ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal, expected, highlighted: false);
}
Console.WriteLine($"PASS: {checks} checks for learning upgrades 0..4, both languages and upgrade previews.");

void CheckDescriptions(UpgradeDisplay display, int expected, bool highlighted)
{
    foreach (var (language, description) in descriptions)
    {
        var variables = new LocString("cards", "JUNNA_JK82.description");
        card.DynamicVars.AddTo(variables);
        variables.Add(new IfUpgradedVar(display));
        variables.Add("energyPrefix", "junna");
        string rendered = Smart.Default.Format(description, variables.Variables);
        string plain = Regex.Replace(rendered, @"\[img\][^\[]+\[/img\]", "⚡");
        plain = Regex.Replace(plain, @"\[[^\]]+\]", "");
        var gain = Regex.Match(plain, @"获得(?:(\d+)⚡|(⚡+))。");
        int actual = !gain.Success ? -1 : gain.Groups[1].Success
            ? int.Parse(gain.Groups[1].Value) : gain.Groups[2].Length;
        Expect($"{language} {display} level {card.CurrentUpgradeLevel} energy {actual}, expected {expected}", actual == expected);
        Expect($"{language} {display} upgrade highlight", rendered.Contains("[green]") == highlighted);
    }
}

void Expect(string name, bool pass)
{
    if (!pass) throw new InvalidOperationException($"FAIL: {name}");
    checks++;
}
