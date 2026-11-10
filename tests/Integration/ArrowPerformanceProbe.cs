using System.Diagnostics;
using System.Text.Json;
using BaseLib.Abstracts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;

namespace JunnaIntegration;

/// <summary>Targeted real-engine measurements and the effect-off ownership regression.</summary>
public sealed class ArrowPerformanceProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_perf";
    public override string Args => "prepare <count> <Full|Reduced|Off> | measure <label> | release | check | video";
    public override string Description => "Measure arrow HUD work in an isolated test game.";
    public override bool IsNetworked => false;
    private static readonly List<object> Results = [];
    private static readonly List<double> ProcessTimes = new(1024), DrawTimes = new(1024);
    private static long _processBytes, _drawBytes;
    private static bool _measuring;
    private static bool _installed;
    private static readonly string Output = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "..", "arrow-perf-results.json"));

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (!_installed)
        {
            var harmony = new Harmony("JunnaIntegration.ArrowPerformance");
            harmony.Patch(AccessTools.Method(typeof(ArrowHud), nameof(ArrowHud._Process)),
                prefix: new HarmonyMethod(typeof(ArrowPerformanceProbe), nameof(Begin)),
                postfix: new HarmonyMethod(typeof(ArrowPerformanceProbe), nameof(EndProcess)));
            var guides = typeof(ArrowHud).Assembly.GetType("ShoujoKagekiJunna.Combat.ArrowGuides") ?? typeof(ArrowHud);
            harmony.Patch(AccessTools.Method(guides, "_Draw"),
                prefix: new HarmonyMethod(typeof(ArrowPerformanceProbe), nameof(Begin)),
                postfix: new HarmonyMethod(typeof(ArrowPerformanceProbe), nameof(EndDraw)));
            _installed = true;
        }
        return args.FirstOrDefault() switch
        {
            "prepare" => new(Prepare(player, int.Parse(args[1]), Enum.Parse<CoreVfxLevel>(args[2])), true, "Preparing arrows"),
            "measure" => new(Measure(args[1]), true, "Measuring arrows"),
            "release" => new(Release(player), true, "Checking release ownership"),
            "check" => new(Check(player), true, "Checking snapshots and effect levels"),
            "video" => new(PrepareVideo(player), true, "Preparing normal card play"),
            _ => new(false, "Choose prepare, measure or release")
        };
    }

    private readonly record struct Stamp(long Ticks, long Bytes);
    private static void Begin(out Stamp __state) => __state = _measuring
        ? new(Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread()) : default;
    private static void EndProcess(Stamp __state)
    {
        if (__state == default) return;
        _processBytes += GC.GetAllocatedBytesForCurrentThread() - __state.Bytes;
        ProcessTimes.Add(Stopwatch.GetElapsedTime(__state.Ticks).TotalMilliseconds);
    }
    private static void EndDraw(Stamp __state)
    {
        if (__state == default) return;
        _drawBytes += GC.GetAllocatedBytesForCurrentThread() - __state.Bytes;
        DrawTimes.Add(Stopwatch.GetElapsedTime(__state.Ticks).TotalMilliseconds);
    }

    private static async Task Prepare(Player player, int count, CoreVfxLevel level)
    {
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = level });
        foreach (var arrow in ArrowCombat.Deployed.ToArray())
        {
            ArrowCombat.Cancel(arrow.Card);
            await CardPileCmd.Add(arrow.Card, PileType.Discard, skipVisuals: true);
        }
        var room = NCombatRoom.Instance!;
        if (room.GetNodeOrNull<ArrowHud>("JunnaArrows") is { } previous)
        {
            room.RemoveChild(previous);
            previous.QueueFree();
        }
        room.AddChild(new ArrowHud { Name = "JunnaArrows" });
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            await CreatureCmd.SetMaxHp(enemy, 10000);
            await CreatureCmd.SetCurrentHp(enemy, 10000);
        }
        var state = player.Creature.CombatState!;
        for (int i = 0; i < count; i++)
        {
            var card = state.CreateCard(ModelDb.Card<GroupArrowProbe>(), player);
            await CardPileCmd.Add(card, ArrowPile.PileId, skipVisuals: true);
            var play = new CardPlay { Card = card, Target = null,
#if STS2_BETA
                Player = player,
#endif
                ResultPile = ArrowPile.PileId, Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            ArrowCombat.Deploy(card, play, 3, (_, _) => Task.CompletedTask);
        }
        await Frames(3);
        Results.Add(new { kind = "prepared", count, level = level.ToString(), live_roots = Roots() });
        Save();
    }

    private static async Task Measure(string label)
    {
        await Frames(90);
        const int samples = 360;
        var frames = new double[samples];
        var draws = new double[samples];
        ProcessTimes.Clear(); DrawTimes.Clear();
        _processBytes = _drawBytes = 0;
        _measuring = true;
        long previous = Stopwatch.GetTimestamp();
        for (int i = 0; i < samples; i++)
        {
            await Frames(1);
            long now = Stopwatch.GetTimestamp();
            frames[i] = (now - previous) * 1000d / Stopwatch.Frequency;
            previous = now;
            draws[i] = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        }
        _measuring = false;
        Results.Add(new { kind = "measurement", label, arrows = ArrowCombat.Deployed.Count,
            targets = ArrowCombat.Deployed.FirstOrDefault()?.Targets.Count() ?? 0,
            level = CoreApi.Configuration.Current.VfxLevel.ToString(),
            frame_ms = Stats(frames), hud_process_ms = Stats(ProcessTimes), hud_draw_ms = Stats(DrawTimes),
            process_bytes_per_frame = _processBytes / (double)samples,
            draw_bytes_per_frame = _drawBytes / (double)samples,
            hud_draws = DrawTimes.Count, render_draw_calls = Stats(draws), live_roots = Roots() });
        Save();
    }

    private static async Task Release(Player player)
    {
        int before = Roots();
        await ArrowCombat.AccelerateAll(new ThrowingPlayerChoiceContext(), player, 5);
        await Frames(30);
        int after = Roots();
        Results.Add(new { kind = "release", level = CoreApi.Configuration.Current.VfxLevel.ToString(),
            before, after, deployed = ArrowCombat.Deployed.Count, pass = after == 0 && ArrowCombat.Deployed.Count == 0 });
        Save();
    }

    private static async Task Check(Player player)
    {
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = CoreVfxLevel.Full });
        await Frames(5);
        var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
        var views = (System.Collections.IDictionary)AccessTools.Field(typeof(ArrowHud), "_views").GetValue(hud)!;
        var actual = (IEnumerable<ArrowDeployment>)AccessTools.Field(typeof(ArrowHud), "_displayOrder").GetValue(hud)!;
        bool order = actual.SequenceEqual(ArrowCombat.DisplayOrder());
        bool targets = true;
        foreach (var arrow in actual)
        {
            var view = views[arrow]!;
            var points = (List<Vector2>)AccessTools.Property(view.GetType(), "Targets").GetValue(view)!;
            var expected = (Vector2[])AccessTools.Method(typeof(ArrowHud), "TargetPoints").Invoke(hud, [arrow])!;
            targets &= points.Count == expected.Length && points.Zip(expected).All(pair => pair.First.DistanceTo(pair.Second) < .001f);
        }
        var guides = hud.GetNode<Node2D>("ArrowGuides");
        var batches = guides.GetChildren().OfType<MultiMeshInstance2D>().ToArray();
        bool matrices = batches.All(batch => Enumerable.Range(0, batch.Multimesh.VisibleInstanceCount < 0 ? batch.Multimesh.InstanceCount : batch.Multimesh.VisibleInstanceCount).All(index =>
        {
            var transform = batch.Multimesh.GetInstanceTransform2D(index);
            return transform.IsFinite() && (batch.Name == "Fans" || Mathf.IsEqualApprox(transform.Y.Length(), 1));
        }));
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = CoreVfxLevel.Off });
        await Frames(3);
        bool off = !guides.IsVisibleInTree() && hud.GetChildren().OfType<Control>()
            .Where(node => node.HasNode("TiltedFrame")).All(node => node.IsVisibleInTree());
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = CoreVfxLevel.Reduced });
        await Frames(5);
        bool restored = guides.IsVisibleInTree();
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = CoreVfxLevel.Full });
        await Frames(3);
        Results.Add(new { kind = "regression", order, targets, matrices, off, restored,
            pass = order && targets && matrices && off && restored });
        Save();
    }

    private static async Task PrepareVideo(Player player)
    {
        await Prepare(player, 0, CoreVfxLevel.Full);
        foreach (var card in player.PlayerCombatState!.Hand.Cards.ToArray())
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        await CreatureCmd.SetMaxHp(player.Creature, 1000);
        await CreatureCmd.SetCurrentHp(player.Creature, 1000);
        player.PlayerCombatState.Energy = 20;
        foreach (string id in new[] { "JB03", "JB03", "JA77", "JA71" })
            await CardPileCmd.Add(ShoujoKagekiJunna.Models.CardCatalog.Create(id, player), PileType.Hand, skipVisuals: false);
        await Frames(30);
        int visible = NCombatRoom.Instance!.Ui.Hand.FindChildren("*", "Control", true, false)
            .OfType<NCard>().Count(card => card.IsVisibleInTree());
        Results.Add(new { kind = "video_ready", model_cards = player.PlayerCombatState.Hand.Cards.Count,
            visible_cards = visible, pass = visible == 4 });
        Save();
        if (visible != 4) throw new InvalidOperationException("Unexpected visible hand cards");
    }

    private static int Roots() => NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows")
        .FindChildren("*", "Control", true, false).OfType<Control>().Count(node => node.HasNode("TiltedFrame"));
    private static async Task Frames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
    private static object Stats(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return new { count = sorted.Length, mean = sorted.Length == 0 ? 0 : sorted.Average(),
            p95 = sorted.Length == 0 ? 0 : sorted[(int)((sorted.Length - 1) * .95)],
            max = sorted.LastOrDefault() };
    }
    private static void Save() => File.WriteAllText(Output, JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
}
