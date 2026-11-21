using System.Text.Json;
using Godot;
using Environment = System.Environment;
using MegaCrit.Sts2.Core.Runs;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaAncientPortraitProbe;

public sealed class KnowledgeAcquisitionAnimationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_knowledge_acquisition_animation";
    public override string Args => "prepare native|chain|repeated | check | showcase";
    public override string Description => "Check that a knowledge acquisition previews and flies as one complete group.";
    public override bool IsNetworked => false;
    private const string PatchId = "Junna.KnowledgeAcquisitionAnimation.Observe";
    private static HashSet<CardModel>? _before;
    private static string _case = "";
    private static string[] _expected = [];
    private static readonly List<string[]> Batches = [];
    private static readonly List<(string Id, ulong Frame)> Flies = [];
    private static readonly List<object> Results = [];
    private static string Output => Path.GetFullPath("../../../out/tmp/knowledge-presentation", Environment.CurrentDirectory);
    private static bool Matches(CardModel card) => _before != null && !_before.Contains(card) && _expected.Contains(card.Id.ToString());
    private static void Preview(IEnumerable<CardPileAddResult> results)
    {
        var ids = results.Where(result => result.success && Matches(result.cardAdded)).Select(result => result.cardAdded.Id.ToString()).ToArray();
        if (ids.Length > 0) Batches.Add(ids);
    }
    private static void Fly(NCard card, bool isAddingToPile, NCardFlyVfx? __result)
    {
        if (isAddingToPile && __result != null && card.Model is { } model && Matches(model))
            Flies.Add((model.Id.ToString(), Engine.GetProcessFrames()));
    }
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        if (args.Contains("prepare")) return new(Prepare(player, args), true, "Choose the knowledge reward, then run check");
        if (args.Contains("showcase")) return new(Showcase(player), true, "Prepared hand tips");
        return new(Check(player), true, "Checking acquisition animation");
    }
    private static Task Prepare(Player player, string[] args)
    {
        _before = null;
        new Harmony(PatchId).UnpatchAll(PatchId);
        _case = args.Contains("chain") ? "chain" : args.Contains("repeated") ? "repeated" : "native";
        var source = player.RunState.CreateCard(CardCatalog.Model(_case == "chain" ? "JK126" : _case == "repeated" ? "JK137" : "JK49"), player);
        var others = new[] { player.RunState.CreateCard(ModelDb.Card<StrikeIronclad>(), player),
            player.RunState.CreateCard(ModelDb.Card<DefendIronclad>(), player) };
        _expected = (_case == "chain" ? others.Prepend(source) : KnowledgePreviewCards.Create(source)).Select(card => card.Id.ToString()).Order().ToArray();
        _before = player.Deck.Cards.ToHashSet();
        Batches.Clear(); Flies.Clear();
        var patch = new Harmony(PatchId);
        patch.Patch(AccessTools.Method(typeof(CardCmd), nameof(CardCmd.PreviewCardPileAdd), [typeof(IReadOnlyList<CardPileAddResult>), typeof(float), typeof(MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle)]), prefix: new HarmonyMethod(typeof(KnowledgeAcquisitionAnimationCommand), nameof(Preview)));
        patch.Patch(AccessTools.Method(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create), [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)]),
            postfix: new HarmonyMethod(typeof(KnowledgeAcquisitionAnimationCommand), nameof(Fly)));
        var reward = new CardReward(others.Prepend(source), CardCreationSource.Encounter, player,
            new CardCreationOptions([], CardCreationSource.Encounter, CardRarityOddsType.Uniform));
        _ = reward.SelectUnsynchronized();
        return Task.CompletedTask;
    }
    private static async Task Check(Player player)
    {
        long deadline = Environment.TickCount64 + 6000;
        while (Flies.Count < _expected.Length && Environment.TickCount64 < deadline) await Task.Delay(50);
        var gained = player.Deck.Cards.Where(Matches).Select(card => card.Id.ToString()).Order().ToArray();
        var checks = new Dictionary<string, bool>
        {
            ["exact_group_in_deck"] = gained.SequenceEqual(_expected),
            ["one_complete_preview_batch"] = Batches.Count == 1 && Batches[0].Order().SequenceEqual(_expected),
            ["one_flight_per_card"] = Flies.Select(fly => fly.Id).Order().SequenceEqual(_expected),
            ["simultaneous_flights"] = Flies.Count == _expected.Length && Flies.Max(fly => fly.Frame) - Flies.Min(fly => fly.Frame) <= 1,
            ["original_deck_order"] = player.Deck.Cards.Where(_before!.Contains).SequenceEqual(_before)
        };
        Results.Add(new { name = _case, pass = checks.Values.All(value => value), expected = _expected, gained, batches = Batches.ToArray(),
            flights = Flies.Select(fly => new { id = fly.Id, frame = fly.Frame }).ToArray(), checks });
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "acquisition-results.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
        _before = null;
        new Harmony(PatchId).UnpatchAll(PatchId);
    }
    private static async Task Showcase(Player player)
    {
        using var single = KnowledgeCombat.SingleCard();
        foreach (var old in player.PlayerCombatState!.Hand.Cards.ToArray()) await CardPileCmd.RemoveFromCombat(old);
        var card = CardCatalog.Create("JK49", player);
        await CardPileCmd.Add(card, PileType.Hand);
        await CardPileCmd.Add(CardCatalog.Create("JK137", player), PileType.Hand);
    }
}
