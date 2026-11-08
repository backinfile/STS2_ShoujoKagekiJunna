using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ExhaustCounterProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_exhaust_counter";
    public override string Args => "check";
    public override string Description => "Read the native exhaust-pile counter after normal card play.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        int actual = player!.PlayerCombatState!.ExhaustPile.Cards.Count;
        int displayed = (int)AccessTools.Field(typeof(NCombatCardPile), "_currentCount").GetValue(NCombatRoom.Instance!.Ui.ExhaustPile)!;
        return new(displayed == actual, JsonSerializer.Serialize(new { actual, displayed, pass = displayed == actual }));
    }
}

public sealed class ArrowFollowupProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_followup";
    public override string Args => "check";
    public override string Description => "Targeted acquisition, automatic arrow and motion checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => new(
        args.FirstOrDefault() == "demo" ? Demo(player!, args.ElementAtOrDefault(1)) : Check(player!), true, "Follow-up checks completed");
    private static CardModel? _observed;
    private static int _visuals, _exhaustHooks;
    private static void ObserveStableVisuals(MegaCrit.Sts2.Core.Nodes.Cards.NCard __1, CardPile __3)
    { if (__1.Model == _observed && __3.Type == PileType.Exhaust) _visuals++; }
    private static void ObserveVisuals(IEnumerable<CardPileAddResult> results)
    { if (results.Any(r => r.cardAdded == _observed &&
#if STS2_BETA
        r.targetPile == PileType.Exhaust
#else
        r.cardAdded.Pile?.Type == PileType.Exhaust
#endif
        )) _visuals++; }
    private static void ObserveExhaust(CardModel card) { if (card == _observed) _exhaustHooks++; }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../..", "out/tmp/arrow-followup"));
        Directory.CreateDirectory(directory);
        void Verify(string name, object? actual, object expected)
        {
            rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var targets = state.HittableEnemies.Where(c => c.IsAlive).ToArray();
        Verify("fixture has multiple targets", targets.Length >= 2, true);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var arrow in ArrowCombat.Deployed.ToArray())
        { ArrowCombat.Cancel(arrow.Card); await CardPileCmd.RemoveFromCombat(arrow.Card, skipVisuals: true); }
        player.PlayerCombatState!.Energy = 20;
        for (int i = 0; i < 3; i++)
        {
            int previous = ArrowCombat.Deployed.Count;
            try { await CardActions.Fly(context, player, 7 + i); }
            catch (NotImplementedException) { /* A player-choice request is the reported failure. */ }
            var added = ArrowCombat.Deployed.Skip(previous).ToArray();
            Verify($"automatic fly {i} deploys without a player choice", added.Length, 1);
            if (added.Length == 1)
            {
                Verify($"automatic fly {i} chooses a living target", targets.Contains(added[0].Play.Target), true);
                Verify($"automatic fly {i} keeps its damage", (int)added[0].Card.DynamicVars.Damage.BaseValue, 7 + i);
            }
        }
        await CardActions.Fly(context, player, 1, targets.Last());
        var explicitArrow = ArrowCombat.Deployed.Last();
        Verify("effect-specified target is preserved", explicitArrow.Play.Target == targets.Last(), true);
        _observed = explicitArrow.Card; _visuals = 0; _exhaustHooks = 0;
        var harmony = new Harmony("junna.followup.observer");
