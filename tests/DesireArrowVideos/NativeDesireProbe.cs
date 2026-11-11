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
using MegaCrit.Sts2.Core.Modding;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace DesireArrowVideos;

[ModInitializer(nameof(Initialize))]
public static class Startup
{
    public static void Initialize()
    {
        var harmony = new Harmony("Junna.DesireArrowVideos.Observe");
        harmony.Patch(AccessTools.Method(typeof(CatalogPower), nameof(CatalogPower.AfterCardPlayed)),
            prefix: new HarmonyMethod(typeof(Observe), nameof(Observe.BeforePower)),
            postfix: new HarmonyMethod(typeof(Observe), nameof(Observe.AfterPower)));
        harmony.Patch(AccessTools.Method(typeof(ArrowCombat), nameof(ArrowCombat.Accelerate)),
            prefix: new HarmonyMethod(typeof(Observe), nameof(Observe.BeforeAccelerate)),
            postfix: new HarmonyMethod(typeof(Observe), nameof(Observe.AfterAccelerate)));
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        long last = 0;
        tree.ProcessFrame += () =>
        {
            RenderingServer.ForceDraw(false);
            if (Case.Active is not { } active || System.Environment.TickCount64 - last < 120) return;
            last = System.Environment.TickCount64; active.Save();
        };
    }
}

public sealed record Trigger(string Power, string Card, decimal Energy);
public sealed class Acceleration(ArrowDeployment arrow, string role, int before, int amount, Trigger? trigger, string[] candidates)
{
    public ArrowDeployment Arrow { get; } = arrow;
    public string Role { get; } = role;
    public int Before { get; } = before;
    public int Amount { get; } = amount;
    public Trigger? Trigger { get; } = trigger;
    public string[] Candidates { get; } = candidates;
    public int After { get; set; }
    public bool Done { get; set; }
}

public static class Observe
{
    private static readonly AsyncLocal<Trigger?> Current = new();
    public static void BeforePower(CatalogPower __instance, CardPlay play, out Trigger? __state)
    {
        __state = Current.Value;
        Current.Value = new(__instance.DesignId, CardCatalog.DesignId(play.Card) ?? play.Card.Id.Entry, play.Resources.EnergyValue);
    }
    public static void AfterPower(Trigger? __state) => Current.Value = __state;
    public static void BeforeAccelerate(ArrowDeployment arrow, int turns, out Acceleration? __state)
    {
        __state = null;
        if (Case.Active is not { } active || arrow.Card.Owner != active.Player) return;
        __state = new(arrow, active.Roles.GetValueOrDefault(arrow.Card, "unknown"), arrow.Remaining, turns, Current.Value,
            ArrowCombat.Deployed.Where(a => a.Card.Owner == active.Player && !(bool)AccessTools.Field(typeof(ArrowDeployment), "Resolving").GetValue(a)! && a.Remaining > 0)
                .Select(a => active.Roles.GetValueOrDefault(a.Card, "unknown")).ToArray());
        active.Events.Add(__state);
    }
    public static void AfterAccelerate(ref Task __result, Acceleration? __state)
    { if (__state != null) __result = Complete(__result, __state); }
    private static async Task Complete(Task task, Acceleration item)
    { await task; item.After = item.Arrow.Remaining; item.Done = true; }
}

