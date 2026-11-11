using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class KnowledgeGridSourcesProbe : AbstractConsoleCmd
{
    private static Player _player = null!;
    private static readonly List<CardModel> Offers = [];
    private static CardModel[] _selected = [];
    private static int _take;
    private static bool _dynamic;
    private static string OfferId(int index) => _dynamic ? new[] { "JK126", "JB01", "JG27", "JA75", "JA82" }[index % 5] : Ids[index % Ids.Length];
    private static readonly string[] Ids = ["JH27", "JK67", "JB01", "JG27", "JA75"];
    public override string CmdName => "junna_grid_sources";
    public override string Args => "check <phase>";
    public override string Description => "Check all native simple-grid knowledge acquisition sources.";
    public override bool IsNetworked => false;
    private static bool RewardCards(int __1, ref IEnumerable<CardCreationResult> __result)
    {
        var cards = Enumerable.Range(0, __1).Select(i => _player.RunState.CreateCard(CardCatalog.Model(OfferId(i)), _player)).ToArray();
        Offers.AddRange(cards); __result = cards.Select(card => new CardCreationResult(card)); return false;
    }
    private static bool CombatCards(int __2, ref IEnumerable<CardModel> __result)
    {
        var cards = Enumerable.Range(0, __2).Select(i => _player.Creature.CombatState!.CreateCard(CardCatalog.Model(OfferId(i)), _player)).ToArray();
        Offers.AddRange(cards); __result = cards; return false;
    }
    private sealed class Selector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minimum, int maximum)
        { _selected = options.Take(_take).ToArray(); return Task.FromResult<IEnumerable<CardModel>>(_selected); }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) => throw new NotSupportedException();
    }
    private static Harmony? _demo;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        var factory = AccessTools.Method(typeof(CardFactory), "CreateForReward", [typeof(Player), typeof(int), typeof(CardCreationOptions)]);
        var prefix = AccessTools.Method(typeof(KnowledgeGridSourcesProbe), nameof(RewardCards));
        if (args.FirstOrDefault() == "demo-factory")
        {
            _player = player; _dynamic = false; Offers.Clear();
            _demo = new Harmony("Junna.KnowledgeGridSources.Demo");
            _demo.Patch(factory, prefix: new HarmonyMethod(prefix));
            return new(true, "Prepared deterministic native event offers");
        }
        if (args.FirstOrDefault() == "demo-stop")
        {
            _demo?.Unpatch(factory, prefix); _demo = null;
            return new(true, "Restored native card factory");
        }
        return player.PlayerCombatState == null ? new(false, "Combat required")
            : new(Check(player, args.ElementAtOrDefault(1) ?? "grid-sources"), true, "Checking five native grid sources");
    }
    private static async Task Check(Player player, string phase)
    {
        _player = player;
        string root = System.Environment.GetEnvironmentVariable("JUNNA_KNOWLEDGE_STACK_OUTPUT")!;
        var rows = new List<object>();
        void Expect(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var harmony = new Harmony("Junna.KnowledgeGridSources");
        var factory = AccessTools.Method(typeof(CardFactory), "CreateForReward", [typeof(Player), typeof(int), typeof(CardCreationOptions)]);
        var combatFactory = typeof(CardFactory).GetMethods().Single(method => method.Name == "GetDistinctForCombat" && method.GetParameters().Length == 4);
        var rewardPatch = AccessTools.Method(typeof(KnowledgeGridSourcesProbe), nameof(RewardCards));
        var combatPatch = AccessTools.Method(typeof(KnowledgeGridSourcesProbe), nameof(CombatCards));
        harmony.Patch(factory, prefix: new HarmonyMethod(rewardPatch));
        harmony.Patch(combatFactory, prefix: new HarmonyMethod(combatPatch));
        using var selector = CardSelectCmd.UseSelector(new Selector());
        try
        {
            foreach (string source in new[] { "BrainLeech", "RoomFullOfCheese", "SeaGlass", "SeaGlassSkip", "ChoicesParadox", "SealedDeck", "BrainLeechDynamic", "ChoicesParadoxDynamic", "SealedDeckDynamic" })
            {
                _dynamic = source.EndsWith("Dynamic"); string nativeSource = source.Replace("Dynamic", "");
                Offers.Clear(); _selected = []; _take = nativeSource switch { "RoomFullOfCheese" or "SeaGlass" => 2, "SeaGlassSkip" => 0, "SealedDeck" => 10, _ => 1 };
                bool combat = nativeSource == "ChoicesParadox";
                var before = (combat ? player.PlayerCombatState!.AllCards : player.Deck.Cards).ToHashSet();
                try
                {
                    if (nativeSource is "BrainLeech" or "RoomFullOfCheese")
                    {
                        var type = typeof(EventModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Events." + nativeSource)!;
                        var model = (EventModel)ModelDb.AllEvents.Single(model => model.GetType() == type).MutableClone();
                        AccessTools.PropertySetter(typeof(EventModel), "Owner").Invoke(model, [player]);
                        await (Task)AccessTools.Method(type, nativeSource == "BrainLeech" ? "ShareKnowledge" : "Gorge").Invoke(model, [])!;
                    }
                    else if (nativeSource.StartsWith("SeaGlass"))
                    {
                        var relic = (SeaGlass)ModelDb.Relic<SeaGlass>().ToMutable(); relic.Owner = player; relic.CharacterId = player.Character.Id;
                        await relic.AfterObtained();
                    }
                    else if (nativeSource == "ChoicesParadox")
                    {
                        var relic = (ChoicesParadox)ModelDb.Relic<ChoicesParadox>().ToMutable(); relic.Owner = player;
                        Expect("ChoicesParadox native first-turn condition", player.PlayerCombatState!.TurnNumber, 1);
                        await relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), player);
                    }
                    else await (Task)AccessTools.Method(typeof(SealedDeck), "ChooseCards").Invoke(null, [player])!;
                    var acquired = (combat ? player.PlayerCombatState!.AllCards : player.Deck.Cards).Where(card => !before.Contains(card)).ToArray();
                    int expected = _dynamic ? Offers.Count : _selected.Sum(card => Math.Max(1, KnowledgePreviewCards.Create(card).Count));
                    Expect(source + " selected root count", _selected.Length, _take);
                    Expect(source + " acquires complete selected groups", acquired.Length, expected);
                    Expect(source + " retains chosen physical models", _selected.All(acquired.Contains), true);
                    if (_dynamic) Expect(source + " acquires each candidate once", Offers.All(acquired.Contains) && acquired.Distinct().Count() == Offers.Count, true);
                    else Expect(source + " leaves unselected models detached", Offers.Except(_selected).All(card => card.Pile == null), true);
                }
                catch (Exception error) { rows.Add(new { name = source + " exception", pass = false, error = error.ToString() }); }
                int singleBefore = player.Deck.Cards.Count;
                var single = player.RunState.CreateCard(CardCatalog.Model("JH27"), player);
                await CardPileCmd.Add(single, PileType.Deck, skipVisuals: true);
                Expect(source + " source scope does not leak", player.Deck.Cards.Count - singleBefore, 1);
            }
        }
        finally { harmony.Unpatch(factory, rewardPatch); harmony.Unpatch(combatFactory, combatPatch); }
        File.WriteAllText(Path.Combine(root, phase + ".json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