#if STS2_BETA
        harmony.Patch(AccessTools.Method(typeof(CardPileCmd), "GetTweenForCardsChangingPiles", [typeof(IEnumerable<CardPileAddResult>), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(ArrowFollowupProbe), nameof(ObserveVisuals)));
#else
        harmony.Patch(AccessTools.Method(typeof(CardPileCmd), "AppendPileLerpTween"),
            prefix: new HarmonyMethod(typeof(ArrowFollowupProbe), nameof(ObserveStableVisuals)));
#endif
        harmony.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.AfterCardExhausted)),
            prefix: new HarmonyMethod(typeof(ArrowFollowupProbe), nameof(ObserveExhaust)));
        try { await ArrowCombat.Accelerate(context, explicitArrow, 99); }
        finally { harmony.UnpatchAll(harmony.Id); _observed = null; }
        Verify("resolved arrow exhausts", explicitArrow.Card.Pile!.Type, PileType.Exhaust);
        Verify("resolved arrow does not request a card or smoke animation", _visuals, 0);
        Verify("resolved arrow retains native exhaustion hooks", _exhaustHooks, 1);

        foreach (string id in new[] { "JK48", "JK48B", "A" })
        {
            var deck = player.Deck;
            var before = deck.Cards.ToHashSet();
            var acquired = player.RunState.CreateCard(CardCatalog.Model(id), player);
            var curse = player.RunState.CreateCard<Injury>(player);
            await CardPileCmd.Add(new[] { acquired, curse }, PileType.Deck, skipVisuals: true);
            var gained = deck.Cards.Where(c => !before.Contains(c)).ToArray();
            var group = KnowledgeCombat.Group(acquired)!;
            Verify(id + " group stays before the tablet curse", gained.Last() == curse, true);
            Verify(id + " main card stays first", CardCatalog.DesignId(gained.First()), group.MainCardId);
            Verify(id + " exactly one tablet curse", gained.Count(c => c is Injury), 1);
            Verify(id + " complete group quantity", gained.Length, 2 + group.GrantedCardCounts.Values.Sum());
        }

        // Drive the actual HUD after one deployment disappears and another target moves.
        for (int i = 0; i < 3; i++) await CardActions.Fly(context, player, 1, targets[i % targets.Length]);
        var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
        await Task.Delay(500);
        hud.SetProcess(false);
        try
        {
            hud._Process(1.0 / 60);
            var roots = hud.GetChildren().OfType<Control>().Where(n => n.GetNodeOrNull<Control>("TiltedFrame") != null).ToArray();
            if (roots.Length >= 2)
            {
                var poses = roots.ToDictionary(root => root, root => (root.Position, root.GetNode<Control>("TiltedFrame").Rotation));
                ArrowCombat.Cancel(ArrowCombat.Deployed.First().Card);
                var node = targets.Select(target => NCombatRoom.Instance.GetCreatureNode(target)!).OrderBy(n => n.Position.X).Last();
                var oldTargetPosition = node.Position;
                node.Position += new Vector2(-500, 300);
                hud._Process(1.0 / 60);
                float firstDistance = roots.Where(root => !root.IsQueuedForDeletion()).Max(root => root.Position.DistanceTo(poses[root].Position));
                float firstAngle = roots.Where(root => !root.IsQueuedForDeletion()).Max(root => Mathf.Abs(Mathf.AngleDifference(poses[root].Rotation, root.GetNode<Control>("TiltedFrame").Rotation)));
                Verify("layout begins moving without a slot jump", firstDistance > 0 && firstDistance < 20, true);
                Verify("retarget begins turning without an angle jump", firstAngle > 0 && firstAngle < .15f, true);
                for (int i = 0; i < 90; i++) hud._Process(1.0 / 60);
                Verify("layout completes its move", roots.Where(root => !root.IsQueuedForDeletion()).Any(root => root.Position.DistanceTo(poses[root].Position) > 60), true);
                node.Position = oldTargetPosition;
                var releasing = ArrowCombat.Deployed.First(a => a.Target == node.Entity);
                var views = (System.Collections.IDictionary)AccessTools.Field(typeof(ArrowHud), "_views").GetValue(hud)!;
                var view = views[releasing]!;
                var releaseRoot = (Control)view.GetType().GetProperty("Root")!.GetValue(view)!;
                var releaseFrame = releaseRoot.GetNode<Control>("TiltedFrame");
                float beforeRelease = releaseFrame.Rotation;
                var release = ArrowCombat.Accelerate(context, releasing, 99);
                Verify("release preserves the current turn angle", Mathf.Abs(Mathf.AngleDifference(beforeRelease, releaseFrame.Rotation)) < .001f, true);
                await release;
                Verify("turning arrow completes normally", releasing.Card.Pile!.Type, PileType.Exhaust);
            }
            else Verify("motion fixture has surviving arrows", roots.Length >= 2, true);
        }
        finally { hud.SetProcess(true); }
        var motionType = typeof(ArrowHud).Assembly.GetType("ShoujoKagekiJunna.Combat.ArrowLayoutMotion");
        if (motionType != null)
        {
            object Motion(float angle)
            {
                var instance = Activator.CreateInstance(motionType, true)!;
                motionType.GetMethod("Reset")!.Invoke(instance, [Vector2.Zero, angle]);
                return instance;
            }
            float Angle(object motion) => (float)motionType.GetProperty("Angle")!.GetValue(motion)!;
            void Advance(object motion, float angle, float delta) => motionType.GetMethod("Advance")!.Invoke(motion,
                [Vector2.Zero, Vector2.FromAngle(angle + Mathf.Pi * .5f) * 1000, delta]);
            var turning = Motion(0);
            var speeds = new List<float>();
            for (int i = 0; i < 90; i++)
            {
                float previous = Angle(turning);
                Advance(turning, Mathf.Pi * .5f, 1f / 60);
                speeds.Add((Angle(turning) - previous) * 60);
            }
            Verify("turn speed rises gently then slows", speeds.Max() > speeds[0] * 2 && speeds[^1] < .001f, true);
            Verify("turn settles without bouncing", speeds.All(speed => speed >= -.00001f) && Mathf.Abs(Angle(turning) - Mathf.Pi * .5f) < .001f, true);
            var wrapping = Motion(Mathf.DegToRad(179));
            Advance(wrapping, Mathf.DegToRad(-179), 1f / 60);
            Verify("rotation crosses wrap by the shortest path", Angle(wrapping) > Mathf.DegToRad(179) && Angle(wrapping) < Mathf.Pi, true);
            var redirected = Motion(0);
            for (int i = 0; i < 5; i++) Advance(redirected, Mathf.Pi * .5f, 1f / 60);
            float before = Angle(redirected);
            Advance(redirected, -Mathf.Pi * .5f, 1f / 240);
            Verify("mid-turn retarget retains angular momentum", Angle(redirected) > before, true);
            float Sample(int fps)
            {
                var motion = Motion(0);
                for (int i = 0; i < fps / 2; i++) Advance(motion, Mathf.Pi * .5f, 1f / fps);
                return Angle(motion);
            }
            Verify("turning is consistent at 30 and 120 FPS", Mathf.Abs(Sample(30) - Sample(120)) < .003f, true);
            File.WriteAllText(Path.Combine(directory, "turn-speed.json"), JsonSerializer.Serialize(speeds));
        }
        else Verify("smooth motion curve exists", false, true);
        Verify("complete", true, true);
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Follow-up regression check failed");
    }

    private static async Task Demo(Player player, string? mode)
    {
        var context = new ThrowingPlayerChoiceContext();
        if (mode == "kill")
        {
            await CreatureCmd.Kill(player.Creature.CombatState!.HittableEnemies.First());
            return;
        }
        if (mode == "finish") { await ArrowCombat.AccelerateAll(context, player, 99); return; }
        foreach (var arrow in ArrowCombat.Deployed.ToArray())
        { ArrowCombat.Cancel(arrow.Card); await CardPileCmd.RemoveFromCombat(arrow.Card, skipVisuals: true); }
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        var targets = player.Creature.CombatState!.HittableEnemies.ToArray();
        foreach (var target in targets)
        { await CreatureCmd.SetMaxHp(target, 1000); await CreatureCmd.SetCurrentHp(target, 1000); }
        player.PlayerCombatState!.Energy = 10;
        var sweep = CardCatalog.Create("JA76", player);
        await CardPileCmd.Add(sweep, PileType.Hand, skipVisuals: true);
        await CardActions.PaidPlay(context, sweep);
        for (int i = 0; i < 4; i++) await CardActions.Fly(context, player, 4, targets[i % targets.Length]);
    }
}

