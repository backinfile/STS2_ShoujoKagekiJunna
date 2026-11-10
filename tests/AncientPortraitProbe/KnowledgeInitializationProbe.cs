using System.Text.Json;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaAncientPortraitProbe;

public sealed class KnowledgeInitializationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_knowledge_initialization";
    public override string Args => "check";
    public override string Description => "Check native group initialization and reward association tips.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        return new(Check(player), true, "Knowledge initialization check started");
    }

    private static async Task Check(Player player)
    {
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/knowledge-initialization"));
        Directory.CreateDirectory(directory);
        var checks = new List<object>();
        try
        {
            var tips = CardCatalog.Model("JK126").HoverTips.OfType<HoverTip>().ToArray();
            checks.Add(new { name = "reward entrance explains associated reward cards", pass = tips.Any(tip =>
                tip.Title == "关联牌" && tip.Description.Contains("同一奖励页面的其他卡牌")),
                tips = tips.Select(tip => new { tip.Title, tip.Description }) });
            var source = player.RunState.CreateCard(CardCatalog.Model("JK32"), player);
            var before = player.Deck.Cards.ToHashSet();
            await CardPileCmd.Add(source, PileType.Deck, skipVisuals: true);
            var gained = player.Deck.Cards.Where(card => !before.Contains(card)).ToArray();
            var expected = new[] { "JUNNA_JK32", "JUNNA_JK32B", "JUNNA_JK32C" }.Order().ToArray();
            foreach (var card in gained)
            {
                var actual = KnowledgePreviewCards.Create(card).Select(member => member.Id.Entry).Order().ToArray();
                checks.Add(new { name = card.Id.Entry + " has the complete initial group", pass = actual.SequenceEqual(expected), actual });
            }

            foreach (var card in gained)
            {
                string self = "[gold]" + CardCatalog.Model(((JunnaCard)card).DesignId).Title + "[/gold]";
                var relationTips = card.HoverTips.OfType<HoverTip>().Where(tip => tip.Title == "关联牌").ToArray();
                checks.Add(new { name = card.Id.Entry + " tip omits itself", pass = relationTips.Length == 1 &&
                    !relationTips[0].Description.Contains(self) });
            }
            var curse = player.RunState.CreateCard(CardCatalog.Model("JK122B"), player);
            var referrers = CardCatalog.Groups.Where(group => group.GrantedCardIds.Contains("JK122B"))
                .Select(group => CardCatalog.Model(group.MainCardId).Id).ToHashSet();
            var actualReferrers = DynamicKnowledge.Members(curse).Where(card => card.Id != curse.Id)
                .Select(card => card.Id).ToHashSet();
            checks.Add(new { name = "shared curse lists all referring cards", pass = actualReferrers.SetEquals(referrers),
                expected = referrers.Select(id => id.Entry), actual = actualReferrers.Select(id => id.Entry) });
            var field = (SavedSpireField<Player, string>)AccessTools.Field(typeof(DynamicKnowledge), "Relations").GetValue(null)!;
            string original = field.Get(player) ?? throw new InvalidOperationException("Native relations were not saved.");
            var relations = JsonSerializer.Deserialize<List<KnowledgeRelation>>(original)!;
            var sourceRelation = relations.Single(relation => relation.EntranceId == source.Id.ToString());
            sourceRelation.CardCounts[CardCatalog.Model("JA105").Id.ToString()] = 1;
            field.Set(player, JsonSerializer.Serialize(relations));
            var sibling = gained.First(card => card.Id.Entry == "JUNNA_JK32B");
            checks.Add(new { name = "editing source does not update its sibling list", pass =
                DynamicKnowledge.Members(source).Any(card => card.Id.Entry == "JUNNA_JA105") &&
                DynamicKnowledge.Members(sibling).All(card => card.Id.Entry != "JUNNA_JA105") });
            var sameName = player.RunState.CreateCard(CardCatalog.Model("JK32"), player);
            checks.Add(new { name = "same-name instances use their updated source list", pass =
                DynamicKnowledge.Members(sameName).Any(card => card.Id.Entry == "JUNNA_JA105") });
            var restored = Player.FromSerializable(player.ToSerializable());
            foreach (var card in restored.Deck.Cards) card.Owner = restored;
            var restoredSibling = restored.Deck.Cards.First(card => card.Id == sibling.Id);
            checks.Add(new { name = "independent native lists survive save restoration", pass =
                DynamicKnowledge.Members(restoredSibling).All(card => card.Id.Entry != "JUNNA_JA105") });
            sourceRelation.CardCounts.Clear();
            sourceRelation.CardCounts[source.Id.ToString()] = 1;
            field.Set(player, JsonSerializer.Serialize(relations));
            checks.Add(new { name = "reduced list keeps only its source preview", pass =
                KnowledgePreviewCards.Create(source).Count == 1 &&
                !source.HoverTips.OfType<HoverTip>().Any(tip => tip.Title == "关联牌") });
            sourceRelation.CardCounts.Clear();
            field.Set(player, JsonSerializer.Serialize(relations));
            checks.Add(new { name = "empty list keeps the physical source and hides empty tips", pass =
                KnowledgePreviewCards.Create(source).Count == 1 &&
                !source.HoverTips.OfType<HoverTip>().Any(tip => tip.Title == "关联牌") });
            field.Set(player, original);
            var matchingSource = CardCatalog.Create("JK32", player);
            var matchingPartner = CardCatalog.Create("JK32B", player);
            using (KnowledgeCombat.SingleCard())
            {
                await CardPileCmd.AddGeneratedCardToCombat(matchingSource, PileType.Draw, player);
                await CardPileCmd.AddGeneratedCardToCombat(matchingPartner, PileType.Draw, player);
            }
            await KnowledgeCombat.DrawPartners(new ThrowingPlayerChoiceContext(), source);
            checks.Add(new { name = "drawing partners excludes a single source-name slot", pass = matchingSource.Pile?.Type == PileType.Draw });
            checks.Add(new { name = "drawing partners includes its initialized sibling", pass = matchingPartner.Pile?.Type == PileType.Hand });
            await CardPileCmd.RemoveFromCombat(matchingSource, skipVisuals: true);
            await CardPileCmd.RemoveFromCombat(matchingPartner, skipVisuals: true);
        }
        catch (Exception error) { checks.Add(new { name = "knowledge checks", pass = false, error = error.ToString() }); }
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
