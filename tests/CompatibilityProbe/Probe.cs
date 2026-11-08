using System.Text.Json;
using BaseLib.Abstracts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaCompatibilityProbe;

[ModInitializer(nameof(Initialize))]
public static class Startup
{
    public static void Initialize()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => RenderingServer.ForceDraw(false);
    }
}

public sealed class CompatibilityCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_compatibility";
    public override string Args => "check";
    public override string Description => "Check the reported custom-card preview and patch boundaries.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        var checks = new List<object>();
        bool passed = true;
        void Check(string name, Func<bool> test)
        {
            try { bool pass = test(); passed &= pass; checks.Add(new { name, pass }); }
            catch (Exception error) { passed = false; checks.Add(new { name, pass = false, error = error.ToString() }); }
        }
        Check("arrow localization patch installed", () => Harmony.GetPatchInfo(AccessTools.Method(typeof(CardTypeExtensions), nameof(CardTypeExtensions.ToLocString)))?.Prefixes.Any(patch => patch.owner == "ShoujoKagekiJunna") == true);
        Check("custom arrow name", () => ArrowPile.ArrowType.ToLocString().GetFormattedText() == "箭矢");
        var state = player.Creature.CombatState!;
        var arrow = state.CreateCard(ModelDb.Card<JunnaFullDraw>(), player);
        var converted = state.CreateCard(ModelDb.Card<JunnaStrike>(), player);
        HandArrow.Adapt(converted);
        foreach (var (name, card) in new[] { ("native arrow", arrow), ("converted arrow", converted), ("ordinary attack", state.CreateCard(ModelDb.Card<JunnaStrike>(), player)), ("ordinary skill", state.CreateCard(ModelDb.Card<JunnaDefend>(), player)) })
        {
            Check(name + " NCard preview", () =>
            {
                var node = NCard.Create(card)!;
                var root = ((SceneTree)Engine.GetMainLoop()).Root;
                root.AddChild(node);
                try
                {
                    node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
                    return node.GetNode<Label>("%TypeLabel").Text == card.Type.ToLocString().GetFormattedText();
                }
                finally { root.RemoveChild(node); node.QueueFree(); }
            });
        }
        var assembly = typeof(HandArrow).Assembly;
        var acquire = assembly.GetType("ShoujoKagekiJunna.Combat.KnowledgeCombat+AcquireGroup", true)!;
        Check("knowledge acquisition target resolved", () => AccessTools.Method(acquire, "TargetMethod").Invoke(null, null) != null);
        return new(passed, JsonSerializer.Serialize(new { passed, arrow_type = (int)ArrowPile.ArrowType, checks }));
    }
}