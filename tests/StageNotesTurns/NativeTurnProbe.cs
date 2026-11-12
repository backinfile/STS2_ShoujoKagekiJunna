using System.Text.Json;
using BaseLib.Utils.Attributes;
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

namespace StageNotesTurns;

[ModInitializer(nameof(Initialize))]
public static class Startup
{
    public static void Initialize()
    {
        ModHelper.SubscribeForCombatStateHooks("StageNotesTurns", _ => [ModelDb.GetById<CycleObserver>(ModelDb.GetId<CycleObserver>())]);
        new Harmony("Junna.StageNotesTurns.Observe").Patch(AccessTools.Method(typeof(CardActions), nameof(CardActions.Choose)),
            prefix: new HarmonyMethod(typeof(ObserveChoice), nameof(ObserveChoice.Prefix)),
            postfix: new HarmonyMethod(typeof(ObserveChoice), nameof(ObserveChoice.Postfix)));
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        long last = 0;
        tree.ProcessFrame += () =>
        {
            RenderingServer.ForceDraw(false);
            if (CycleCase.Active is not { } active || System.Environment.TickCount64 - last < 150) return;
            last = System.Environment.TickCount64;
            active.Save();
        };
    }
}

[CustomID("JUNNA_REAL_TURN_CYCLE_OBSERVER")]
public sealed class CycleObserver : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;
    public override Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        if (CycleCase.Active is { } active && card.Owner == active.Player)
            active.Draws.Add(new Draw(card, card.Owner.PlayerCombatState!.TurnNumber, fromHandDraw));
        return Task.CompletedTask;
    }
}

public sealed record Draw(CardModel Card, int Turn, bool FromHandDraw);
public sealed class Pick(int turn, int instance, CardModel[] candidates)
{
    public int Turn { get; } = turn;
    public int Instance { get; } = instance;
    public CardModel[] Candidates { get; } = candidates;
    public CardModel[] Selected { get; set; } = [];
    public bool Resolved { get; set; }
}

public static class ObserveChoice
{
    public static void Prefix(CardModel source, IEnumerable<CardModel> candidates, out Pick? __state)
    {
        __state = null;
        if (CycleCase.Active is not { } active || source.Owner != active.Player || CardCatalog.DesignId(source) != "U_CARD_010") return;
        if (!active.Instances.TryGetValue(source, out int index)) active.Instances[source] = index = active.Instances.Count;
        __state = new(active.Player.PlayerCombatState!.TurnNumber, index, candidates.ToArray());
        active.Picks.Add(__state);
    }
    public static void Postfix(ref Task<CardModel[]> __result, Pick? __state)
    {
        if (__state != null) __result = Complete(__result, __state);
    }
    private static async Task<CardModel[]> Complete(Task<CardModel[]> task, Pick pick)
    {
        var selected = await task; pick.Selected = selected; pick.Resolved = true; return selected;
    }
}

public sealed class CycleCase(Player player, string mode, bool upgraded, string caseId)
{
    public static CycleCase? Active;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public Player Player { get; } = player;
    public string Mode { get; } = mode;
    public bool Upgraded { get; } = upgraded;
    public int BaseTurn { get; } = player.PlayerCombatState!.TurnNumber;
    public Dictionary<CardModel, string> Roles { get; } = [];
    public Dictionary<CardModel, (int Upgrade, int? Shine)> Initial { get; } = [];
    public Dictionary<CardModel, int> Instances { get; } = [];
    public List<Pick> Picks { get; } = [];
    public List<Draw> Draws { get; } = [];
    private readonly Dictionary<CardModel, int> _ids = [];
    private int Id(CardModel card) { if (!_ids.TryGetValue(card, out int id)) _ids[card] = id = _ids.Count; return id; }
    private object Card(CardModel card) => new { instance = Id(card), id = CardCatalog.DesignId(card), role = Roles.GetValueOrDefault(card, "unexpected"), pile = card.Pile?.Type.ToString(), upgrade = card.CurrentUpgradeLevel, shine = Shine.GetState(card)?.Remaining, initialUpgrade = Initial.GetValueOrDefault(card).Upgrade, initialShine = Initial.GetValueOrDefault(card).Shine };
    private static string Output => System.Environment.GetEnvironmentVariable("JUNNA_CYCLE_OUTPUT")!;
    public void Save()
    {
        if (Player.PlayerCombatState is not { } state) return;
        var document = new { caseId, mode = Mode, upgraded = Upgraded, baseTurn = BaseTurn, turn = state.TurnNumber,
            hand = state.Hand.Cards.Select(Card).ToArray(), draw = state.DrawPile.Cards.Select(Card).ToArray(),
            picks = Picks.Select(pick => new { turn = pick.Turn, instance = pick.Instance, resolved = pick.Resolved,
                candidates = pick.Candidates.Select(Card).ToArray(), selected = pick.Selected.Select(Card).ToArray() }).ToArray(),
            draws = Draws.Select(draw => new { turn = draw.Turn, fromHandDraw = draw.FromHandDraw, card = Card(draw.Card) }).ToArray(),
            powerCount = Player.Creature.Powers.OfType<CatalogPower>().Count(power => power.DesignId == "U_CARD_010") };
        File.WriteAllText(Path.Combine(Output, "live.json"), JsonSerializer.Serialize(document, JsonOptions));
    }
}

