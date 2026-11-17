using System.Collections;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;

namespace JunnaIntegration;

public sealed class ArrowPreviewPlacementProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_preview_placement";
    public override string Args => "check | demo";
    public override string Description => "Observe anchored arrow previews and native mouse card targeting.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        return new(Run(player, args.FirstOrDefault() == "demo"), true, "Preview placement probe started");
    }

    private static async Task Run(Player player, bool demo)
    {
        var room = NCombatRoom.Instance!;
        var hud = room.GetNode<ArrowHud>("JunnaArrows");
        var viewport = room.GetViewport();
        var tree = room.GetTree();
        var enemy = room.GetCreatureNode(player.Creature.CombatState!.HittableEnemies.First())!.Hitbox;
        var previewField = AccessTools.Field(typeof(ArrowHud), "_hoverPreview");
        var cache = (IDictionary)AccessTools.Field(typeof(ArrowHud), "_previewCache").GetValue(hud)!;
        Control? Preview() => (Control?)previewField.GetValue(hud);
        bool Visible() => Preview()?.IsVisibleInTree() == true;
        async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await room.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        void Move(Vector2 point, bool held = false) => viewport.PushInput(new InputEventMouseMotion {
            Position = point, GlobalPosition = point, ButtonMask = held ? MouseButtonMask.Left : 0 }, true);
        void Button(MouseButton button, bool pressed, Vector2 point) => viewport.PushInput(new InputEventMouseButton {
            ButtonIndex = button, Pressed = pressed, Position = point, GlobalPosition = point }, true);
        Vector2 EnemyPoint() => enemy.GetGlobalTransformWithCanvas() * (enemy.Size * new Vector2(.5f, .9f));
        var rows = new List<object>();
        bool pass = true;
        void Check(string name, bool passed, object? actual = null)
        {
            rows.Add(new { name, pass = passed, actual });
            pass &= passed;
        }
        void ColdCache()
        {
            if (demo) return;
            foreach (var arrow in cache.Keys.Cast<ArrowDeployment>().ToArray())
                AccessTools.Method(typeof(ArrowHud), "RemovePreviewCard").Invoke(hud, [arrow]);
        }

        Move(new Vector2(900, 900));
        await Frames(24);
        Move(EnemyPoint());
        await Frames(24);
        var positions = new List<Vector2>();
        for (int i = 0; i < 24; i++)
        {
            if (Visible()) positions.Add(Preview()!.GlobalPosition);
            await Frames(1);
        }
        var bounds = Preview()?.GetGlobalRect() ?? default;
        var anchor = enemy.GetGlobalRect();
        Check("ordinary enemy hover stays visible and stable", positions.Count == 24 && positions.All(p => p.DistanceTo(positions[0]) < 1), positions.Select(p => p.ToString()).Distinct().ToArray());
        Check("preview remains over the hovered enemy", Math.Abs(bounds.GetCenter().X - anchor.GetCenter().X) < 1 && bounds.End.Y <= anchor.Position.Y - 8, bounds.ToString());
        Check("preview stays inside the viewport", hud.GetViewportRect().Encloses(bounds));
        var cardScales = Preview()!.GetChildren().OfType<NCard>().Where(card => card.IsVisibleInTree()).Select(card => card.Scale.X).ToArray();
        float handScale = HandPosHelper.GetScale(1).X;
        Check("hover cards stay slightly smaller than ordinary hand cards", cardScales.Length > 0
            && cardScales.All(scale => scale >= handScale * .8f && scale < handScale), cardScales);
        if (demo) await Task.Delay(1500);

        var holder = room.Ui.Hand.ActiveHolders.First(h => h.CardModel?.TargetType == TargetType.AnyEnemy);
        var cardPoint = holder.Hitbox.GetGlobalTransformWithCanvas() * (holder.Hitbox.Size * .5f);
        Move(cardPoint);
        await Frames(12);
        Button(MouseButton.Left, true, cardPoint);
        await Frames(6);
        Move(EnemyPoint(), held: true);
        await Frames(12);
        Check("real mouse drag enters native card play", room.Ui.Hand.InCardPlay);
        ColdCache();
        bool hiddenThroughoutDrag = true;
        for (int i = 0; i < 24; i++)
        {
            await Frames(1);
            hiddenThroughoutDrag &= !Visible();
        }
        Check("arrow preview stays hidden while dragging onto enemy", hiddenThroughoutDrag);
        if (!demo) Check("dragging constructs no cached preview cards", cache.Count == 0, cache.Count);
        if (demo) await Task.Delay(1500);
        Button(MouseButton.Right, true, EnemyPoint());
        await Frames(2);
        Button(MouseButton.Right, false, EnemyPoint());
        Button(MouseButton.Left, false, EnemyPoint());
        Move(new Vector2(900, 900));
        await Frames(24);
        Move(EnemyPoint());
        await Frames(24);
        Check("ordinary hover returns after cancelling drag", !room.Ui.Hand.InCardPlay && Visible());
        Check("hover card size stays fixed after reopening", Preview()!.GetChildren().OfType<NCard>()
            .Where(card => card.IsVisibleInTree()).Select(card => card.Scale.X).SequenceEqual(cardScales));
        if (demo) await Task.Delay(1500);

        if (!demo)
        {
            var manager = NTargetManager.Instance;
            manager.StartTargeting(TargetType.AnyEnemy, new Vector2(960, 800), TargetMode.ClickMouseToTarget, null, null);
            Move(EnemyPoint());
            ColdCache();
            await Frames(24);
            Check("standalone native targeting hides preview", manager.IsInSelection && !room.Ui.Hand.InCardPlay && !Visible());
            Check("standalone targeting constructs no preview cards", cache.Count == 0, cache.Count);
            manager.CancelTargeting();
            Move(new Vector2(900, 900));
            await Frames(24);
            Move(EnemyPoint());
            await Frames(24);
            Check("ordinary hover returns after cancelling target selection", Visible());
        }
        string file = Path.GetFullPath("../../../out/tmp/arrow-preview-position/" + (demo ? "demo" : "check") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(new { pass, rows }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