public sealed class TargetedFlyProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_fly_targets";
    public override string Args => "check";
    public override string Description => "Check source-assigned flying-arrow targets, including lethal hits.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => new(Check(player!, args.Contains("power")), true, "Source target checks completed");
    private static readonly List<Creature?> Requested = [];
    private static void Observe(Creature? target) => Requested.Add(target);

    private static async Task Check(Player player, bool lethalPower)
    {
        var rows = new List<object>();
        void Verify(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var context = new ThrowingPlayerChoiceContext();
        var targets = player.Creature.CombatState!.HittableEnemies.ToArray();
        Verify("fixture has two targets", targets.Length, 2);
        async Task Reset()
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
            foreach (var target in targets)
            { await CreatureCmd.SetMaxHp(target, 1000000); await CreatureCmd.SetCurrentHp(target, 1000000); }
            player.PlayerCombatState!.Energy = 20;
            player.PlayerCombatState.IncrementTurnNumber();
            Requested.Clear();
        }
        async Task Play(string id, Creature? target = null)
        {
            var card = CardCatalog.Create(id, player);
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            await CardActions.PaidPlay(context, card, target);
        }
        var harmony = new Harmony("junna.source-target.observer");
        harmony.Patch(AccessTools.Method(typeof(CardActions), nameof(CardActions.Fly)),
            prefix: new HarmonyMethod(typeof(TargetedFlyProbe), nameof(Observe)));
        try
        {
            if (!lethalPower)
            {
                await Reset(); await Play("JA76");
                Verify("sweep creates one arrow for each original enemy", Requested.Count == 2 && targets.All(target => Requested.Count(t => t == target) == 1), true);
                Verify("sweep deploys one arrow toward each enemy", ArrowCombat.Deployed.Count == 2 && targets.All(target => ArrowCombat.Deployed.Count(a => a.Play.Target == target) == 1), true);
                await Reset(); await Play("JA81", targets.Last());
                Verify("linked shot supplies the original target", Requested.Count == 1 && Requested[0] == targets.Last(), true);
                Verify("linked shot arrow keeps that target", ArrowCombat.Deployed.Single().Play.Target == targets.Last(), true);
                await Reset(); await Play("JZ05"); await Play("JB01", targets.Last());
                Verify("improvisation supplies the triggering target", Requested.Count == 1 && Requested[0] == targets.Last(), true);
                Verify("improvisation arrow keeps that target", ArrowCombat.Deployed.Single().Play.Target == targets.Last(), true);
            }
            await Reset(); await CreatureCmd.SetCurrentHp(targets.First(), 1);
            if (lethalPower) await Play("JZ05");
            await Play(lethalPower ? "JB01" : "JA81", targets.First());
            Verify("lethal source still supplies the original target", Requested.Count == 1 && Requested[0] == targets.First(), true);
            Verify("lethal source switches to the adjacent surviving target", ArrowCombat.Deployed.Single().Play.Target == targets.Last(), true);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        rows.Add(new { name = "complete", pass = true });
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../..", "out/tmp/fly-targets"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, lethalPower ? "power-results.json" : "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Source-assigned fly target check failed");
    }
}

