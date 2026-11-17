using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public static class StudyAnimationObservation
{
    private static Player? _player;
    private static string _scenario = "";
    private static int _expected, _previews, _settlements;
    private static CardModel[] _original = [];
    private static readonly List<(CardModel Card, CardPilePosition Position)> Generated = [];
    private static readonly List<NCardFlyVfx> Flies = [];
    private static object? _insertion;
    private static readonly System.Diagnostics.Stopwatch SettlementTimer = new();
    private static readonly string Output = System.Environment.GetEnvironmentVariable("JUNNA_STUDY_OUTPUT")
        ?? Path.GetFullPath("../../../out/tmp/study-animation/observation.json", System.Environment.CurrentDirectory);

    public static void Initialize()
    {
        var patch = new Harmony("Junna.StudyAnimation.Observe");
        patch.Patch(AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat)),
            prefix: new HarmonyMethod(typeof(StudyAnimationObservation), nameof(Generate)));
        patch.Patch(AccessTools.Method(typeof(CardCmd), "PreviewInternal"),
            postfix: new HarmonyMethod(typeof(StudyAnimationObservation), nameof(Preview)));
        patch.Patch(AccessTools.Method(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create), [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)]),
            postfix: new HarmonyMethod(typeof(StudyAnimationObservation), nameof(Fly)));
        patch.Patch(AccessTools.Method(typeof(CatalogPower), nameof(CatalogPower.BeforeSideTurnEnd)),
            prefix: new HarmonyMethod(typeof(StudyAnimationObservation), nameof(Settling)),
            postfix: new HarmonyMethod(typeof(StudyAnimationObservation), nameof(Settled)));
        long previous = 0;
        ((SceneTree)Engine.GetMainLoop()).ProcessFrame += () =>
        {
            if (_player == null || System.Environment.TickCount64 - previous < 100) return;
            previous = System.Environment.TickCount64;
            Save();
        };
    }

    private static bool Matches(CardModel card) => _player != null && card.Owner == _player && CardCatalog.DesignId(card) == "B";
    public static void Generate(CardModel card, PileType newPileType, CardPilePosition position)
    { if (Matches(card) && newPileType == PileType.Draw) Generated.Add((card, position)); }
    public static void Preview(CardModel card, bool isAddingCardsToPile, TaskCompletionSource? __result)
    { if (Matches(card) && isAddingCardsToPile && __result != null) _previews++; }
    public static void Fly(NCard card, PileType pileType, bool isAddingToPile, NCardFlyVfx? __result)
    { if (card.Model is { } model && Matches(model) && pileType == PileType.Draw && isAddingToPile && __result != null) Flies.Add(__result); }
    public static void Settling(CatalogPower __instance, CombatSide side, IEnumerable<Creature> participants)
    { if (_player != null && __instance.Owner == _player.Creature && __instance.DesignId == "A" && side == CombatSide.Player && participants.Contains(_player.Creature)) SettlementTimer.Restart(); }
    public static void Settled(CatalogPower __instance, CombatSide side, IEnumerable<Creature> participants, ref Task __result)
    { if (_player != null && __instance.Owner == _player.Creature && __instance.DesignId == "A" && side == CombatSide.Player && participants.Contains(_player.Creature)) __result = Complete(__result); }
    private static async Task Complete(Task task)
    {
        await task;
        SettlementTimer.Stop();
        _settlements++;
        var draw = _player!.PlayerCombatState!.DrawPile.Cards.ToArray();
        _insertion = new { drawCount = draw.Length, originalCount = _original.Length,
            originalOrderPreserved = draw.Where(_original.Contains).SequenceEqual(_original),
            generated = Generated.Select(c => new { id = CardCatalog.DesignId(c.Card), upgraded = c.Card.IsUpgraded,
                position = c.Position.ToString(), index = Array.IndexOf(draw, c.Card), pile = c.Card.Pile?.Type.ToString() }).ToArray() };
        Save();
    }
    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        File.WriteAllText(Output, JsonSerializer.Serialize(new { scenario = _scenario, expected = _expected,
            settlementDurationMs = SettlementTimer.Elapsed.TotalMilliseconds, settlements = _settlements, previews = _previews, flies = Flies.Count,
            completedFlies = Flies.Count(fly => fly.SwooshAwayCompletion?.Task.IsCompletedSuccessfully == true), insertion = _insertion },
            new JsonSerializerOptions { WriteIndented = true }));
    }
    public static async Task Prepare(Player player, string scenario)
    {
        _player = null;
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        player.PlayerCombatState.Energy = 9;
        await CreatureCmd.SetMaxHp(player.Creature, 1000);
        await CreatureCmd.SetCurrentHp(player.Creature, 1000);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 1000); await CreatureCmd.SetCurrentHp(enemy, 1000); }
        for (int i = 0; i < (scenario == "empty" ? 0 : 8); i++)
            await Card(player, "JB01", PileType.Draw, false);
        bool upgraded = scenario is "upgraded" or "stacked";
        await Card(player, "A", PileType.Hand, upgraded);
        if (scenario == "stacked") await Card(player, "A", PileType.Hand, false);
        _scenario = scenario;
        _expected = (upgraded ? 2 : 1) + (scenario == "stacked" ? 1 : 0);
        _original = player.PlayerCombatState.DrawPile.Cards.ToArray();
        _previews = _settlements = 0;
        _insertion = null;
        Generated.Clear(); Flies.Clear();
        _player = player;
        Save();
    }
    private static async Task Card(Player player, string id, PileType pile, bool upgraded)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, skipVisuals: false);
    }
}

public sealed class StudyAnimationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_study_animation";
    public override string Args => "base | upgraded | stacked | empty";
    public override string Description => "Prepare real-turn insight animation checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        string scenario = args.FirstOrDefault() ?? "base";
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (scenario is not ("base" or "upgraded" or "stacked" or "empty")) return new(false, "Unknown scenario");
        return new(StudyAnimationObservation.Prepare(player, scenario), true, "Preparing " + scenario);
    }
}