using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class KnowledgeCopyRewardCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_copy_reward";
    public override string Args => "prepare [upgrade] | inspect";
    public override string Description => "Observe actual JS19 depletion, queued reward and copied-card flight.";
    public override bool IsNetworked => false;
    internal static CopyRewardObservation? Active;
    internal static string Output => Path.GetFullPath("../../../artifacts/latest-card-pool/copy-reward", System.Environment.CurrentDirectory);
    internal static void Initialize()
    {
        var harmony = new Harmony("JunnaIntegration.KnowledgeCopy.Observe");
        harmony.Patch(AccessTools.Method(typeof(CombatRoom), "AddExtraReward"),
            prefix: new HarmonyMethod(typeof(CopyRewardObservation), nameof(CopyRewardObservation.RewardAdded)));
        harmony.Patch(AccessTools.Method(typeof(CardCmd), "PreviewInternal"),
            postfix: new HarmonyMethod(typeof(CopyRewardObservation), nameof(CopyRewardObservation.Preview)));
        harmony.Patch(AccessTools.Method(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create), [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)]),
            postfix: new HarmonyMethod(typeof(CopyRewardObservation), nameof(CopyRewardObservation.Fly)));
        harmony.Patch(AccessTools.Method(typeof(CardPile), nameof(CardPile.InvokeCardAddFinished)),
            postfix: new HarmonyMethod(typeof(CopyRewardObservation), nameof(CopyRewardObservation.AddFinished)));
        harmony.Patch(AccessTools.Method(typeof(KnowledgeProgress), nameof(KnowledgeProgress.CopySingleCards)),
            postfix: new HarmonyMethod(typeof(CopyRewardObservation), nameof(CopyRewardObservation.Copied)));
        long last = 0;
        ((SceneTree)Engine.GetMainLoop()).ProcessFrame += () =>
        {
            if (Active == null || System.Environment.TickCount64 - last < 150) return;
            last = System.Environment.TickCount64;
            Active.Save();
        };
    }
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "inspect") { Active?.Save(); return new(true, "Snapshot saved"); }
        return new(Prepare(player, args.Contains("upgrade")), true, "Preparing JS19 last-shine fixture");
    }
    private static async Task Prepare(Player player, bool upgraded)
    {
        Active = null;
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
        var context = new ThrowingPlayerChoiceContext();
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 1000); await CreatureCmd.SetCurrentHp(enemy, 1000);
            await TestGameApi.LoseBlock(context, enemy, enemy.Block, null);
        }
        player.PlayerCombatState.Energy = 20;
        using var single = KnowledgeCombat.SingleCard();
        var source = player.RunState.CreateCard(CardCatalog.Model("JS19"), player);
        if (upgraded) { source.UpgradeInternal(); source.FinalizeUpgradeInternal(); }
        Shine.SetState(source, 1, 9);
        await CardPileCmd.Add(source, PileType.Deck, skipVisuals: true);
        foreach (string id in new[] { "JK122", "JK49B" })
        {
            var target = player.RunState.CreateCard(CardCatalog.Model(id), player);
            if (id == "JK122") { target.UpgradeInternal(); target.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(target, PileType.Deck, skipVisuals: true);
        }
        var active = player.Creature.CombatState.CloneCard(source);
        active.DeckVersion = source;
        Shine.SetState(active, 1, 9);
        await CardPileCmd.Add(active, PileType.Hand, skipVisuals: false);
        var finisher = CardCatalog.Create("JK275", player);
        finisher.DynamicVars.Damage.BaseValue = 2000;
        await CardPileCmd.Add(finisher, PileType.Hand, skipVisuals: false);
        Active = new(player, source, upgraded);
        Active.Save();
    }
}

internal sealed class CopyRewardObservation(Player player, CardModel source, bool upgraded)
{
    private readonly HashSet<CardModel> _before = player.Deck.Cards.ToHashSet();
    private readonly List<Reward> _rewards = [];
    private readonly List<NCardFlyVfx> _flies = [];
    private int _previews, _finished;
    private int _copiesReturned, _completedAtReturn;
    private bool IsCopy(CardModel card) => card.Owner == player && !_before.Contains(card)
        && CardCatalog.DesignId(card) is "JK122" or "JK49B";
    public static void RewardAdded(object[] __args)
    { if (KnowledgeCopyRewardCommand.Active is { } a) a._rewards.AddRange(__args.OfType<KnowledgeCopyReward>()); }
    public static void Preview(CardModel card, TaskCompletionSource? __result)
    { if (__result != null && KnowledgeCopyRewardCommand.Active is { } a && a.IsCopy(card)) a._previews++; }
    public static void Fly(NCard card, PileType pileType, bool isAddingToPile, NCardFlyVfx? __result)
    {
        if (__result != null && pileType == PileType.Deck && isAddingToPile
            && KnowledgeCopyRewardCommand.Active is { } a && card.Model is { } model && a.IsCopy(model)) a._flies.Add(__result);
    }
    public static void AddFinished(CardPile __instance)
    { if (KnowledgeCopyRewardCommand.Active is { } a && __instance == a.Player.Deck) a._finished++; }
    private Player Player => player;
    public static void Copied(ref Task __result)
    { if (KnowledgeCopyRewardCommand.Active is { } a) __result = After(__result, a); }
    private static async Task After(Task task, CopyRewardObservation a)
    {
        await task;
        a._copiesReturned++;
        a._completedAtReturn = a._flies.Count(f => f.SwooshAwayCompletion?.Task.IsCompleted == true);
        a.Save();
    }
    public void Save()
    {
        var copies = player.Deck.Cards.Where(IsCopy).ToArray();
        var doc = new
        {
            upgraded, expected = upgraded ? 2 : 1, sourceDepleted = source.HasBeenRemovedFromState,
            pendingPower = player.Creature.Powers.Where(p => p.Id.Entry == "JUNNA_KNOWLEDGE_COPY_REWARD_POWER").Sum(p => p.Amount),
            rewardMaximums = _rewards.OfType<KnowledgeCopyReward>().Select(r => r.Maximum),
            copyCount = copies.Length, copies = copies.Select(c => new { id = CardCatalog.DesignId(c), c.IsUpgraded, pile = c.Pile?.Type.ToString() }),
            previews = _previews, flies = _flies.Count, completedFlies = _flies.Count(f => f.SwooshAwayCompletion?.Task.IsCompleted == true),
            copyReturns = _copiesReturned, completedAtReturn = _completedAtReturn, addFinished = _finished
        };
        Directory.CreateDirectory(KnowledgeCopyRewardCommand.Output);
        File.WriteAllText(Path.Combine(KnowledgeCopyRewardCommand.Output, "live.json"), JsonSerializer.Serialize(doc));
    }
}
