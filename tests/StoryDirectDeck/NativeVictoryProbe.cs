using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace StoryDirectDeck;

[ModInitializer(nameof(Initialize))]
public static class Startup
{
    public static void Initialize()
    {
        var harmony = new Harmony("Junna.StoryDirectDeck.Observe");
        harmony.Patch(AccessTools.Method(typeof(KnowledgeProgress), nameof(KnowledgeProgress.WonCombat)),
            postfix: new HarmonyMethod(typeof(StoryObservation), nameof(StoryObservation.AfterVictory)));
        harmony.Patch(AccessTools.Method(typeof(CombatRoom), "AddExtraReward"),
            prefix: new HarmonyMethod(typeof(StoryObservation), nameof(StoryObservation.ExtraReward)));
        foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(CardReward)))
            harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(StoryObservation), nameof(StoryObservation.CardRewardCreated)));
        harmony.Patch(AccessTools.Method(typeof(CardCmd), "PreviewInternal"),
            postfix: new HarmonyMethod(typeof(StoryObservation), nameof(StoryObservation.PreviewCreated)));
        harmony.Patch(AccessTools.Method(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create), [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)]),
            postfix: new HarmonyMethod(typeof(StoryObservation), nameof(StoryObservation.FlyCreated)));
        harmony.Patch(AccessTools.Method(typeof(CardPile), nameof(CardPile.InvokeCardAddFinished)),
            postfix: new HarmonyMethod(typeof(StoryObservation), nameof(StoryObservation.PileAddFinished)));
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        long last = 0;
        tree.ProcessFrame += () =>
        {
            RenderingServer.ForceDraw(false);
            if (StoryCase.Active is not { } active || System.Environment.TickCount64 - last < 150) return;
            last = System.Environment.TickCount64;
            active.Save();
        };
    }
}

public static class StoryObservation
{
    public static void AfterVictory(ref Task __result)
    {
        if (StoryCase.Active is { } active) __result = Complete(__result, active);
    }
    private static async Task Complete(Task task, StoryCase active)
    {
        await task;
        active.CompletedFliesAtVictory = active.Flies.Count(fly => fly.SwooshAwayCompletion?.Task.IsCompleted == true);
        active.VictorySettlements++;
        active.Save();
    }
    public static void ExtraReward(object[] __args)
    {
        if (StoryCase.Active is { } active)
            active.ExtraRewards.AddRange(__args.OfType<Reward>());
    }
    public static void PreviewCreated(CardModel card, TaskCompletionSource? __result)
    {
        if (__result != null && StoryCase.Active is { } active && card.Owner == active.Player && CardCatalog.DesignId(card) == "U_CARD_008B")
            active.Previews++;
    }
    public static void FlyCreated(NCard card, PileType pileType, bool isAddingToPile, NCardFlyVfx? __result)
    {
        if (__result != null && isAddingToPile && pileType == PileType.Deck && StoryCase.Active is { } active
            && card.Model is { } model && model.Owner == active.Player && CardCatalog.DesignId(model) == "U_CARD_008B")
            active.Flies.Add(__result);
    }
    public static void PileAddFinished(CardPile __instance)
    {
        if (StoryCase.Active is { } active && __instance == active.Player.Deck) active.PileAddFinished++;
    }
    public static void CardRewardCreated(CardReward __instance) => StoryCase.Active?.CardRewards.Add(__instance);
}

public sealed class StoryCase(Player player, string caseId, int expected, bool upgraded)
{
    public static StoryCase? Active;
    public Player Player { get; } = player;
    public string CaseId { get; } = caseId;
    public int Expected { get; } = expected;
    public bool Upgraded { get; } = upgraded;
    public CardModel[] Before { get; } = player.Deck.Cards.ToArray();
    public int VictorySettlements { get; set; }
    public int Previews { get; set; }
    public int PileAddFinished { get; set; }
    public List<NCardFlyVfx> Flies { get; } = [];
    public int CompletedFliesAtVictory { get; set; }
    public List<Reward> ExtraRewards { get; } = [];
    public HashSet<CardReward> CardRewards { get; } = [];
    public void Save()
    {
        object Card(CardModel card) => new { id = CardCatalog.DesignId(card), pile = card.Pile?.Type.ToString(),
            upgrade = card.CurrentUpgradeLevel, shine = Shine.GetState(card)?.Remaining,
            maxShine = Shine.GetState(card)?.Maximum, temporary = Shine.GetState(card)?.Temporary };
        var added = Player.Deck.Cards.Except(Before).ToArray();
        var document = new { caseId = CaseId, expected = Expected, upgraded = Upgraded, victorySettlements = VictorySettlements,
            pileAddFinished = PileAddFinished, previews = Previews, addingFlies = Flies.Count, completedFlies = Flies.Count(fly => fly.SwooshAwayCompletion?.Task.IsCompleted == true),
            completedFliesAtVictory = CompletedFliesAtVictory,
            beforeDeckCount = Before.Length, deckCount = Player.Deck.Cards.Count,
            beforeStoryCount = Before.Count(card => CardCatalog.DesignId(card) == "U_CARD_008B"),
            storyCount = Player.Deck.Cards.Count(card => CardCatalog.DesignId(card) == "U_CARD_008B"),
            added = added.Select(Card).ToArray(), distinctAdded = added.Distinct().Count(),
            originalDeckPreserved = Before.All(Player.Deck.Cards.Contains),
            extraRewards = ExtraRewards.Select(reward => reward.GetType().Name).ToArray(),
            rewardCards = CardRewards.SelectMany(reward => reward.Cards).Select(Card).ToArray(),
            hand = Player.PlayerCombatState?.Hand.Cards.Select(Card).ToArray() };
        var path = System.Environment.GetEnvironmentVariable("JUNNA_STORY_OUTPUT")!;
        File.WriteAllText(Path.Combine(path, "live.json"), JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class StoryCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_story_deck";
    public override string Args => "prepare <count> <base|upgrade> <caseId>";
    public override string Description => "Prepare story fixtures; victory must use native card play.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null || args.Length != 4 || args[0] != "prepare" || !int.TryParse(args[1], out int count))
            return new(false, "Expected active combat and prepare count base|upgrade caseId");
        return new(Prepare(player, count, args[2] == "upgrade", args[3]), true, "Preparing story fixture");
    }
    private static async Task Prepare(Player player, int count, bool upgraded, string caseId)
    {
        StoryCase.Active = null;
        KnowledgeProgress.ResetCombat();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card); }
        player.PlayerCombatState!.Energy = 10;
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await JunnaIntegration.TestGameApi.LoseBlock(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(), enemy, enemy.Block, null);
            await CreatureCmd.SetCurrentHp(enemy, 1);
        }
        for (int i = 0; i < count; i++)
        {
            var source = player.Creature.CombatState!.CreateCard(CardCatalog.Model("U_CARD_008"), player);
            if (upgraded) { source.UpgradeInternal(); source.FinalizeUpgradeInternal(); }
            await CardPileCmd.AddGeneratedCardToCombat(source, PileType.Hand, player);
        }
        var finisher = player.Creature.CombatState!.CreateCard(CardCatalog.Model("JK275"), player);
        await CardPileCmd.AddGeneratedCardToCombat(finisher, PileType.Hand, player);
        StoryCase.Active = new(player, caseId, count, upgraded);
        StoryCase.Active.Save();
    }
}
