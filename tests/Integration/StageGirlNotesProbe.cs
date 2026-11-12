using System.Text.Json;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class StageGirlNotesProbe : AbstractConsoleCmd
{
    private static readonly string[] Ids = Enumerable.Range(359, 9).Select(number => "JK" + number).ToArray();
    private static readonly Dictionary<string, object> Checks = [];
    private static readonly Dictionary<CardModel, bool> Before = [];
    private static string Report => Path.GetFullPath("../../../out/tmp/act2-event/checks.json");
    public override string CmdName => "junna_notes";
    public override string Args => "prepare <eligible-count> | study | practice | eligibility | effects";
    public override string Description => "Focused checks for the stage girl notes event.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        switch (args.FirstOrDefault())
        {
            case "deck":
                MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen.ShowScreen(player);
                return new(true, "Opened native deck view");
            case "close-deck":
                MegaCrit.Sts2.Core.Nodes.Screens.Capstones.NCapstoneContainer.Instance?.Close();
                return new(true, "Closed native deck view");
            case "summary":
                var outcome = (SavedSpireField<Player, string>)AccessTools.Field(typeof(JunnaStageGirlNotes), "Outcome").GetValue(null)!;
                return new(true, JsonSerializer.Serialize(new
                {
                    total = player.Deck.Cards.Count,
                    cards = player.Deck.Cards.GroupBy(card => CardCatalog.DesignId(card) ?? card.Id.Entry)
                        .ToDictionary(group => group.Key, group => group.Count()),
                    upgraded = player.Deck.Cards.Count(card => card.IsUpgraded),
                    outcome = outcome.Get(player)
                }));
            case "prepare-knowledge":
                return new(Prepare(player, int.Parse(args[1]), true), true, "Knowledge upgrade fixture prepared");
            case "prepare":
                return new(Prepare(player, int.Parse(args[1])), true, "Event fixture prepared");
            case "study":
                Check("study deck total", player.Deck.Cards.Count, Before.Count + 9);
                foreach (string id in Ids)
                    Check("study " + id, player.Deck.Cards.Count(card => CardCatalog.DesignId(card) == id),
                        1 + Before.Keys.Count(card => CardCatalog.DesignId(card) == id));
                Check("existing copies preserved", Before.All(pair => player.Deck.Cards.Contains(pair.Key)
                    && pair.Key.IsUpgraded == pair.Value), true);
                break;
            case "practice":
                Check("practice deck size", player.Deck.Cards.Count, Before.Count);
                Check("practice upgraded copies", Before.Count(pair => !pair.Value && pair.Key.IsUpgraded),
                    Math.Min(1, Before.Count(pair => !pair.Value && CardCatalog.IsKnowledge(pair.Key))));
                Check("practice upgrades knowledge only", Before.Where(pair => !pair.Value && pair.Key.IsUpgraded)
                    .All(pair => CardCatalog.IsKnowledge(pair.Key)), true);
                Check("practice preserves ordinary cards", Before.Where(pair => !CardCatalog.IsKnowledge(pair.Key))
                    .All(pair => pair.Key.IsUpgraded == pair.Value), true);
                break;
            case "eligibility":
                CheckEligibility(player);
                break;
            case "effects":
                return new(Effects(player), true, "Nine card effects started");
            default:
                return new(false, Args);
        }
        return new(true, "Checks saved");
    }

    private static async Task Prepare(Player player, int eligible, bool knowledgeCandidates = false)
    {
        var field = (SavedSpireField<Player, string>)AccessTools.Field(typeof(JunnaStageGirlNotes), "Outcome").GetValue(null)!;
        field.Set(player, null!);
        foreach (var card in player.Deck.Cards.ToArray())
            await CardPileCmd.RemoveFromDeck(card, false);
        using var single = KnowledgeCombat.SingleCard();
        var state = (RunState)player.RunState;
        var notes = ModelDb.GetById<JunnaStageGirlNotes>(ModelDb.GetId<JunnaStageGirlNotes>());
        var roomSet = (MegaCrit.Sts2.Core.Rooms.RoomSet)AccessTools.Field(typeof(ActModel), "_rooms").GetValue(state.Act)!;
        Check("event model registered", ModelDb.AllEvents.Any(model => model.Id == notes.Id), true);
        roomSet.events.RemoveAll(model => model.Id == notes.Id);
        roomSet.events.Insert(roomSet.eventsVisited % (roomSet.events.Count + 1), notes);
        ((HashSet<ModelId>)AccessTools.Field(typeof(RunState), "_visitedEventIds").GetValue(state)!).Remove(notes.Id);
        state.Odds.UnknownMapPoint.MonsterOdds = 0;
        state.Odds.UnknownMapPoint.EliteOdds = 0;
        state.Odds.UnknownMapPoint.ShopOdds = 0;
        state.Odds.UnknownMapPoint.TreasureOdds = 0;
        Before.Clear();
        for (int index = 0; index < eligible + 1; index++)
        {
            var card = player.RunState.CreateCard(CardCatalog.Model(index == eligible ? "JK359" : knowledgeCandidates ? Ids[index % Ids.Length] : "JB01"), player);
            if (index == eligible) CardCmd.Upgrade(card);
            await CardPileCmd.Add(card, PileType.Deck, CardPilePosition.Bottom, skipVisuals: true);
            Before.Add(card, card.IsUpgraded);
        }
        if (knowledgeCandidates)
        {
            var ordinary = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
            await CardPileCmd.Add(ordinary, PileType.Deck, CardPilePosition.Bottom, skipVisuals: true);
            Before.Add(ordinary, ordinary.IsUpgraded);
        }
    }

    private static void CheckEligibility(Player player)
    {
        var state = (RunState)player.RunState;
        var model = ModelDb.GetById<JunnaStageGirlNotes>(ModelDb.GetId<JunnaStageGirlNotes>());
        int originalAct = state.CurrentActIndex;
        var actIndexField = AccessTools.Field(typeof(RunState), "_currentActIndex");
        try
        {
            for (int act = 0; act < 3; act++)
            {
                actIndexField.SetValue(state, act);
                Check("act " + (act + 1), model.IsAllowed(state), act == 1);
            }
            actIndexField.SetValue(state, 1);
            state.AddVisitedEvent(model);
            Check("visited event excluded", model.IsAllowed(state), false);
        }
        finally { actIndexField.SetValue(state, originalAct); }
        foreach (string id in Ids)
        {
            var card = CardCatalog.Model(id);
            Check(id + " event rarity", card.Rarity, CardRarity.Event);
            Check(id + " excluded from rewards", CardCatalog.IsRewardCard(card), false);
            Check(id + " direct partners", CardCatalog.Groups.Single(group => group.MainCardId == id).GrantedCardIds.Length, 8);
        }
    }

    private static async Task Effects(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var enemies = state.GetOpponentsOf(player.Creature).ToArray();
        double previousSpeed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 12;
        try
        {
            foreach (string id in Ids)
            foreach (bool upgraded in new[] { false, true })
            {
                string label = id + (upgraded ? "+" : "");
                try
                {
                    await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
                    foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
                    foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
                    {
                        ArrowCombat.Cancel(card);
                        await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
                    }
                    await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
                    foreach (var enemy in enemies)
                    {
                        foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
                        await CreatureCmd.SetMaxHp(enemy, 1000000);
                        await CreatureCmd.SetCurrentHp(enemy, 1000000);
                        await TestGameApi.LoseBlock(context, enemy, enemy.Block, null);
                    }
                    player.PlayerCombatState!.Energy = 10;
                    async Task<CardModel> Card(string designId, PileType pile, bool upgrade = false)
                    {
                        using var single = KnowledgeCombat.SingleCard();
                        var card = CardCatalog.Create(designId, player);
                        if (upgrade) CardCmd.Upgrade(card);
                        await CardPileCmd.Add(card, pile,
                            CardPilePosition.Bottom, skipVisuals: true);
                        return card;
                    }
                    var source = await Card(id, PileType.Hand, upgraded);
                    var sameName = await Card(id, PileType.Draw);
                    foreach (string linked in Ids.Where(other => other != id).Take(5)) await Card(linked, PileType.Draw);
                    var ordinary = await Card("JB01", PileType.Discard);
                    CardModel? waiting = null;
                    if (id == "JK362")
                    {
                        waiting = await Card("JA38", PileType.Hand);
                        waiting.DynamicVars["Turns"].BaseValue = 5;
                        await TestGameApi.PaidPlay(context, waiting, enemies[0]);
                    }
                    int beforeEnergy = player.PlayerCombatState.Energy;
                    await TestGameApi.PaidPlay(context, source, enemies[0]);
                    Check(label + " countdown", ArrowCombat.Find(source)?.Remaining, 1);
                    Check(label + " cost", player.PlayerCombatState.Energy, beforeEnergy);
                    await ArrowCombat.Accelerate(context, ArrowCombat.Find(source)!, 100);
                    int draw = id is "JK359" or "JK365" ? (upgraded ? 4 : 3) : 2;
                    Check(label + " draw count", player.PlayerCombatState.Hand.Cards.Count, draw);
                    Check(label + " same-name draw", sameName.Pile?.Type, PileType.Hand);
                    Check(label + " directed draw does not reshuffle", ordinary.Pile?.Type, PileType.Discard);
                    if (id == "JK360") Check(label + " block", player.Creature.Block, upgraded ? 6 : 4);
                    if (id is "JK361" or "JK363" or "JK366")
                    {
                        int damage = id == "JK361" ? (upgraded ? 5 : 3) : id == "JK363" ? (upgraded ? 6 : 4) : (upgraded ? 7 : 5);
                        Check(label + " damage", 1000000m - enemies[0].CurrentHp, (decimal)damage);
                        if (id == "JK361") Check(label + " all targets", enemies.All(enemy => enemy.CurrentHp == 1000000 - damage), true);
                    }
                    if (id == "JK362") Check(label + " haste", ArrowCombat.Find(waiting!)?.Remaining, upgraded ? 3 : 4);
                    if (id == "JK364") Check(label + " energy", player.PlayerCombatState.Energy, beforeEnergy + 1);
                    if (id == "JK367") Check(label + " damage bonus", DamageModifiers.CaptureDamageModifierSnapshot(ordinary).DamageBonus, 2m);
                }
                catch (Exception error) { Check(label + " exception", error.ToString(), "none"); }
            }
        }
        finally { Godot.Engine.TimeScale = previousSpeed; }
    }

    private static void Check(string name, object? actual, object? expected)
    {
        Checks[name] = new { name, actual, expected, pass = Equals(actual, expected) };
        Directory.CreateDirectory(Path.GetDirectoryName(Report)!);
        File.WriteAllText(Report, JsonSerializer.Serialize(Checks.Values, new JsonSerializerOptions { WriteIndented = true }));
    }
}
