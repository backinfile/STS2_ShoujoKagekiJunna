using System.Collections;
using System.Diagnostics;
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
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

/// <summary>Focused coverage, density, input, cache and release checks for the arrow presentation.</summary>
public sealed class ArrowPresentationProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_ui";
    public override string Args => "check | prepare <enemy|friendly|group|live> <count> | hover <enemy|arrow|overflow|preview|none> <index> | page <next|previous> | key <next|previous|escape> | batch <count> | info";
    public override string Description => "Check and demonstrate the arrow presentation in an isolated game.";
    public override bool IsNetworked => false;
    private static readonly List<object> Results = [];
    private static readonly List<long> Resolved = [];
    private static Vector2 _pointer;
    private static string Output => Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "..", "arrow-ui-results.json"));
    private static ArrowHud Hud => NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
    private static T Field<T>(string name) => (T)AccessTools.Field(typeof(ArrowHud), name).GetValue(Hud)!;
    private static object View(ArrowDeployment arrow) => Field<IDictionary>("_views")[arrow]!;
    private static T ViewField<T>(ArrowDeployment arrow, string name)
        => (T)AccessTools.Field(View(arrow).GetType(), name).GetValue(View(arrow))!;
    private static Control Root(ArrowDeployment arrow)
        => (Control)AccessTools.Property(View(arrow).GetType(), "Root").GetValue(View(arrow))!;
    private static List<Vector2> Points(ArrowDeployment arrow)
        => (List<Vector2>)AccessTools.Property(View(arrow).GetType(), "Targets").GetValue(View(arrow))!;
    private static NCard[] PreviewCards => Field<Control?>("_hoverPreview")?.GetChildren().OfType<NCard>().Where(card => card.Visible).ToArray() ?? [];

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        return args.FirstOrDefault() switch
        {
            "check" => new(Check(player), true, "Arrow presentation checks started"),
            "prepare" => new(Prepare(player, args[1], int.Parse(args[2])), true, "Arrow presentation prepared"),
            "hover" => new(Hover(args[1], int.TryParse(args.ElementAtOrDefault(2), out var index) ? index : 0), true, "Pointer moved"),
            "page" => new(Page(args.ElementAtOrDefault(1) != "previous"), true, "Card input sent"),
            "key" => new(KeyInput(args[1]), true, "Key input sent"),
            "batch" => new(Batch(player, int.Parse(args[1])), true, "Batch timing started"),
            "info" => new(true, JsonSerializer.Serialize(new
            {
                deployed = ArrowCombat.Deployed.Count, visible = Field<HashSet<ArrowDeployment>>("_visibleArrows").Count,
                cache = Field<IDictionary>("_previewCache").Count, index = Field<int>("_previewIndex"),
                active = Field<ArrowDeployment?>("_inspectedArrow")?.Card.Title,
                previews = PreviewCards.Select(card => new { card.Model!.Title, scale = card.Scale.X }).ToArray()
            })),
            _ => new(false, Args)
        };
    }

    private static void Verify(string name, bool pass, object? details = null)
    {
        Results.Add(new { name, pass, details });
        Save();
        if (!pass) throw new InvalidOperationException(name);
    }

    private static void Save() => File.WriteAllText(Output,
        JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));

    private static async Task Prepare(Player player, string kind, int count)
    {
        await Hover("none", 0);
        foreach (var arrow in ArrowCombat.Deployed.ToArray())
        {
            ArrowCombat.Cancel(arrow.Card);
            await CardPileCmd.RemoveFromCombat(arrow.Card, skipVisuals: true);
        }
        foreach (var power in player.Creature.Powers.OfType<CatalogPower>().ToArray()) await PowerCmd.Remove(power);
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = CoreVfxLevel.Full });
        var state = player.Creature.CombatState!;
        foreach (var enemy in state.HittableEnemies)
        {
            await CreatureCmd.SetMaxHp(enemy, 10000);
            await CreatureCmd.SetCurrentHp(enemy, 10000);
        }
        player.PlayerCombatState!.Energy = 100;
        Resolved.Clear();
        for (int i = 0; i < count; i++)
        {
            var card = kind == "group" ? state.CreateCard(ModelDb.Card<GroupArrowProbe>(), player)
                : CardCatalog.Create(kind == "friendly" ? "JA80" : "JB03", player);
            await CardPileCmd.Add(card, ArrowPile.PileId, skipVisuals: true);
            var play = new CardPlay
            {
                Card = card, Target = kind == "friendly" ? player.Creature : state.HittableEnemies.First(),
#if STS2_BETA
                Player = player,
#endif
                ResultPile = ArrowPile.PileId, Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1
            };
            if (kind == "live") ArrowCombat.Deploy((JunnaFullDraw)card, play, 3);
            else ArrowCombat.Deploy(card, play, 3, (_, _) =>
            {
                Resolved.Add(ArrowCombat.Find(card)!.Sequence);
                return Task.CompletedTask;
            });
        }
        await Task.Delay(500);
    }

    private static async Task Hover(string kind, int index)
    {
        Control? control = kind switch
        {
            "enemy" => NCombatRoom.Instance!.CreatureNodes.Where(node => node.Entity.IsEnemy && node.Entity.IsAlive).ElementAt(index).Hitbox,
            "arrow" => Root(ArrowCombat.DisplayOrder()[index]).GetNode<Control>("TiltedFrame"),
            "overflow" => Hud.GetChildren().OfType<NButton>().Where(button => button.Visible && button.Name.ToString().StartsWith("ArrowOverflow")).ElementAt(index),
            "preview" => Field<List<(ArrowDeployment Arrow, NCard Card)>>("_previewCards")[index].Card.GetNode<Control>("ArrowPreviewHitbox"),
            _ => null
        };
        var viewport = NCombatRoom.Instance!.GetViewport();
        var point = control == null ? Vector2.Zero : control.GetGlobalTransform() * (control.Size * .5f);
        if (kind == "preview")
        {
            var cards = Field<List<(ArrowDeployment Arrow, NCard Card)>>("_previewCards");
            int active = cards.FindIndex(pair => pair.Arrow == Field<ArrowDeployment?>("_inspectedArrow"));
            point = control!.GetGlobalTransform() * (control.Size * new Vector2(index > active ? .85f : .15f, .35f));
        }
        _pointer = point;
        var windowPoint = viewport.GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = windowPoint, GlobalPosition = windowPoint });
        await Task.Delay(300);
    }

    private static async Task Page(bool next)
    {
        var viewport = Hud.GetViewport();
        var point = viewport.GetFinalTransform() * _pointer;
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
            Pressed = true, ButtonIndex = next ? MouseButton.WheelDown : MouseButton.WheelUp });
        for (int frame = 0; frame < 60; frame++)
        {
            await Task.Delay(16);
            if (Field<Tween?>("_previewMotion")?.IsRunning() != true) return;
        }
        throw new TimeoutException("Preview focus animation did not finish");
    }

    private static async Task KeyInput(string key)
    {
        var code = key switch { "next" => Key.Pagedown, "previous" => Key.Pageup, _ => Key.Escape };
        Input.ParseInputEvent(new InputEventKey { Pressed = true, Keycode = code });
        await Task.Delay(200);
    }

    private static async Task Batch(Player player, int count)
    {
        await Prepare(player, "group", count);
        var expected = ArrowCombat.ResolutionOrder().Select(arrow => arrow.Sequence).ToArray();
        var watch = Stopwatch.StartNew();
        await ArrowCombat.AccelerateAll(new ThrowingPlayerChoiceContext(), player, 99);
        watch.Stop();
        await Task.Delay(400);
        Verify("batch order and cleanup", Resolved.SequenceEqual(expected) && ArrowCombat.Deployed.Count == 0,
            new { count, elapsed_ms = watch.Elapsed.TotalMilliseconds });
    }

    private static async Task Check(Player player)
    {
        Results.Clear();
        await Prepare(player, "enemy", 0);
        var context = new ThrowingPlayerChoiceContext();
        var card = CardCatalog.Create("JB03", player);
        await CardPileCmd.Add(card, PileType.Hand, skipVisuals: false);
        await CardActions.PaidPlay(context, card, player.Creature.CombatState!.HittableEnemies.First());
        await Task.Delay(1600);
        var arrow = ArrowCombat.Find(card)!;
        var originalTarget = arrow.Target;
        Verify("single arrow starts with one display target", Points(arrow).Count == 1 && !ViewField<bool>(arrow, "Group"));
        var power = CardCatalog.Create("JA205", player);
        await CardPileCmd.Add(power, PileType.Hand, skipVisuals: false);
        await CardActions.PaidPlay(context, power);
        await Task.Delay(300);
        int enemies = player.Creature.CombatState.HittableEnemies.Count();
        Verify("waiting arrow updates effective coverage and group appearance", Points(arrow).Count == enemies && ViewField<bool>(arrow, "Group"));
        Verify("original side-effect target and order remain intact", arrow.Target == originalTarget && !arrow.TargetsAllEnemies);
        await Hover("enemy", enemies - 1);
        Verify("expanded arrow appears when hovering another enemy", PreviewCards.Any(node => node.Model == card));
        await Hover("none", 0);
        await PowerCmd.Remove(player.Creature.Powers.OfType<CatalogPower>().Single(item => item.DesignId == "JA205"));
        await Task.Delay(200);
        Verify("removing the expansion restores single-target appearance", Points(arrow).Count == 1 && !ViewField<bool>(arrow, "Group"));
        var replacementPower = CardCatalog.Create("JA205", player);
        await CardPileCmd.Add(replacementPower, PileType.Hand, skipVisuals: false);
        await CardActions.PaidPlay(context, replacementPower);
        await Task.Delay(200);
        var hp = player.Creature.CombatState.HittableEnemies.Select(enemy => enemy.CurrentHp).ToArray();
        await ArrowCombat.Accelerate(context, arrow, 99);
        Verify("expanded arrow still hits every enemy for 25", player.Creature.CombatState.HittableEnemies
            .Select((enemy, i) => hp[i] - enemy.CurrentHp).All(damage => damage == 25));

        var direct = player.Creature.CombatState.CreateCard(ModelDb.Card<ExternalDirectCard>(), player);
        await CardPileCmd.Add(direct, PileType.Hand, skipVisuals: false);
        await HandArrow.Play(context, direct);
        await Task.Delay(1500);
        var directArrow = ArrowCombat.Find(direct)!;
        Verify("direct-damage material remains single-target under expansion", DamageCapability.CanDealDamage(direct)
            && !ViewField<bool>(directArrow, "Group") && Points(directArrow).Count == 1);
        var directTargets = player.Creature.CombatState.HittableEnemies.ToArray();
        var directHp = directTargets.Select(enemy => enemy.CurrentHp).ToArray();
        int directIndex = Array.IndexOf(directTargets, directArrow.Target);
        await ArrowCombat.Accelerate(context, directArrow, 99);
        Verify("direct-damage material keeps its original damage coverage", directTargets
            .Select((enemy, i) => directHp[i] - enemy.CurrentHp).SequenceEqual(
                Enumerable.Range(0, directTargets.Length).Select(i => i == directIndex ? 4 : 0)));

        await Prepare(player, "enemy", 50);
        await Hover("enemy", 0);
        var firstIds = PreviewCards.Select(node => node.GetInstanceId()).Order().ToArray();
        var fanRect = Field<Control>("_hoverPreview").GetGlobalRect();
        NCard Inspected() => PreviewCards.Single(node => node.Model == Field<ArrowDeployment?>("_inspectedArrow")!.Card);
        Verify("dense hover has a compact native-hand fan", firstIds.Length is > 0 and <= 5
            && Inspected().Scale.X >= .89f && Field<Control>("_hoverPreview") is not Panel
            && Field<Control>("_hoverPreview").Size.Y < Hud.GetViewportRect().Size.Y * .5f);
        Verify("neighbours are smaller and current card stays upright", PreviewCards.Where(node => node != Inspected())
            .All(node => node.Scale.X < Inspected().Scale.X && node.ZIndex < Inspected().ZIndex)
            && Mathf.Abs(Inspected().Rotation) < .001f);
        await Hover("none", 0);
        await Hover("enemy", 0);
        Verify("reentering a target reuses preview nodes", PreviewCards.Select(node => node.GetInstanceId()).Order().SequenceEqual(firstIds));
        await Page(true);
        Verify("wheel inspects the next physical card", Field<int>("_previewIndex") == 1
            && Inspected().Model == ArrowCombat.DisplayOrder()[1].Card);
        await Page(false);
        Verify("previous card reuses its warm fan", Field<int>("_previewIndex") == 0
            && PreviewCards.Select(node => node.GetInstanceId()).Order().SequenceEqual(firstIds));
        await Hover("preview", 1);
        Verify("hovering an exposed neighbour brings it forward", Field<int>("_previewIndex") == 1);
        await Task.Delay(450);
        Verify("neighbour hover remains stable after its lift", Field<int>("_previewIndex") == 1
            && Field<Control>("_hoverPreview").GetGlobalRect() == fanRect);
        await Page(true);
        Verify("wheel over the fan keeps the selected card", Field<Control>("_hoverPreview").Visible
            && Field<int>("_previewIndex") == 2);
        await Hover("none", 0);
        await Hover("enemy", 0);
        for (int i = 0; i < 55; i++) await Page(true);
        Verify("all dense arrows remain reachable with a bounded cache", Field<int>("_previewIndex") == 49
            && Field<IDictionary>("_previewCache").Count <= 10);
        await Hover("none", 0);
        await Hover("enemy", 0);
        await KeyInput("next");
        Verify("keyboard inspects the next card", Field<int>("_previewIndex") == 1);
        await KeyInput("previous");
        Verify("keyboard returns to the previous card", Field<int>("_previewIndex") == 0);
        await KeyInput("escape");
        Verify("escape closes the preview until pointer leaves", !Field<Control>("_hoverPreview").Visible);
        await Hover("none", 0);
        var lines = Hud.GetNode<Node2D>("ArrowGuides").GetNode<MultiMeshInstance2D>("SingleLines").Multimesh;
        float idleAlpha = lines.GetInstanceColor(0).A;
        await Hover("arrow", 0);
        Verify("focused guide is stronger and unrelated guides fade", lines.GetInstanceColor(0).A > idleAlpha
            && lines.GetInstanceColor(1).A < idleAlpha);
        await Hover("none", 0);
        await Hover("overflow", 0);
        Verify("overflow opens its hidden arrows", PreviewCards.Length > 0 && PreviewCards.All(node => !Field<HashSet<ArrowDeployment>>("_visibleArrows").Any(item => item.Card == node.Model)));
        var removed = ArrowCombat.Deployed.First(item => item.Card == PreviewCards[0].Model);
        ArrowCombat.Cancel(removed.Card);
        await CardPileCmd.RemoveFromCombat(removed.Card, skipVisuals: true);
        await Task.Delay(350);
        Verify("removed arrow is evicted from preview cache", !Field<IDictionary>("_previewCache").Contains(removed));

        await Prepare(player, "friendly", 100);
        var slots = Field<Dictionary<ArrowDeployment, Vector2>>("_slots");
        float bottom = Field<float>("_layoutBottom");
        int visible = Field<HashSet<ArrowDeployment>>("_visibleArrows").Count;
        Verify("dense friendly layout stays inside its vertical capacity", visible < 100 && slots.Values.All(point => point.Y <= bottom + .01f), new { visible, bottom });
        Verify("overflow quantity accounts for every hidden arrow", Hud.GetChildren().OfType<NButton>().Where(button => button.Visible && button.Name.ToString().StartsWith("ArrowOverflow"))
            .Sum(button => int.Parse(button.GetNode<Label>("Text").Text[1..])) + visible == 100);
        await Hover("overflow", 0);
        Verify("friendly overflow remains readable", PreviewCards.Length is > 0 and <= 5 && Inspected().Scale.X >= .89f);
        foreach (var level in new[] { CoreVfxLevel.Reduced, CoreVfxLevel.Off, CoreVfxLevel.Full })
        {
            CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = level });
            await Task.Delay(150);
            Verify("information remains available at " + level, Hud.GetChildren().OfType<NButton>().Any(button => button.Visible)
                && Field<HashSet<ArrowDeployment>>("_visibleArrows").All(item => Root(item).Visible));
        }
        await Hover("none", 0);
        await Batch(player, 50);
        Verify("released arrows leave no roots or cached cards", Field<IDictionary>("_views").Count == 0
            && Field<IDictionary>("_previewCache").Count == 0 && !Hud.FindChildren("FloatingArrow", "Control", true, false).Any());
        await Prepare(player, "enemy", 12);
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = CoreVfxLevel.Off });
        await ArrowCombat.AccelerateAll(context, player, 99);
        await Task.Delay(200);
        Verify("effect-off release frees every arrow", ArrowCombat.Deployed.Count == 0 && Field<IDictionary>("_views").Count == 0);
        CoreApi.Configuration.Update(CoreApi.Configuration.Current with { VfxLevel = CoreVfxLevel.Full });
        Verify("complete", true);
    }
}
