using System.Collections;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowHoverDensityProbe : AbstractConsoleCmd
{
    private static Vector2 _pointer;
    public override string CmdName => "junna_hover_density";
    public override string Args => "check <count> | prepare <count> | hover <enemy|player|arrow|clear> | placement | live-damage | info";
    public override string Description => "Check actual card visibility for dense arrow hover previews.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "live-damage")
            return new(CheckLiveDamage(player), true, "Live arrow damage check started");
        if (args.FirstOrDefault() == "placement")
            return new(CheckPlacement(player), true, "Hover placement check started");
        if (args.FirstOrDefault() == "states")
            return new(true, JsonSerializer.Serialize(ArrowCombat.Deployed.Select(arrow => new {
                arrow.Sequence,
                owner = arrow.Card.Owner.NetId,
                card = arrow.Card.Id.ToString(),
                targets = arrow.Targets.Select(target =>
                    Array.IndexOf(arrow.Card.Owner.Creature.CombatState!.Creatures.ToArray(), target)),
                arrow.Remaining,
                arrow.PlayCount,
                payloadType = AccessTools.Property(typeof(ArrowDeployment), "Payload").GetValue(arrow)!.GetType().FullName,
                state = JsonDocument.Parse((string)AccessTools.Method(typeof(ArrowHud), "ArrowPreviewState").Invoke(null, [arrow])!).RootElement
            })));
        if (args.FirstOrDefault() == "info") return new(true, JsonSerializer.Serialize(Snapshot()));
        if (args.FirstOrDefault() == "mutate")
        {
            var arrow = ArrowCombat.Deployed.OrderBy(a => a.Sequence).ElementAt(int.Parse(args[1]));
            int amount = int.Parse(args.ElementAtOrDefault(3) ?? "1");
            switch (args.ElementAtOrDefault(2))
            {
                case "waiting": AccessTools.Property(typeof(ArrowDeployment), "AttacksWhileWaiting").SetValue(arrow, amount); break;
                case "remaining": AccessTools.Property(typeof(ArrowDeployment), "Remaining").SetValue(arrow, amount); break;
                case "upgrade": arrow.Card.UpgradeInternal(); break;
                case "cost": CostChanges.SetFixed(arrow.Card, amount); break;
                case "repeat": AccessTools.Property(typeof(ArrowDeployment), "PlayCount").SetValue(arrow, amount); break;
                case "exhaust": AccessTools.Property(typeof(ArrowDeployment), "ExhaustAfterResolve").SetValue(arrow, amount != 0); break;
                case "keyword": arrow.Card.AddKeyword(CardKeyword.Retain); break;
                case "variable": arrow.Card.DynamicVars["Damage"].BaseValue = amount; break;
                case "shine": ShoujoKageki.Core.V1.Shine.SetState(arrow.Card, amount, 3); break;
                default: return new(false, "Unknown state");
            }
            return new(true, "Arrow state updated");
        }
        if (args.FirstOrDefault() == "input")
        {
            var viewport = NCombatRoom.Instance!.GetViewport();
            InputEvent input = args.ElementAtOrDefault(1) == "wheel"
                ? new InputEventMouseButton { Pressed = true,
                    ButtonIndex = args.ElementAtOrDefault(2) == "up" ? MouseButton.WheelUp : MouseButton.WheelDown,
                    Position = _pointer, GlobalPosition = _pointer }
                : new InputEventKey { Pressed = true, Keycode = args.ElementAtOrDefault(2) switch {
                    "left" => Key.Left, "right" => Key.Right, "up" => Key.Pageup, "down" => Key.Pagedown, _ => Key.Escape } };
            Callable.From(() => { if (input is InputEventKey) Input.ParseInputEvent(input); else viewport.PushInput(input, true); }).CallDeferred();
            return new(true, "Input queued");
        }
        if (args.FirstOrDefault() == "hover")
        {
            Hover(player, args.ElementAtOrDefault(1) ?? "enemy");
            return new(true, "Pointer updated");
        }
        int count = int.Parse(args.ElementAtOrDefault(1) ?? "64");
        return new(Run(player, count, args.FirstOrDefault() == "check", args.ElementAtOrDefault(2) == "player", args.ElementAtOrDefault(2) == "mixed", args.ElementAtOrDefault(2) == "interleaved"), true, "Hover density probe started");
    }

    private static async Task CheckLiveDamage(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.First();
        var card = CardCatalog.Create("JB03", player);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        await TestGameApi.PaidPlay(context, card, target);
        var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
        for (int attempt = 0; attempt < 20 && !SnapshotHasCard(); attempt++)
        {
            Hover(player, "enemy");
            await Task.Delay(150);
        }
        var preview = hud.GetNode<Control>("JunnaArrowHoverPreview").GetChildren().OfType<NCard>()
            .Single(node => node.IsVisibleInTree() && node.Model == card);
        var label = preview.GetNode<RichTextLabel>("%DescriptionLabel");
        var checks = new List<object>();
        void Check(int expected)
        {
            string value = label.Text;
            checks.Add(new { expected, value, pass = value.Contains(expected.ToString(), StringComparison.Ordinal) });
        }
        Check(25);
        await PowerCmd.Apply<StrengthPower>(context, player.Creature, 5, player.Creature, null);
        await Task.Delay(250);
        Check(30);
        await PowerCmd.Apply<WeakPower>(context, player.Creature, 1, target, null);
        await Task.Delay(250);
        Check(22);
        await PowerCmd.Apply<VulnerablePower>(context, target, 1, player.Creature, null);
        await Task.Delay(250);
        Check(33);
        await PowerCmd.Remove(player.Creature.GetPower<StrengthPower>()!);
        await Task.Delay(250);
        Check(28);
        string report = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory,
            "../../../out/tmp/arrow-live-damage-preview/hover.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(checks));

        bool SnapshotHasCard() => hud.GetNodeOrNull<Control>("JunnaArrowHoverPreview")?.GetChildren().OfType<NCard>()
            .Any(node => node.IsVisibleInTree() && node.Model == card) == true;
    }

    private static async Task Run(Player player, int count, bool check, bool self, bool mixed, bool interleaved)
    {
        foreach (var card in CardActions.All(player).ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
        var target = self ? player.Creature : player.Creature.CombatState!.HittableEnemies.First();
        await CreatureCmd.SetMaxHp(target, 100000);
        await CreatureCmd.SetCurrentHp(target, 100000);
        for (int i = 0; i < count + (mixed ? 1 : 0); i++)
        {
            var card = CardCatalog.Create((i == count || interleaved && (i == 1 || i == 4)) ? "JB03" : self ? "JH11" : "U_CARD_005", player);
            if (check && count > 1) card.DynamicVars.Damage.BaseValue += i + 1;
            var play = new CardPlay { Card = card, Target = target,
#if STS2_BETA
                Player = player,
#endif
                ResultPile = ArrowPile.PileId, Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            ArrowCombat.Deploy(card, play, 3, (_, _) => Task.CompletedTask);
            await CardPileCmd.Add(card, ArrowPile.PileId, skipVisuals: true);
        }
        await Task.Delay(350);
        Hover(player, self ? "player" : "enemy");
        await Task.Delay(350);
        var snapshot = Snapshot();
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/arrow-play-speed/hover-density"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "check-" + count + (self ? "-player" : "") + ".json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        if (!check) return;
        var room = NCombatRoom.Instance!;
        var hud = room.GetNode<ArrowHud>("JunnaArrows");
        var preview = (Control?)AccessTools.Field(typeof(ArrowHud), "_hoverPreview").GetValue(hud);
        var cards = preview?.GetChildren().OfType<NCard>().Where(card => card.IsVisibleInTree()).ToArray() ?? [];
        var canvas = hud.GetViewportRect();
        int Total() => ((ICollection)AccessTools.Field(typeof(ArrowHud), "_hoverArrows").GetValue(hud)!).Count;
        int Index() => (int)AccessTools.Field(typeof(ArrowHud), "_previewIndex").GetValue(hud)!;
        NCard[] Visible() => preview?.GetChildren().OfType<NCard>().Where(card => card.IsVisibleInTree()).ToArray() ?? [];
        bool visible = Total() == count && cards.Length == Math.Min(count, 5) && cards.All(card => card.IsVisibleInTree() && card.Modulate.A > .9f && card.Scale.X > 0);
        bool inside = cards.All(card => {
            var rect = card.GetNode<Control>("%Frame").GetGlobalRect();
            return canvas.Grow(1).Encloses(rect) && rect.Size.X > 10 && rect.Size.Y > 10;
        });
        bool distinct = cards.Select(card => card.Model).Distinct(ReferenceEqualityComparer.Instance).Count() == Math.Min(count, 5);
        var first = ArrowCombat.Deployed.First();
        await ArrowCombat.Accelerate(new ThrowingPlayerChoiceContext(), first, 1);
        await Task.Delay(100);
        var firstCard = cards.FirstOrDefault(card => ReferenceEquals(card.Model, first.Card));
        var presentation = firstCard?.Body.GetNode<ArrowCardPresentation>("JunnaArrowCard");
        var turns = presentation == null ? null : (Label)AccessTools.Field(typeof(ArrowCardPresentation), "_turns").GetValue(presentation)!;
        bool countdown = turns?.Text == "2";
        bool fan = cards.Length < 2 || cards.All(card => Math.Abs(card.Scale.X - .7f) < .001f)
            && cards.Any(card => Math.Abs(card.Rotation) > .001f);
        var viewport = hud.GetViewport();
        void Send(InputEvent input) => Callable.From(() => { if (input is InputEventKey) Input.ParseInputEvent(input); else viewport.PushInput(input, true); }).CallDeferred();
        var seen = new HashSet<CardModel>(ReferenceEqualityComparer.Instance) { first.Card };
        for (int i = 1; i < count; i++)
        {
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true,
                Position = _pointer, GlobalPosition = _pointer });
            await Task.Delay(40);
            var selected = (ArrowDeployment?)AccessTools.Field(typeof(ArrowHud), "_inspectedArrow").GetValue(hud);
            if (selected != null) seen.Add(selected.Card);
        }
        await Task.Delay(180);
        bool paging = seen.Count == count && Index() == count - 1;
        bool cacheBound = ((IDictionary)AccessTools.Field(typeof(ArrowHud), "_previewCache").GetValue(hud)!).Count <= 10;
        bool keyboard = true;
        int keyboardIndex = Index();
        if (count > 1)
        {
            Send(new InputEventKey { Keycode = Key.Left, Pressed = true });
            await Task.Delay(180);
            keyboardIndex = Index();
            keyboard = keyboardIndex == count - 2;
        }
        var oldBounds = preview!.GetGlobalRect();
        var inspected = (ArrowDeployment?)AccessTools.Field(typeof(ArrowHud), "_inspectedArrow").GetValue(hud);
        var neighbour = Visible().FirstOrDefault(card => !ReferenceEquals(card.Model, inspected?.Card));
        bool neighbourFocus = neighbour == null;
        if (neighbour != null && TryHover(neighbour.GetNode<Control>("ArrowPreviewHitbox")))
        {
            await Task.Delay(200);
            neighbourFocus = ReferenceEquals(((ArrowDeployment?)AccessTools.Field(typeof(ArrowHud), "_inspectedArrow").GetValue(hud))?.Card, neighbour.Model)
                && preview.GetGlobalRect() == oldBounds;
        }
        Hover(player, self ? "player" : "enemy");
        await Task.Delay(100);
        Send(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        await Task.Delay(100);
        bool escape = !preview.IsVisibleInTree();
        Hover(player, "clear");
        await Task.Delay(350);
        var frames = hud.FindChildren("TiltedFrame", "", true, false).OfType<Control>().ToArray();
        bool arrowHits = frames.Length == count && frames.All(TryHover);
        Hover(player, "clear");
        await Task.Delay(350);
        Hover(player, "arrow");
        await Task.Delay(100);
        bool single = Visible().Length == 1;
        Hover(player, "clear");
        await Task.Delay(350);
        bool cleared = !preview.IsVisibleInTree();
        Hover(player, self ? "player" : "enemy");
        await Task.Delay(200);
        ArrowCombat.Cancel(first.Card);
        await CardPileCmd.RemoveFromCombat(first.Card, skipVisuals: true);
        await Task.Delay(350);
        bool removed = Total() == count - 1 && Visible().Length == Math.Min(count - 1, 5)
            && !((IDictionary)AccessTools.Field(typeof(ArrowHud), "_previewCache").GetValue(hud)!).Contains(first);
        bool pass = visible && inside && distinct && countdown && fan && paging && cacheBound && keyboard
            && neighbourFocus && escape && arrowHits && single && cleared && removed;
        File.WriteAllText(Path.Combine(directory, "result-" + count + (self ? "-player" : "") + ".json"),
            JsonSerializer.Serialize(new { count, actual = cards.Length, visible, inside, distinct, countdown, fan, paging, cacheBound, keyboard, keyboardIndex, neighbourFocus, escape, arrowHits, single, cleared, removed, pass }, new JsonSerializerOptions { WriteIndented = true }));
        if (check && !pass) throw new InvalidOperationException("Arrow hover preview regression failed");
    }

    private static async Task CheckPlacement(Player player)
    {
        Hover(player, "clear");
        await Task.Delay(350);
        Hover(player, "enemy");
        await Task.Delay(700);
        var room = NCombatRoom.Instance!;
        var hud = room.GetNode<ArrowHud>("JunnaArrows");
        var preview = (Control)AccessTools.Field(typeof(ArrowHud), "_hoverPreview").GetValue(hud)!;
        var enemy = room.GetCreatureNode(player.Creature.CombatState!.HittableEnemies.First())!.Hitbox.GetGlobalRect();
        var rect = preview.GetGlobalRect();
        var tips = NGame.Instance!.HoverTipsContainer!.FindChildren("*", "", true, false).OfType<NHoverTipSet>()
            .Where(set => set.IsVisibleInTree()).SelectMany(set => set.GetChildren().OfType<Control>())
            .Where(control => control.IsVisibleInTree() && control.Size.X > 0 && control.Size.Y > 0)
            .Select(control => control.GetGlobalRect()).ToArray();
        bool visible = preview.IsVisibleInTree();
        bool aboveHead = rect.End.Y <= enemy.Position.Y - 8;
        bool centered = Math.Abs(rect.GetCenter().X - enemy.GetCenter().X) < 1;
        bool avoidsTips = tips.All(tip => !rect.Intersects(tip));
        bool inside = hud.GetViewportRect().Encloses(rect);
        var result = new { visible, aboveHead, centered, avoidsTips, inside, tipsCount = tips.Length,
            preview = rect.ToString(), enemy = enemy.ToString(), tips = tips.Select(tip => tip.ToString()),
            pass = visible && aboveHead && centered && avoidsTips && inside && tips.Length > 0 };
        string file = Path.GetFullPath("../../../out/tmp/main-consolidation-hover/placement.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Hover(Player player, string mode)
    {
        var room = NCombatRoom.Instance!;
        var hud = room.GetNodeOrNull<ArrowHud>("JunnaArrows");
        Control? anchor = mode switch {
            "player" => room.GetCreatureNode(player.Creature)!.Hitbox,
            "enemy" => room.GetCreatureNode(player.Creature.CombatState!.HittableEnemies.First())!.Hitbox,
            "arrow" => hud?.FindChildren("TiltedFrame", "", true, false).OfType<Control>().FirstOrDefault(),
            _ => null
        };
        var viewport = room.GetViewport();
        void Move(Vector2 point) { _pointer = point; viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true); }
        if (anchor != null && mode != "arrow")
        {
            // Dense arrow formations can cross the creature's center; hit a free part of its hitbox.
            foreach (float y in new[] { .9f, .5f, .1f, .75f, .25f })
                foreach (float x in new[] { .5f, .1f, .9f, .25f, .75f })
                {
                    Move(anchor.GetGlobalTransformWithCanvas() * (anchor.Size * new Vector2(x, y)));
                    var hovered = viewport.GuiGetHoveredControl();
                    if (hovered == anchor || hovered != null && anchor.IsAncestorOf(hovered)) return;
                }
            return;
        }
        if (anchor == null) Move(new Vector2(900, 900));
        else TryHover(anchor);
    }

    private static bool TryHover(Control anchor)
    {
        var viewport = anchor.GetViewport();
        foreach (float y in new[] { .5f, .1f, .9f, .25f, .75f, .02f, .98f })
            foreach (float x in new[] { .5f, .1f, .9f, .25f, .75f, .02f, .98f })
            {
                var point = anchor.GetGlobalTransformWithCanvas() * (anchor.Size * new Vector2(x, y));
                _pointer = point;
                viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
                var hovered = viewport.GuiGetHoveredControl();
                if (hovered == anchor || hovered != null && anchor.IsAncestorOf(hovered)) return true;
            }
        return false;
    }

    private static object Snapshot()
    {
        var room = NCombatRoom.Instance!;
        var hud = room.GetNodeOrNull<ArrowHud>("JunnaArrows");
        if (hud == null) return new { deployed = 0, cardCount = 0, physicalTotal = 0, total = 0,
            index = 0, counter = "", cache = 0, preview = (object?)null, cards = Array.Empty<object>() };
        var preview = (Control?)AccessTools.Field(typeof(ArrowHud), "_hoverPreview").GetValue(hud);
        var cards = preview?.GetChildren().OfType<NCard>().Where(card => card.IsVisibleInTree()).ToArray() ?? [];
        var canvas = hud.GetViewportRect();
        return new {
            deployed = ArrowCombat.Deployed.Count,
            hovered = hud.GetViewport().GuiGetHoveredControl()?.GetPath().ToString(),
            canvas = new { x = canvas.Position.X, y = canvas.Position.Y, w = canvas.Size.X, h = canvas.Size.Y },
            preview = preview == null ? null : new { position = preview.GlobalPosition.ToString(), size = preview.Size.ToString(), visible = preview.IsVisibleInTree() },
            cardCount = cards.Length,
            physicalTotal = ((ICollection)AccessTools.Field(typeof(ArrowHud), "_hoverCandidates").GetValue(hud)!).Count,
            total = ((ICollection)AccessTools.Field(typeof(ArrowHud), "_hoverArrows").GetValue(hud)!).Count,
            index = (int)AccessTools.Field(typeof(ArrowHud), "_previewIndex").GetValue(hud)!,
            counter = ((Label?)AccessTools.Field(typeof(ArrowHud), "_previewCount").GetValue(hud))?.Text,
            cache = ((IDictionary)AccessTools.Field(typeof(ArrowHud), "_previewCache").GetValue(hud)!).Count,
            cards = cards.Select(card => {
                var rect = card.GetNode<Control>("%Frame").GetGlobalRect();
                var quantity = card.GetNode<Label>("ArrowPreviewQuantity");
                var quantityRect = quantity.GetGlobalRect();
                return new { id = card.Model!.Id.Entry, upgrade = card.Model.CurrentUpgradeLevel,
                    quantity = quantity.Text, quantityVisible = quantity.Visible,
                    quantityInside = canvas.Grow(1).Encloses(quantityRect), quantityY = quantityRect.Position.Y,
                    quantityBottom = quantityRect.End.Y, position = card.GlobalPosition.ToString(), scale = card.Scale.ToString(), visible = card.IsVisibleInTree(), alpha = card.Modulate.A,
                    rect = new { x = rect.Position.X, y = rect.Position.Y, w = rect.Size.X, h = rect.Size.Y },
                    inside = canvas.Grow(1).Encloses(rect) };
            }).ToArray()
        };
    }
}