public sealed class CycleCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_real_cycle";
    public override string Args => "prepare <normal|missing|capacity|staggered> [upgrade] | inspect";
    public override string Description => "Prepare fixtures and observe native turn transitions; never advance turn counters or call the power hook.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "inspect") { CycleCase.Active?.Save(); return new(true, "Read-only cycle snapshot saved"); }
        return new(Prepare(player, args.ElementAtOrDefault(1) ?? "normal", args.Contains("upgrade"), args.FirstOrDefault(arg => arg.StartsWith("case="))?[5..] ?? Guid.NewGuid().ToString("N")), true, "Preparing native-turn cycle fixture");
    }
    private static async Task Prepare(Player player, string mode, bool upgraded, string caseId)
    {
        CycleCase.Active = null;
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
        var context = new ThrowingPlayerChoiceContext();
        await CreatureCmd.SetMaxHp(player.Creature, 1000); await CreatureCmd.SetCurrentHp(player.Creature, 1000);
        await JunnaIntegration.TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        { foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power); }
        player.PlayerCombatState!.Energy = 3;
        var active = new CycleCase(player, mode, upgraded, caseId);
        CardModel Create(string id, string role, bool upgrade = false)
        {
            var card = player.Creature.CombatState!.CreateCard(CardCatalog.Model(id), player);
            if (upgrade) card.UpgradeInternal();
            if (role == "shine") Shine.SetState(card, 2, 6);
            active.Roles[card] = role; active.Initial[card] = (card.CurrentUpgradeLevel, Shine.GetState(card)?.Remaining);
            return card;
        }
        var source = Create("U_CARD_010", "power", upgraded);
        await CardPileCmd.AddGeneratedCardToCombat(source, PileType.Hand, player);
        var draw = new List<CardModel>();
        for (int i = 0; i < 60; i++)
            draw.Add(mode == "staggered" && i == 0 ? Create("U_CARD_010", "secondPower", true) : Create("JB02", "filler"));
        foreach (var (id, role) in new[] { ("JK11", "knowledge"), ("JA38", "arrow"), ("JS35", "shine") })
        {
            if (mode == "missing" && role == "knowledge") continue;
            for (int i = 0; i < 6; i++) draw.Add(Create(id, role, i == 0));
        }
        await CardPileCmd.Add(draw, player.PlayerCombatState.DrawPile, CardPilePosition.Bottom, skipVisuals: true);
        if (mode == "capacity")
        {
            await JunnaIntegration.TestGameApi.PaidPlay(context, source);
            for (int i = 0; i < 10; i++)
            {
                var filler = Create("JB02", "retainedFiller"); filler.AddKeyword(CardKeyword.Retain); filler.EnergyCost.SetThisCombat(0);
                await CardPileCmd.AddGeneratedCardToCombat(filler, PileType.Hand, player);
            }
        }
        CycleCase.Active = active;
        active.Save();
    }
}
