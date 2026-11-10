using System.Text.Json;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaAncientPortraitProbe;

public sealed class RewardAssociationCommand : AbstractConsoleCmd
{
    private static CardModel _source = null!, _foreign = null!, _native = null!;
    private static string[] _foreignTips = [], _nativeTips = [], _foreignKeywords = [];
    private static string OutputDirectory => Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory,
        "../../../out/tmp/reward-associations"));
    public override string CmdName => "junna_reward_associations";
    public override string Args => "prepare | check | showcase";
    public override string Description => "Check the persistent acquisition heading and untouched associated cards.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        if (args.Contains("prepare"))
        {
            _source = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
            _foreign = player.RunState.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
            _native = player.RunState.CreateCard(CardCatalog.Model("JK32"), player);
            _foreignTips = Tips(_foreign);
            _nativeTips = Tips(_native);
            _foreignKeywords = Keywords(_foreign);
            var reward = new CardReward([_source, _foreign, _native], CardCreationSource.Other, player,
                new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform));
            _ = reward.SelectUnsynchronized();
            return new(true, "Choose the first reward, then run check");
        }
        return new(args.Contains("showcase") ? Showcase(player) : Check(player), true, "Reward association checks started");
    }

    private static string[] Tips(CardModel card) => card.HoverTips.OfType<HoverTip>()
        .Select(tip => tip.Title + "\n" + tip.Description).ToArray();
    private static string[] Keywords(CardModel card) => card.Keywords.Select(keyword => keyword.ToString()).Order().ToArray();
    private static string AssociationDescription(CardModel card) => card.HoverTips.OfType<HoverTip>()
        .SingleOrDefault(tip => tip.Title == "关联牌").Description ?? "";

    private static async Task Check(Player player)
    {
        var checks = new List<object>();
        void Expect(string name, bool pass, string? actual = null) => checks.Add(new { name, pass, actual });
        void Heading(string name, CardModel card)
        {
            string description = AssociationDescription(card);
            string first = description.Split('\n')[0];
            Expect(name + " starts with its acquisition heading", first.Contains("同一奖励页面的其他卡牌"), description);
            Expect(name + " highlights its heading", first.Contains("[b]") && first.Contains("[gold]"), first);
        }
        try
        {
            Expect("native reward selection acquired the entrance", _source.Pile?.Type == PileType.Deck);
            Heading("unacquired model", CardCatalog.Model("JK126"));
            Heading("acquired entrance", _source);
            Expect("captured card keeps its knowledge classification", !CardCatalog.IsKnowledge(_foreign));
            Expect("captured card keeps its keywords", Keywords(_foreign).SequenceEqual(_foreignKeywords));
            Expect("captured card keeps its hover tips", Tips(_foreign).SequenceEqual(_foreignTips));
            Expect("captured knowledge card keeps its own hover tips", Tips(_native).SequenceEqual(_nativeTips));
            string before = AssociationDescription(_source);
            Expect("captured names follow the heading", before.Contains(_foreign.Title) && before.Contains(_native.Title), before);
            var later = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
            var extra = player.RunState.CreateCard(ModelDb.Card<DefendIronclad>(), player);
            string[] extraTips = Tips(extra);
            await DynamicKnowledge.Prepare(new ThrowingPlayerChoiceContext(), later, [later, extra]);
            await CardPileCmd.Add(later, PileType.Deck, skipVisuals: true);
            Heading("entrance after another acquisition", _source);
            string after = AssociationDescription(_source);
            Expect("later acquisition adds its name after the existing names", after.Contains(_foreign.Title) &&
                after.Contains(_native.Title) && after.Contains(extra.Title), after);
            Expect("later captured card keeps its knowledge classification", !CardCatalog.IsKnowledge(extra));
            Expect("later captured card keeps its hover tips", Tips(extra).SequenceEqual(extraTips));
            Expect("same-name entrance shares the updated tip", AssociationDescription(later) == after);
            var restored = Player.FromSerializable(player.ToSerializable());
            foreach (var card in restored.Deck.Cards) card.Owner = restored;
            var restoredSource = restored.Deck.Cards.First(card => card.Id == _source.Id);
            var restoredForeign = restored.Deck.Cards.First(card => card.Id == _foreign.Id);
            Expect("heading and accumulated names survive restoration", AssociationDescription(restoredSource) == after);
            Expect("restored captured card keeps its original classification and tips", !CardCatalog.IsKnowledge(restoredForeign) &&
                Tips(restoredForeign).SequenceEqual(_foreignTips));
            var field = (SavedSpireField<Player, string>)AccessTools.Field(typeof(DynamicKnowledge), "Relations").GetValue(null)!;
            string saved = field.Get(player)!;
            var relations = JsonSerializer.Deserialize<List<KnowledgeRelation>>(saved)!;
            var relation = relations.Single(group => group.EntranceId == _source.Id.ToString());
            relation.CardCounts.Clear();
            field.Set(player, JsonSerializer.Serialize(relations));
            Heading("entrance with no captured cards", _source);
            field.Set(player, saved);
        }
        catch (Exception error) { checks.Add(new { name = "reward association checks", pass = false, actual = error.ToString() }); }
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "results.json"), JsonSerializer.Serialize(checks,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task Showcase(Player player)
    {
        foreach (var card in player.PlayerCombatState!.Hand.Cards.ToArray())
            await CardPileCmd.RemoveFromCombat(card);
        foreach (var card in new[] { CardCatalog.Model("JK126"), ModelDb.Card<StrikeIronclad>(),
                     CardCatalog.Model("JK32"), ModelDb.Card<DefendIronclad>() })
            await CardPileCmd.Add(player.Creature.CombatState!.CreateCard(card, player), PileType.Hand);
    }
}
