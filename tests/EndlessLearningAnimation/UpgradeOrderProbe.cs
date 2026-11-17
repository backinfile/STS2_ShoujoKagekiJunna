using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.RestSite;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace EndlessLearningAnimation;

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    public static Player? Player;
    public static CardModel? Source;
    private static readonly HashSet<Node> Active = [];
    private static readonly List<object> Events = [];
    private static readonly List<object> Checks = [];
    private static int _gains;
    private static int _previews, _nativeSmithCalls, _manualSmithCalls;
    private static bool _sparksFinished = true;
    public static void Initialize()
    {
        new Harmony("Junna.EndlessLearningAnimation.Observe").PatchAll(typeof(Probe).Assembly);

    }
    public static void Reset(Player player, CardModel source)
    {
        Player = player; Source = source; Events.Clear(); Checks.Clear(); _gains = 0; _previews = 0; _nativeSmithCalls = 0; _manualSmithCalls = 0; _sparksFinished = true;
        Record("prepared");
    }
    private static void Record(string kind)
    {
        Events.Add(new { kind, ms = System.Environment.TickCount64, activeUpgrades = Active.Count });
        Save();
    }
    private static void Save()
    {
        string directory = System.Environment.GetEnvironmentVariable("JUNNA_ENDLESS_OUTPUT")!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "live.json"), JsonSerializer.Serialize(new {
            events = Events, checks = Checks, gains = _gains, previews = _previews, nativeSmithCalls = _nativeSmithCalls,
            manualSmithCalls = _manualSmithCalls, level = Source?.CurrentUpgradeLevel,
            deckNewKnowledge = Player?.Deck.Cards.Count(c => CardCatalog.DesignId(c) == "JK82B"),
            drawNewKnowledge = Player?.PlayerCombatState?.DrawPile.Cards.Count(c => CardCatalog.DesignId(c) == "JK82B")
        }));
    }
    [HarmonyPatch(typeof(NCardUpgradeVfx), nameof(NCardUpgradeVfx.Create))]
    private static class UpgradeAnimation
    {
        private static void Postfix(CardModel card, NCardUpgradeVfx? __result)
        {
            if (card != Source || __result == null) return;
            var vfx = __result;
            Active.Add(vfx); Record("upgrade-start");
            vfx.TreeExited += () => { Active.Remove(vfx); Record("upgrade-exited"); };
        }
    }
    [HarmonyPatch(typeof(NCardSmithVfx), nameof(NCardSmithVfx.Create), [typeof(IEnumerable<CardModel>), typeof(bool)])]
    private static class SmithAnimation
    {
        private static void Postfix(IEnumerable<CardModel> cards, NCardSmithVfx? __result)
        {
            if (!cards.Contains(Source!) || __result == null) return;
            var nativeStateMachine = typeof(SmithRestSiteOption).GetMethod(nameof(SmithRestSiteOption.DoLocalPostSelectVfx))!
                .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            bool native = new StackTrace().GetFrames().Any(frame => frame.GetMethod()?.DeclaringType == nativeStateMachine);
            if (native) _nativeSmithCalls++; else _manualSmithCalls++;
            Checks.Add(new { name = "smith effect is created by the native game method", pass = native });
            var vfx = __result;
            Active.Add(vfx); Record("upgrade-start");
            var particles = vfx.FindChildren("*", "GPUParticles2D", true, false).OfType<GpuParticles2D>().Where(p => p.OneShot).ToArray();
            _sparksFinished = particles.Length == 0;
            int remaining = particles.Length;
            foreach (var particle in particles)
            {
                bool done = false;
                void Finish()
                {
                    if (done) return;
                    done = true;
                    if (--remaining == 0) { _sparksFinished = true; Record("upgrade-sparks-finished"); }
                }
                particle.Finished += Finish;
                vfx.TreeExited += Finish;
            }
            vfx.TreeExited += () => { Active.Remove(vfx); Record("upgrade-exited"); };
        }
    }
    [HarmonyPatch(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create),
        [typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard), typeof(PileType), typeof(bool), typeof(string)])]
    private static class UpgradeFlight
    {
        private static void Postfix(MegaCrit.Sts2.Core.Nodes.Cards.NCard card, bool isAddingToPile)
        {
            if (card.Model != Source || isAddingToPile || Active.Count == 0) return;
            Active.Clear(); Record("upgrade-finished");
        }
    }
    [HarmonyPatch]
    private static class Acquisition
    {
        private static MethodBase TargetMethod() => typeof(CardPileCmd).GetMethods().Single(m => m.Name == "Add"
            && m.GetParameters()[0].ParameterType == typeof(IEnumerable<CardModel>)
            && m.GetParameters()[1].ParameterType == typeof(CardPile));
        private static void Prefix(IEnumerable<CardModel> cards)
        {
            foreach (var card in cards.Where(c => c.Owner == Player && c.Pile == null && CardCatalog.DesignId(c) == "JK82B"))
            {
                _gains++;
                Checks.Add(new { name = "one knowledge created by the upgrade settlement", pass = true });
                Record("knowledge-acquired");
            }
        }
    }
    [HarmonyPatch(typeof(CardCmd), "PreviewInternal")]
    private static class KnowledgePreview
    {
        private static void Postfix(CardModel card, bool isAddingCardsToPile)
        {
            if (!isAddingCardsToPile || card.Owner != Player || CardCatalog.DesignId(card) != "JK82B") return;
            _previews++;
            Checks.Add(new { name = "new knowledge preview follows the native upgrade effect and sparks", pass = Active.Count == 0 && _sparksFinished });
            Record("knowledge-preview");
        }
    }
    public static async Task Prepare(Player player)
    {
        Source = null; Player = null;
        var card = player.RunState.CreateCard(CardCatalog.Model("JK82"), player);
        using (KnowledgeCombat.SingleCard()) await CardPileCmd.Add(card, PileType.Deck);
        Reset(player, card);
    }
    public static async Task Check(Player player, string mode)
    {
        if (mode == "combat")
        {
            var card = player.Creature.CombatState!.CreateCard(CardCatalog.Model("JK82"), player);
            using (KnowledgeCombat.SingleCard()) await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            Reset(player, card);
        }
        else await Prepare(player);
        Record("command-start");
        await CardUpgrades.Upgrade(Source!, mode == "none" ? CardPreviewStyle.None : CardPreviewStyle.HorizontalLayout);
        Record("command-completed");
    }
}

public sealed class UpgradeOrderCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_endless_animation";
    public override string Args => "prepare | check [deck|combat|none]";
    public override string Description => "Observe native learning upgrade and generated-card ordering.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Active run required");
        return new(args.FirstOrDefault() == "prepare" ? Probe.Prepare(player)
            : Probe.Check(player, args.ElementAtOrDefault(1) ?? "deck"), true, "Upgrade order check started");
    }
}
