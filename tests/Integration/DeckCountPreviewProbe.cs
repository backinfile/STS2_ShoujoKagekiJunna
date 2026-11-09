using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class DeckCountPreviewProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_deck_count_preview";
    public override string Args => "check";
    public override string Description => "Check owned deck-count damage previews outside combat.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null || player.Creature.CombatState != null) return new(false, "Requires a run outside combat");
        var rows = new List<object>();
        bool pass = true;
        foreach (bool upgraded in new[] { false, true })
        {
            var card = player.RunState.CreateCard(CardCatalog.Model("JK206"), player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            card.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, card.DynamicVars);
            string text = Regex.Replace(card.GetDescriptionForPile(PileType.None), @"\[[^\]]*\]", "");
            var number = Regex.Match(text, @"[（(](\d+)[）)]");
            int expected = player.Deck.Cards.Count + (upgraded ? 8 : 0);
            bool correct = number.Success && int.Parse(number.Groups[1].Value) == expected
                && card.DynamicVars.Damage.BaseValue == (upgraded ? 8 : 0);
            pass &= correct;
            rows.Add(new { upgraded, text, expected, pass = correct });
        }

        return new(pass, JsonSerializer.Serialize(new { pass, rows }));
    }

}