public sealed class Case(Player player, string mode, string caseId)
{
    public static Case? Active;
    public Player Player { get; } = player;
    public Dictionary<CardModel, string> Roles { get; } = [];
    public List<Acceleration> Events { get; } = [];
    public void Save()
    {
        object Card(CardModel card) => new { role = Roles[card], id = CardCatalog.DesignId(card), pile = card.Pile?.Type.ToString(),
            remaining = ArrowCombat.Find(card)?.Remaining ?? 0, deployed = ArrowCombat.Find(card) != null,
            cost = CardActions.Cost(card), shine = Shine.GetState(card)?.Remaining };
        var state = new { mode, caseId, turn = Player.PlayerCombatState!.TurnNumber, energy = Player.PlayerCombatState.Energy,
            hand = Player.PlayerCombatState.Hand.Cards.Where(Roles.ContainsKey).Select(Card).ToArray(), cards = Roles.Keys.Select(Card).ToArray(),
            events = Events.Select(e => new { role = e.Role, before = e.Before, after = e.After, amount = e.Amount, done = e.Done,
                power = e.Trigger?.Power ?? "native", playedCard = e.Trigger?.Card, paidEnergy = e.Trigger?.Energy, candidates = e.Candidates }).ToArray(),
            enemies = Player.Creature.CombatState!.HittableEnemies.Select(c => new { name = c.Monster?.Id.Entry, hp = c.CurrentHp, block = c.Block }).ToArray() };
        File.WriteAllText(Path.Combine(System.Environment.GetEnvironmentVariable("JUNNA_DESIRE_OUTPUT")!, "live.json"),
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class FixtureCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_desire_video";
    public override string Args => "prepare <control|random|return|haste|double|discount|turns> <caseId>";
    public override string Description => "Prepare fixtures before recording; all observed acceleration is native.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null || args.Length != 3 || args[0] != "prepare") return new(false, "Active combat and prepare mode caseId required");
        return new(Prepare(player, args[1], args[2]), true, "Preparing desire video");
    }
    private static async Task Prepare(Player player, string mode, string caseId)
    {
        Case.Active = null;
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card); }
        var context = new ThrowingPlayerChoiceContext();
        await CreatureCmd.SetMaxHp(player.Creature, 200); await CreatureCmd.SetCurrentHp(player.Creature, 200);
        await JunnaIntegration.TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 2000); await CreatureCmd.SetCurrentHp(enemy, 2000);
            await JunnaIntegration.TestGameApi.LoseBlock(context, enemy, enemy.Block, null);
        }
        player.PlayerCombatState!.Energy = 20;
        var active = new Case(player, mode, caseId);
        async Task<CardModel> Add(string id, string role, PileType pile = PileType.Hand, bool upgrade = false)
        {
            var card = player.Creature.CombatState!.CreateCard(CardCatalog.Model(id), player);
            if (upgrade) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            active.Roles[card] = role;
            await CardPileCmd.AddGeneratedCardToCombat(card, pile, player, CardPilePosition.Bottom);
            return card;
        }
        async Task Play(string id, string role, bool upgrade = false)
        {
            var card = await Add(id, role, upgrade: upgrade);
            await CardActions.PaidPlay(context, card, card.TargetType == TargetType.AnyEnemy ? player.Creature.CombatState!.HittableEnemies.First() : null);
        }
        if (mode == "random")
        { await Play("JB03", "arrow_3"); await Play("JK37", "arrow_9"); await Play("JA38", "arrow_2"); }
        else if (mode is "return" or "double") await Play("JA38", "arrow_2");
        else if (mode == "turns") await Play("JK37", "arrow_9");
        else if (mode != "haste") await Play("JB03", "arrow_3");
        if (mode == "control") await Add("JK37C", "subject");
        else await Play("JK37C", "subject");
        if (mode == "return") await Play("JA22", "return_power");
        if (mode == "haste") { await Play("JA105", "haste_power"); await Add("JA38", "arrow_2"); }
        if (mode == "double") await Play("JK37C", "second_subject", true);
        if (mode == "control") await Add("JB02", "paid");
        if (mode == "discount") { await Add("JK32C", "weather"); await Add("JK32", "helm"); }
        else
        {
            int count = mode == "random" ? 3 : mode == "return" ? 2 : 1;
            for (int i = 0; i < count; i++) await Add("JS35", "zero_" + i);
        }
        if (mode == "turns")
        { await Add("JS35", "next_zero", PileType.Draw); for (int i = 0; i < 9; i++) await Add("JB02", "draw_" + i, PileType.Draw); }
        Case.Active = active; active.Save();
    }
}
