using System.Text.Json;
using BaseLib.Utils.Attributes;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace SteadyProgress;

[ModInitializer(nameof(Initialize))]
public static class Startup
{
    public static void Initialize()
    {
        ModHelper.SubscribeForCombatStateHooks("SteadyProgress", _ => [ModelDb.GetById<OrderObserver>(ModelDb.GetId<OrderObserver>())]);
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => RenderingServer.ForceDraw(false);
    }
}

[CustomID("JUNNA_STEADY_PROGRESS_OBSERVER")]
public sealed class OrderObserver : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;
    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (Probe.Player == card.Owner && CardCatalog.DesignId(card) == "JA119") Probe.Record("generate", card);
        return Task.CompletedTask;
    }
    public override Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        if (Probe.Player == card.Owner && fromHandDraw) Probe.Record("draw", card);
        return Task.CompletedTask;
    }
}

public static class Probe
{
    public static Player? Player;
    public static readonly List<object> Events = [];
    public static void Record(string kind, CardModel? card = null)
    {
        if (Player?.PlayerCombatState is not { } state) return;
        Events.Add(new { kind, turn = state.TurnNumber, id = card == null ? null : CardCatalog.DesignId(card),
            upgrade = card?.CurrentUpgradeLevel, generatedInHand = state.Hand.Cards.Count(c => CardCatalog.DesignId(c) == "JA119") });
        Save();
    }
    public static void Save()
    {
        if (Player?.PlayerCombatState is not { } state) return;
        object[] Cards(CardPile pile) => pile.Cards.Select(c => (object)new { id = CardCatalog.DesignId(c), upgrade = c.CurrentUpgradeLevel }).ToArray();
        File.WriteAllText(Path.Combine(System.Environment.GetEnvironmentVariable("JUNNA_STEADY_OUTPUT")!, "live.json"),
            JsonSerializer.Serialize(new { turn = state.TurnNumber, events = Events, hand = Cards(state.Hand),
                discard = Cards(state.DiscardPile), draw = Cards(state.DrawPile) }));
    }
}

public sealed class PrepareCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_steady";
    public override string Args => "prepare [upgrade] [stack] | inspect";
    public override string Description => "Prepare Steady Progress fixtures and observe native draw order.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.Contains("inspect")) { Probe.Save(); return new(true, "Snapshot saved"); }
        return new(Prepare(player, args), true, "Preparing Steady Progress fixture");
    }
    private static async Task Prepare(Player player, string[] args)
    {
        Probe.Player = null;
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
        await CreatureCmd.SetMaxHp(player.Creature, 1000);
        await CreatureCmd.SetCurrentHp(player.Creature, 1000);
        player.PlayerCombatState!.Energy = 3;
        CardModel Create(string id) => player.Creature.CombatState!.CreateCard(CardCatalog.Model(id), player);
        for (int i = 0; i < (args.Contains("stack") ? 2 : 1); i++)
        {
            var source = Create("JA144");
            if (args.Contains("upgrade")) source.UpgradeInternal();
            await CardPileCmd.AddGeneratedCardToCombat(source, PileType.Hand, player);
        }
        await CardPileCmd.Add(Enumerable.Range(0, 20).Select(_ => Create("JB02")).ToArray(),
            player.PlayerCombatState.DrawPile, CardPilePosition.Bottom, skipVisuals: true);
        Probe.Events.Clear(); Probe.Player = player; Probe.Record("prepared");
    }
}