public sealed class ArrowCadenceProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_cadence";
    public override string Args => "check";
    public override string Description => "Verify that arrow deployment releases the action queue when drawing starts.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => new(Check(player!), true, "Arrow cadence checks completed");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Verify(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var context = new ThrowingPlayerChoiceContext();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var arrow in ArrowCombat.Deployed.ToArray())
        { ArrowCombat.Cancel(arrow.Card); await CardPileCmd.RemoveFromCombat(arrow.Card, skipVisuals: true); }
        var target = player.Creature.CombatState!.HittableEnemies.First();
        await CreatureCmd.SetMaxHp(target, 1000000); await CreatureCmd.SetCurrentHp(target, 1000000);
        var room = NCombatRoom.Instance!;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var first = CardActions.Fly(context, player, 1, target);
        ArrowHud? hud = null;
        ArrowDeploymentVfx? effect = null;
        for (int i = 0; i < 60 && effect == null; i++)
        {
            await Task.Delay(20);
            hud = room.GetNodeOrNull<ArrowHud>("JunnaArrows");
            effect = hud?.GetChildren().OfType<ArrowDeploymentVfx>().FirstOrDefault();
        }
        double leadSeconds = watch.Elapsed.TotalSeconds;
        double duration = effect!.Duration;
        float drawEnd = (float)AccessTools.Property(typeof(ArrowDeploymentVfx), "DrawEndTime").GetValue(effect)!;
        await first;
        watch.Stop();
        bool active = GodotObject.IsInstanceValid(effect) && !effect.IsQueuedForDeletion();
        Verify("action queue resumes before the full animation ends", watch.Elapsed.TotalSeconds - leadSeconds < duration - .3, true);
        Verify("drawing animation continues after play returns", active, true);
        Verify("queue resumes while bow is still drawing", active && (float)AccessTools.Field(typeof(ArrowDeploymentVfx), "_time").GetValue(effect)! < drawEnd, true);
        var second = CardActions.Fly(context, player, 1, target);
        bool overlap = false;
        for (int i = 0; i < 60 && !overlap; i++)
        {
            await Task.Delay(20);
            overlap = hud!.GetChildren().OfType<ArrowDeploymentVfx>().Count(e => !e.IsQueuedForDeletion()) >= 2;
        }
        Verify("next deployment overlaps the preceding bow draw", overlap, true);
        await second;
        await Task.Delay((int)(duration * 1000) + 250);
        Verify("completed visuals clean up independently", hud!.GetChildren().OfType<ArrowDeploymentVfx>().Count(), 0);
        Verify("both cards remain deployed", ArrowCombat.Deployed.Count, 2);
        await CardActions.Fly(context, player, 1, target);
        var early = ArrowCombat.Deployed.Last();
        await ArrowCombat.Accelerate(context, early, 99);
        await Task.Delay(500);
        Verify("completion during the residual animation cleans up", hud.GetChildren().OfType<ArrowDeploymentVfx>().Count(), 0);
        Verify("early completed arrow exhausts normally", early.Card.Pile!.Type, PileType.Exhaust);
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../..", "out/tmp/arrow-cadence"));
        Directory.CreateDirectory(directory);
        rows.Add(new { name = "timing", elapsedSeconds = watch.Elapsed.TotalSeconds, leadSeconds, fullDuration = duration, pass = true });
        rows.Add(new { name = "complete", pass = true });
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Arrow cadence regression check failed");
    }
}
