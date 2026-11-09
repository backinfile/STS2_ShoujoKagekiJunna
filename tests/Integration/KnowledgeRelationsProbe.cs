using System.Text.Json;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class KnowledgeRelationsProbe : AbstractConsoleCmd
{
    private static CardModel _source = null!;
    private static CardModel _existing = null!;
    private static CardModel _later = null!;
    public override string CmdName => "junna_relations";
    public override string Args => "setup | check";
    public override string Description => "Check run-wide knowledge relations and reward entrances.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "setup" ? Setup(player) : Check(player), true, "Knowledge relations probe started");

    private static async Task Setup(Player player)
    {
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        _existing = player.RunState.CreateCard(CardCatalog.Model("JK184"), player);
        await CardPileCmd.Add(_existing, PileType.Deck, skipVisuals: true);
        _source = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
        var paper = player.RunState.CreateCard(CardCatalog.Model("JK184"), player);
        var speed = player.RunState.CreateCard(CardCatalog.Model("JA105"), player);
        // Reserve a second acquisition before either offer is accepted, so both merges are exercised.
        _later = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
        await DynamicKnowledge.Prepare(new ThrowingPlayerChoiceContext(), _later,
            [_later, player.RunState.CreateCard(CardCatalog.Model("JK122"), player),
                player.RunState.CreateCard(ModelDb.Card<StrikeIronclad>(), player)]);
        using (KnowledgeCombat.SingleCard())
            foreach (string id in new[] { "JA105", "JK184B", "JK184C" })
                await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Hand, player);
        var reward = new CardReward([_source, paper, speed], CardCreationSource.Other, player,
            new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform));
        _ = reward.SelectUnsynchronized();
    }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        void Group(CardModel card, int count)
        {
            int actual = DynamicKnowledge.Members(card).Count;
            Expect(card.Id + $" shares the full group ({actual}/{count})", actual == count);
            var tips = card.HoverTips.OfType<HoverTip>().Where(tip => tip.Id == "junna_dynamic_knowledge_group").ToArray();
            Expect(card.Id + " has one complete relation tip", tips.Length == 1 && tips[0].Description.Split('\n').Length == count);
        }
        try
        {
            Expect("native reward selection acquired JK126", _source.Pile?.Type == PileType.Deck);
            Group(_source, 9);
            Group(_existing, 9);
            foreach (var card in player.PlayerCombatState!.Hand.Cards.Where(DynamicKnowledge.IsLinked)) Group(card, 9);
            var fresh = player.RunState.CreateCard(CardCatalog.Model("JA105"), player);
            Expect("new same-ID card is knowledge", CardCatalog.IsKnowledge(fresh) && fresh.Keywords.Contains(JunnaKeywords.Knowledge));
            Group(fresh, 9);
            Group((CardModel)fresh.MutableClone(), 9);
            Expect("new same-ID preview includes absent members", KnowledgePreviewCards.Create(fresh).Count == 9);
            int before = player.Deck.Cards.Count;
            using (KnowledgeCombat.SingleCard())
                await CardPileCmd.Add(ShoujoKageki.Core.V1.CardCopies.ForDeck(_existing, player,
                    ShoujoKageki.Core.V1.CardCopyPurpose.Transfer), PileType.Deck, skipVisuals: true);
            Expect("copy acquisition remains a single physical card", player.Deck.Cards.Count == before + 1);
            Group(_existing, 9);
            var entrance = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
            before = player.Deck.Cards.Count;
            await CardPileCmd.Add(entrance, PileType.Deck, skipVisuals: true);
            Expect("later entrance acquires the complete ID group", player.Deck.Cards.Count == before + 9);
            var options = new CardCreationOptions([ModelDb.CardPool<JunnaCardPool>()], CardCreationSource.Other,
                CardRarityOddsType.Uniform, card => card.Id == _source.Id || card.Id == fresh.Id);
            var originalFilter = options.CardPoolFilter;
            Expect("native rewards only roll JK126", Enumerable.Range(0, 12).All(_ =>
                CardFactory.CreateForReward(player, 1, options).Single().Card.Id == _source.Id));
            Expect("reward filter preserves the caller's options", ReferenceEquals(options.CardPoolFilter, originalFilter));
            foreach (var member in DynamicKnowledge.Members(_source).DistinctBy(card => card.Id))
                Expect(member.Id + " obeys sole entrance", DynamicKnowledge.IsRewardEntrance(player, member) == (member.Id == _source.Id));
            Player Restore(Player original)
            {
                var copy = Player.FromSerializable(original.ToSerializable());
                // RunState normally assigns owners after loading inventory; isolated round trips do this here.
                foreach (var card in copy.Deck.Cards) card.Owner = copy;
                return copy;
            }
            var restored = Restore(player);
            var restoredSource = restored.Deck.Cards.First(card => card.Id == _source.Id);
            Group(restoredSource, 9);
            Expect("restored group retains entrance", !DynamicKnowledge.IsRewardEntrance(restored, fresh));
            var foreign = (CardModel)ModelDb.Card<StrikeIronclad>().MutableClone();
            foreign.Owner = restored;
            Expect("unrelated player state does not inherit foreign relations", !DynamicKnowledge.IsLinked(foreign));
            await CardPileCmd.Add(_later, PileType.Deck, skipVisuals: true);
            int expected = 9 + KnowledgeCombat.Group(CardCatalog.Model("JK122"))!.Members.Sum(member => member.Count) + 1;
            Group(_source, expected);
            Group(fresh, expected);
            var strike = player.Creature.CombatState!.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
            Group(strike, expected);
            Group(restoredSource, 9);
            Expect("foreign card becomes knowledge by ID", strike.Keywords.Contains(JunnaKeywords.Knowledge));
            Expect("foreign card is excluded from its native reward pool", !DynamicKnowledge.IsRewardEntrance(player, ModelDb.Card<StrikeIronclad>()));
            using (KnowledgeCombat.SingleCard())
                await CardPileCmd.AddGeneratedCardToCombat(strike, PileType.Draw, player);
            await KnowledgeCombat.DrawPartners(new ThrowingPlayerChoiceContext(), player.PlayerCombatState.Hand.Cards.First());
            Expect("drawing partners includes newly generated foreign model IDs", strike.Pile?.Type == PileType.Hand);
            Group(strike.CreateClone(), expected);
            // Relation membership survives even when no physical member remains in the saved deck.
            var field = (SavedSpireField<Player, string>)AccessTools.Field(typeof(DynamicKnowledge), "Relations").GetValue(null)!;
            var isolated = Restore(player);
            foreach (var card in isolated.Deck.Cards.ToArray()) isolated.Deck.RemoveInternal(card, silent: true);
            var emptyRestored = Restore(isolated);
            var afterRemoval = (CardModel)CardCatalog.Model("JA105").MutableClone();
            afterRemoval.Owner = emptyRestored;
            Group(afterRemoval, expected);
            Expect("relations are stored on the player", !string.IsNullOrEmpty(field.Get(emptyRestored)));
            // Old per-card saves migrate the complete static group and retain the forced entrance.
            var legacy = Restore(player);
            field.Set(legacy, null!);
            var links = (SavedSpireField<CardModel, string>)AccessTools.Field(typeof(DynamicKnowledge), "Links").GetValue(null)!;
            links.Set(legacy.Deck.Cards.First(card => CardCatalog.DesignId(card) == "JK184B"), "legacy/1");
            legacy = Restore(legacy);
            var legacyPaper = legacy.Deck.Cards.First(card => CardCatalog.DesignId(card) == "JK184");
            Group(legacyPaper, 8);
            Expect("legacy migration keeps JK126 as entrance", !DynamicKnowledge.IsRewardEntrance(legacy, legacyPaper));
            var synchronized = player.ToSerializable();
            field.Set(player, field.Get(restored));
            player.SyncWithSerializedPlayer(synchronized);
            Group(player.Deck.Cards.First(card => card.Id == _source.Id), expected);
            if (typeof(KnowledgeCombat).GetMethod("PlayPartners") is { } playPartners)
            {
                foreach (var card in player.PlayerCombatState.Hand.Cards.ToArray())
                    await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
                var source = CardCatalog.Create("JK126", player);
                var first = CardCatalog.Create("JK184C", player);
                var second = CardCatalog.Create("JK184C", player);
                CardModel copy;
                using (KnowledgeCombat.SingleCard())
                {
                    foreach (var card in new[] { source, first, second })
                        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
                    copy = first.CreateClone();
                    await CardPileCmd.Add(copy, PileType.Hand, skipVisuals: true);
                }
                await (Task)playPartners.Invoke(null, [new ThrowingPlayerChoiceContext(), source])!;
                Expect("integrated partner play includes every same-ID copy", new[] { first, second, copy }.All(card => card.Pile != player.PlayerCombatState.Hand));
                Expect("integrated partner play leaves its source in hand", source.Pile == player.PlayerCombatState.Hand);
            }
            var queued = new CardReward([fresh, player.RunState.CreateCard(CardCatalog.Model("JK126"), player)],
                CardCreationSource.Other, player, new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform));
            _ = queued.SelectUnsynchronized();
            Expect("previously prepared rewards collapse to one valid entrance", queued.Cards.Count() == 1 && queued.Cards.Single().Id == _source.Id);
        }
        catch (Exception ex) { rows.Add(new { name = "probe exception", pass = false, error = ex.ToString() }); }
        string directory = Path.GetFullPath("../../../out/tmp/knowledge-global-groups-20261109");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "integration-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
