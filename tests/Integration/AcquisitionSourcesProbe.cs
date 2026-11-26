using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class AcquisitionSourcesProbe : AbstractConsoleCmd
{
    private static readonly List<object> Rows = [];
    private static int _beforeReward;
    private static CardModel[] _dynamicOffers = [];
    private static CardModel? _previousChain;
    private static CardModel[] _beforeDynamicReward = [];
    private static CardModel[] _offered = [];
    private static bool OfferCards(ref IEnumerable<CardCreationResult> __result)
    {
        __result = _offered.Select(card => new CardCreationResult(card)).ToArray();
        return false;
    }
    private static bool TransformCard(CardModel original, ref CardModel __result)
    {
        __result = original.Owner.RunState.CreateCard(CardCatalog.Model("JK314"), original.Owner);
        return false;
    }
    public override string CmdName => "junna_acquisition_sources";
    public override string Args => "check | reward | unknown-reward | reward-check | dynamic-reward | dynamic-reward-check";
    public override string Description => "Check named acquisition sources and default single-card gains.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() switch
        {
            "reward" => Reward(player, true),
            "unknown-reward" => Reward(player, false),
            "dynamic-reward" => Reward(player, true, "JK126"),
            "dynamic-reward-check" => DynamicRewardCheck(player),
            "reward-check" => RewardCheck(player, int.Parse(args[1])),
            _ => Check(player)
        }, true, "Acquisition source probe started");
    private static void Expect(string name, int actual, int expected)
    {
        Rows.Add(new { name, actual, expected, pass = actual == expected });
        Save();
    }
    private static void Save()
    {
        string path = Path.GetFullPath("../../../out/tmp/acquisition-sources-20261111/integration-results.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(Rows, new JsonSerializerOptions { WriteIndented = true }));
    }
    private sealed class Selector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minimum, int maximum)
            => Task.FromResult(options.Take(1));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static async Task Check(Player player)
    {
        Rows.Clear();
        try
        {
            CardModel Create(string id) => player.RunState.CreateCard(CardCatalog.Model(id), player);
            int before = player.Deck.Cards.Count;
            var source = Create("JK314");
            await CardPileCmd.Add(source, PileType.Deck, skipVisuals: true);
            Expect("unadapted Add grants one card", player.Deck.Cards.Count - before, 1);
            before = player.Deck.Cards.Count;
            await CardPileCmd.Add(new[] { Create("JK314"), Create("JK32") }, PileType.Deck, skipVisuals: true);
            Expect("unadapted batch adds only its two cards", player.Deck.Cards.Count - before, 2);
            before = player.Deck.Cards.Count;
            await CardPileCmd.Add(player.RunState.CloneCard(source), PileType.Deck, skipVisuals: true);
            Expect("native copy remains one physical card", player.Deck.Cards.Count - before, 1);
            var original = Create("JB01");
            await CardPileCmd.Add(original, PileType.Deck, skipVisuals: true);
            before = player.Deck.Cards.Count;
            await CardCmd.Transform(original, Create("JK314"), MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None);
            Expect("unadapted Transform does not grant companions", player.Deck.Cards.Count - before, 0);
            var tome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
            tome.AncientCard = CardCatalog.Model("JK314").Id;
            before = player.Deck.Cards.Count;
            await RelicCmd.Obtain(tome, player);
            Expect("DustyTome.AfterObtained grants its complete knowledge group", player.Deck.Cards.Count - before, 4);
            var singleTome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
            singleTome.AncientCard = CardCatalog.Model("JK314").Id;
            before = player.Deck.Cards.Count;
            using (KnowledgeCombat.SingleCard()) await RelicCmd.Obtain(singleTome, player);
            Expect("single-card scope overrides a registered source", player.Deck.Cards.Count - before, 1);
            await Task.Yield();
            before = player.Deck.Cards.Count;
            await CardPileCmd.Add(Create("JK314"), PileType.Deck, skipVisuals: true);
            Expect("registered source does not leak into later Add", player.Deck.Cards.Count - before, 1);
            using (TestGameApi.UseSelector(new Selector()))
            {
                var patcher = new Harmony("Junna.AcquisitionChecks.Factory");
                var factory = AccessTools.Method(typeof(CardFactory), "CreateForReward", [typeof(Player), typeof(int), typeof(CardCreationOptions)]);
                var offer = AccessTools.Method(typeof(AcquisitionSourcesProbe), nameof(OfferCards));
                _offered = [Create("JK314"), Create("JB01")];
                patcher.Patch(factory, prefix: new HarmonyMethod(offer));
                before = player.Deck.Cards.Count;
                try { await RelicCmd.Obtain(ModelDb.Relic<HeftyTablet>().ToMutable(), player); }
                finally { patcher.Unpatch(factory, offer); _offered = []; }
                Expect("HeftyTablet.AfterObtained grants the selected group plus one Injury", player.Deck.Cards.Count - before, 5);
                var transformation = AccessTools.Method(typeof(CardFactory), "CreateRandomCardForTransform",
                    [typeof(CardModel), typeof(bool), typeof(MegaCrit.Sts2.Core.Random.Rng)]);
                var transform = AccessTools.Method(typeof(AcquisitionSourcesProbe), nameof(TransformCard));
                patcher.Patch(transformation, prefix: new HarmonyMethod(transform));
                before = player.Deck.Cards.Count;
                try { await RelicCmd.Obtain(ModelDb.Relic<Astrolabe>().ToMutable(), player); }
                finally { patcher.Unpatch(transformation, transform); }
                Expect("Astrolabe.AfterObtained replaces one card with its knowledge group", player.Deck.Cards.Count - before, 3);
            }
            var combat = player.Creature.CombatState!;
            var discovery = combat.CreateCard<Discovery>(player);
            discovery.MockSelectedCard(combat.CreateCard(CardCatalog.Model("JK314"), player));
            await CardPileCmd.AddGeneratedCardToCombat(discovery, PileType.Hand, player);
            var oldCards = player.PlayerCombatState!.AllCards.ToHashSet();
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), discovery, null);
            Expect("Discovery.OnPlay grants the selected knowledge group in combat",
                player.PlayerCombatState.AllCards.Count(card => !oldCards.Contains(card)), 4);
            using var selector = TestGameApi.UseSelector(new Selector());
            var chosen = await CardSelectCmd.FromChooseACardScreen(new ThrowingPlayerChoiceContext(),
                new[] { combat.CreateCard(CardCatalog.Model("JK314"), player) }, player);
            oldCards = player.PlayerCombatState.AllCards.ToHashSet();
            await CardPileCmd.AddGeneratedCardToCombat(chosen!, PileType.Hand, player);
            Expect("unadapted use of the discovery screen grants only one card",
                player.PlayerCombatState.AllCards.Count(card => !oldCards.Contains(card)), 1);
        }
        catch (Exception error) { Rows.Add(new { name = "probe exception", pass = false, error = error.ToString() }); Save(); }
    }
    private static async Task Reward(Player player, bool known, string id = "JK314")
    {
        var cards = (id == "JK126" ? new[] { "JK126", "JK314", "JB01" } : new[] { id })
            .Select(cardId => player.RunState.CreateCard(CardCatalog.Model(cardId), player)).ToArray();
        if (id == "JK126")
        {
            Rows.Clear();
            _previousChain = player.RunState.CreateCard(CardCatalog.Model(id), player);
            await CardPileCmd.Add(_previousChain, PileType.Deck, skipVisuals: true);
            _beforeDynamicReward = player.Deck.Cards.ToArray();
            _dynamicOffers = cards;
            foreach (var card in cards.Take(2))
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }
        }
        _beforeReward = player.Deck.Cards.Count;
        var reward = new CardReward(cards, known ? CardCreationSource.Encounter : CardCreationSource.Other,
            player, new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform));
        _ = reward.SelectUnsynchronized();
    }

    private static Task DynamicRewardCheck(Player player)
    {
        var companionId = CardCatalog.Model("JK122B").Id;
        var before = _beforeDynamicReward.ToHashSet();
        var gained = player.Deck.Cards.Where(card => !before.Contains(card)).ToArray();
        Expect("Chain Knowledge keeps all offered instances", _dynamicOffers.Count(gained.Contains), 3);
        Expect("the offered Knowledge card keeps its upgrade", gained.Count(card => card == _dynamicOffers[1] && card.IsUpgraded), 1);
        Expect("Chain Knowledge grants exactly the offered cards and three companions", gained.Length, 6);
        Expect("all three companions enter the deck at base level", gained.Count(card => card.Id == companionId && !card.IsUpgraded), 3);
        var associates = DynamicKnowledge.Members(_dynamicOffers[0]).Select(card => card.Id).ToArray();
        Expect("all three companions become Chain Knowledge associates", associates.Count(id => id == companionId), 3);
        Expect("base and upgraded Chain Knowledge share all associations",
            DynamicKnowledge.Members(_previousChain!).Select(card => card.Id).SequenceEqual(associates) ? 1 : 0, 1);
        Expect("the offered Knowledge card keeps its own group",
            DynamicKnowledge.Members(_dynamicOffers[1]).Count(card => card.Id == companionId), 3);
        Expect("the original deck order is retained",
            player.Deck.Cards.Where(before.Contains).SequenceEqual(_beforeDynamicReward) ? 1 : 0, 1);
        return Task.CompletedTask;
    }
    private static Task RewardCheck(Player player, int count)
    {
        Expect(count == 4 ? "native encounter reward grants companions" : "unregistered reward origin does not grant companions",
            player.Deck.Cards.Count - _beforeReward, count);
        return Task.CompletedTask;
    }
}
