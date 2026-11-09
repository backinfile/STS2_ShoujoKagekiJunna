using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class KnowledgeCountPreviewProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_knowledge_preview";
    public override string Args => "prepare";
    public override string Description => "Check permanent-deck knowledge counts, live block and descriptive count previews.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Knowledge preview checks started");

    private static async Task Prepare(Player player)
    {
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
        var context = new ThrowingPlayerChoiceContext();
        await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
        using var single = KnowledgeCombat.SingleCard();
        if (!player.Deck.Cards.Any(CardCatalog.IsKnowledge))
            foreach (string id in new[] { "JK48", "JK48B", "B" })
                await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model(id), player), PileType.Deck, skipVisuals: true);
        foreach (string id in new[] { "JK48B", "JK48", "JK206", "JK184", "JA82", "JK126" })
        {
            var card = CardCatalog.Create(id, player);
            if (id == "JK126") { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: false);
        }
        player.PlayerCombatState!.Energy = 20;
    }

    private static async Task Check(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        var rows = new List<object>();
        void Expect(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = actual is bool ? Equals(actual, expected) : Convert.ToDecimal(actual) == Convert.ToDecimal(expected) });
        string Describe(CardModel card)
        {
            card.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, card.DynamicVars);
            return Regex.Replace(card.GetDescriptionForPile(PileType.Hand), @"\[[^\]]*\]", "");
        }
        bool HasCount(string text, int count) => Regex.IsMatch(text, "[\uFF08(][^\uFF09)]*" + count + "\\u5F20[\uFF09)]");
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
        using var single = KnowledgeCombat.SingleCard();
        foreach (var id in new[] { "JK48", "JK48B", "B" })
            await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model(id), player), PileType.Deck, skipVisuals: true);
        int count = player.Deck.Cards.Count(CardCatalog.IsKnowledge);
        var temporary = CardCatalog.Create("B", player);
        await CardPileCmd.Add(temporary, PileType.Hand, skipVisuals: true);
        var reading = (CatalogCard)CardCatalog.Create("JK48B", player);
        await CardPileCmd.Add(reading, PileType.Hand, skipVisuals: true);
        foreach (bool upgraded in new[] { false, true })
        {
            if (upgraded) { reading.UpgradeInternal(); reading.FinalizeUpgradeInternal(); }
            var text = Describe(reading);
            rows.Add(new { name = "reading description", upgraded, text });
            Expect($"reading upgraded={upgraded} includes permanent knowledge count", HasCount(text, count), true);
            Expect($"reading upgraded={upgraded} block preview", reading.DynamicVars.Block.PreviewValue, (decimal)(reading.Value("Block") + count));
        }
        var extra = player.RunState.CreateCard(CardCatalog.Model("JK48B"), player);
        await CardPileCmd.Add(extra, PileType.Deck, skipVisuals: true);
        count++;
        Expect("deck gain refreshes count in description", HasCount(Describe(reading), count), true);
        Expect("deck gain refreshes total block", reading.DynamicVars.Block.PreviewValue, (decimal)(7 + count));
        await CardPileCmd.Add(temporary, PileType.Discard, skipVisuals: true);
        Expect("combat pile movement preserves deck-based block", Describe(reading).Length > 0 && reading.DynamicVars.Block.PreviewValue == 7 + count, true);
        await PowerCmd.Apply<DexterityPower>(context, player.Creature, 2, player.Creature, reading);
        Describe(reading);
        Expect("dexterity applies once to full conditional block", reading.DynamicVars.Block.PreviewValue, (decimal)(9 + count));
        await PowerCmd.Apply<FrailPower>(context, player.Creature, 1, player.Creature, reading);
        Describe(reading);
        decimal predicted = reading.DynamicVars.Block.PreviewValue;
        Expect("frail applies after full conditional block", predicted, (9 + count) * 0.75m);
        int beforeBlock = player.Creature.Block;
        player.PlayerCombatState!.Energy = 20;
        await CardActions.PaidPlay(context, reading);
        Expect("played block matches preview and permanent deck count", player.Creature.Block - beforeBlock, Math.Floor(predicted));
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        var citation = CardCatalog.Create("JK48", player);
        await CardPileCmd.Add(citation, PileType.Hand, skipVisuals: true);
        Expect("citation description includes permanent knowledge count", HasCount(Describe(citation), count), true);
        Expect("citation damage uses same permanent knowledge count", citation.DynamicVars.Damage.PreviewValue, (decimal)(6 + count));
        var deckCard = player.Deck.Cards.Last(c => CardCatalog.DesignId(c) == "JK48B");
        Expect("deck-view description includes live count", HasCount(Describe(deckCard), count), true);
        Expect("deck-view block preview includes knowledge", deckCard.DynamicVars.Block.PreviewValue, (decimal)(4 + count));
        var broad = CardCatalog.Create("JK206", player);
        await CardPileCmd.Add(broad, PileType.Hand, skipVisuals: true);
        Expect("deck-size card shows actual deck count", HasCount(Describe(broad), player.Deck.Cards.Count), true);
        var singing = CardCatalog.Create("JK184", player);
        await CardPileCmd.Add(singing, PileType.Hand, skipVisuals: true);
        DamageModifiers.Add(singing, extraHits: 2);
        Expect("hit-count card shows total hits", Describe(singing).Contains("\uFF083\u6B21\uFF09"), true);
        var rain = CardCatalog.Create("JA82", player);
        await CardPileCmd.Add(rain, PileType.Hand, skipVisuals: true);
        player.PlayerCombatState!.Energy = 7;
        Expect("X card shows current energy count", HasCount(Describe(rain), 7), true);
        player.PlayerCombatState.Energy = 0;
        Expect("X card refreshes at zero energy", HasCount(Describe(rain), 0), true);
        player.PlayerCombatState.Energy = 7;
        await HandArrow.Play(context, rain);
        Expect("waiting X arrow shows captured zero rather than current energy", HasCount(Describe(rain), 0), true);
        foreach (string id in new[] { "JK48", "JK48B", "JK206", "JK184", "JA82" })
        {
            var unowned = (CardModel)CardCatalog.Model(id).MutableClone();
            var text = unowned.GetDescriptionForPile(PileType.None);
            Expect(id + " ownerless preview is formatted", !text.Contains("ERROR") && !text.Contains('{'), true);
        }
        foreach (bool upgraded in new[] { false, true })
        {
            var inclusive = CardCatalog.Create("JK126", player);
            if (upgraded) { inclusive.UpgradeInternal(); inclusive.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(inclusive, PileType.Hand, skipVisuals: true);
            Describe(inclusive);
            Expect($"inclusive upgraded={upgraded} block preview", inclusive.DynamicVars.Block.PreviewValue, upgraded ? 12m : 8m);
            int before = player.Creature.Block;
            player.PlayerCombatState!.Energy = 20;
            await CardActions.PaidPlay(context, inclusive);
            Expect($"inclusive upgraded={upgraded} grants correct block", player.Creature.Block - before, upgraded ? 12m : 8m);
        }
        string report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "knowledge-count-preview-results.json"));
        File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
