using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class DamagePreviewProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_damage_preview";
    public override string Args => "check";
    public override string Description => "Check knowledge and invested damage use the native live preview.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Damage preview checks started");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, decimal actual, decimal expected) => rows.Add(new { name, actual, expected, pass = actual == expected });
        var card = CardCatalog.Create("JK48", player);
        var partner = CardCatalog.Create("B", player);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        await CardPileCmd.AddGeneratedCardToCombat(partner, PileType.Draw, player);
        int count = player.Deck.Cards.Count(CardCatalog.IsKnowledge);
        card.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, card.DynamicVars);
        Expect("knowledge count changes displayed damage", card.DynamicVars.Damage.PreviewValue, 6 + count);
        DamageModifiers.Add(card, 3);
        card.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, card.DynamicVars);
        Expect("investment included in conditional damage", card.DynamicVars.Damage.PreviewValue, 9 + count);
        card.UpgradeInternal(); card.FinalizeUpgradeInternal();
        card.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, card.DynamicVars);
        Expect("upgraded conditional preview", card.DynamicVars.Damage.PreviewValue, 13 + count);
        rows.Add(new { name = "knowledge hover does not add companion card previews", pass = !card.HoverTips.OfType<CardHoverTip>().Any() });
        var strike = CardCatalog.Create("JB01", player);
        await CardPileCmd.AddGeneratedCardToCombat(strike, PileType.Hand, player);
        DamageModifiers.Add(strike, 4);
        strike.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, strike.DynamicVars);
        Expect("ordinary hand preview includes investment", strike.DynamicVars.Damage.PreviewValue, 10);
        strike.DynamicVars.Damage.UpdateCardPreview(strike, CardPreviewMode.Normal, null, false);
        Expect("non-global preview includes investment", strike.DynamicVars.Damage.PreviewValue, 10);
        string report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/knowledge-transform-20261107/damage-preview-results.json"));
        File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
