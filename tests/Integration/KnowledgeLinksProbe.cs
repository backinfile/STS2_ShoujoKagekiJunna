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
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Entities.UI;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class KnowledgeLinksProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_links";
    public override string Args => "prepare | dense | dynamic | style <0-5> | geometry | info | check <count> | arrow-hover | arrow-unhover";
    public override string Description => "Focused knowledge hover-link checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() is "prepare" or "dense" or "dynamic")
            return new(Prepare(player, args[0] == "dynamic", args[0] == "dense"), true, "Link scenario prepared");
        if (args.FirstOrDefault() == "style")
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int style) || style is < 0 or > 5)
                return new(false, "Choose a ribbon style from 0 to 5");
            var property = AccessTools.Property(typeof(KnowledgeHoverLinks), "Style");
            property.SetValue(null, Enum.ToObject(property.PropertyType, style));
            return new(true, "Ribbon style updated");
        }
        var effect = NCombatRoom.Instance!.GetNode<KnowledgeHoverLinks>("JunnaKnowledgeLinks");
        if (args[0] is "arrow-hover" or "arrow-unhover")
        {
            var hud = NCombatRoom.Instance.GetNode<ArrowHud>("JunnaArrows");
            var frame = hud.FindChildren("TiltedFrame", "", true, false).OfType<Control>().ElementAt(args.Length > 1 ? int.Parse(args[1]) : 0);
            var point = args[0] == "arrow-hover" ? frame.GetGlobalTransformWithCanvas() * frame.PivotOffset : new Vector2(900, 700);
            frame.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            return new(true, "Arrow pointer updated");
        }
        var targets = (IList)AccessTools.Field(typeof(KnowledgeHoverLinks), "_targets").GetValue(effect)!;
        var source = (CardModel?)AccessTools.Field(typeof(KnowledgeHoverLinks), "_source").GetValue(effect);
        var border = (Vector2[])AccessTools.Field(typeof(KnowledgeHoverLinks), "_border").GetValue(effect)!;
        var endpoints = targets.Cast<object>().Select(target => (Vector2)AccessTools.Property(target.GetType(), "Point").GetValue(target)!).ToArray();
        var counts = targets.Cast<object>().Select(target => (int)AccessTools.Property(target.GetType(), "Count").GetValue(target)!).ToArray();
        var highlights = targets.Cast<object>().Select(target => (bool)AccessTools.Property(target.GetType(), "Highlight").GetValue(target)!).ToArray();
        var normals = targets.Cast<object>().Select(target => (Vector2)AccessTools.Property(target.GetType(), "Normal").GetValue(target)!).ToArray();
        bool linked = AccessTools.Property(typeof(KnowledgeHoverLinks), "Style").GetValue(null)!.ToString() == "Linked";
        var origin = (Vector2)AccessTools.Field(typeof(KnowledgeHoverLinks), "_origin").GetValue(effect)!;
        var outward = (Vector2)AccessTools.Field(typeof(KnowledgeHoverLinks), "_outward").GetValue(effect)!;
        var starts = endpoints.Select(end => linked ? origin : (Vector2)AccessTools.Method(typeof(KnowledgeHoverLinks), "BorderPoint")
            .Invoke(effect, [end, Vector2.Zero])!).ToArray();
        var widths = new[] { 0f, .75f, 1f }.Select(progress => (float)AccessTools.Method(typeof(KnowledgeHoverLinks), "RibbonHalfWidth")
            .Invoke(effect, [progress])! * 2).ToArray();
        if (args.FirstOrDefault() == "geometry")
        {
            for (int index = 0; index < endpoints.Length; index++)
            {
                var expected = linked ? (border[0] + border[1]) * .5f : Enumerable.Range(0, border.Length)
                    .Select(edge => Geometry2D.GetClosestPointToSegment(endpoints[index], border[edge], border[(edge + 1) % border.Length]))
                    .MinBy(point => point.DistanceSquaredTo(endpoints[index]));
                if (linked && source?.Pile?.Type == ArrowPile.PileId) expected = border[0];
                if (starts[index].DistanceTo(expected) > .05f) throw new InvalidOperationException("Origin does not match the selected source anchor");
            }
            if (linked) CheckReceivingPoints(effect, endpoints, normals, origin);
            if (widths[0] <= widths[1] || widths[1] != widths[2]) throw new InvalidOperationException("Ribbon does not narrow away from its source");
            var vertices = (IList)AccessTools.Field(typeof(KnowledgeHoverLinks), "_vertices").GetValue(effect)!;
            var colors = (IList)AccessTools.Field(typeof(KnowledgeHoverLinks), "_colors").GetValue(effect)!;
            var indices = (IList)AccessTools.Field(typeof(KnowledgeHoverLinks), "_indices").GetValue(effect)!;
            if (linked && targets.Count > 0)
            {
                if (!outward.IsFinite() || outward.Dot((origin - border.Aggregate(Vector2.Zero, (sum, point) => sum + point) / border.Length).Normalized()) < .99f)
                    throw new InvalidOperationException("Departure is not directed above the source card");
                const int bands = 5, segments = 32;
                int verticesPerRibbon = (segments + 1) * bands;
                for (int offset = 0; offset + bands + bands / 2 < vertices.Count; offset += verticesPerRibbon)
                {
                    int firstCenter = offset + bands / 2;
                    var first = (Vector3)vertices[firstCenter]!;
                    var next = (Vector3)vertices[firstCenter + bands]!;
                    if (new Vector2(next.X - first.X, next.Y - first.Y).Normalized().Dot(outward) < .96f)
                        throw new InvalidOperationException("Ribbon turns before departing upward");
                    int targetIndex = offset / verticesPerRibbon;
                    if (!highlights[targetIndex]) continue;
                    int lastCenter = firstCenter + segments * bands;
                    int middleCenter = firstCenter + segments / 2 * bands;
                    var last = (Vector3)vertices[lastCenter]!;
                    var beforeLast = (Vector3)vertices[lastCenter - bands]!;
                    var tailDirection = new Vector2(beforeLast.X - last.X, beforeLast.Y - last.Y).Normalized();
                    float receivingAlpha = ((Color)colors[lastCenter]!).A;
                    if (tailDirection.Dot(normals[targetIndex]) < .95f || receivingAlpha < .2f
                        || ((Color)colors[middleCenter]!).A >= receivingAlpha * .5f)
                        throw new InvalidOperationException("Receiving tail does not extend toward its source and fade toward the middle");
                }
            }
            if (vertices.Cast<Vector3>().Any(point => !point.IsFinite()) || colors.Cast<Color>().Any(color => !float.IsFinite(color.A))
                || indices.Cast<int>().Any(index => index < 0 || index >= vertices.Count))
                throw new InvalidOperationException("Ribbon mesh contains invalid geometry");
        }
        var result = new { count = targets.Count, source = source?.Id.Entry, widths, counts, highlights, normals = normals.Select(point => new { point.X, point.Y }).ToArray(), outward = new { outward.X, outward.Y },
            border = border.Select(point => new { point.X, point.Y }).ToArray(),
            starts = starts.Select(point => new { point.X, point.Y }).ToArray(),
            points = endpoints.Select(point => new { point.X, point.Y }).ToArray() };
        if (args[0] == "check" && targets.Count != int.Parse(args[1]))
            throw new InvalidOperationException(JsonSerializer.Serialize(result));
        return new(true, JsonSerializer.Serialize(result));
    }

    private static void CheckReceivingPoints(KnowledgeHoverLinks effect, Vector2[] endpoints, Vector2[] normals, Vector2 origin)
    {
        var room = NCombatRoom.Instance!;
        var frames = room.Ui.Hand.ActiveHolders
            .Where(holder => holder.CardNode is { Visibility: ModelVisibility.Visible })
            .Select(holder => (Frame: holder.CardNode!.GetNode<Control>("%Frame"), Arrow: false)).ToList();
        if (room.GetNodeOrNull<ArrowHud>("JunnaArrows") is { } arrowHud)
            frames.AddRange(arrowHud.FindChildren("TiltedFrame", "", true, false)
                .OfType<Control>().Where(frame => frame.IsVisibleInTree()).Select(frame => (frame, true)));
        var pileIcons = new[] { room.Ui.DrawPile.GetNode<TextureRect>("Icon"),
            room.Ui.DiscardPile.GetNode<TextureRect>("Icon"), room.Ui.ExhaustPile.GetNode<TextureRect>("Icon") };
        var transform = effect.GetGlobalTransformWithCanvas().AffineInverse();
        var outlines = frames.Select(pair =>
        {
            Vector2[] corners = pair.Arrow
                ? [new(.5f, 2f / 416), new(238f / 240, 134f / 416), new(.5f, 413f / 416), new(2f / 240, 134f / 416)]
                : [Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down];
            return corners.Select(point => transform * (pair.Frame.GetGlobalTransformWithCanvas() * (point * pair.Frame.Size))).ToArray();
        }).ToArray();
        float gap = (float)AccessTools.Field(typeof(KnowledgeHoverLinks), "ReceiverGap").GetRawConstantValue()!;
        bool OnPileBorder(Vector2 anchor, Vector2 normal)
        {
            foreach (var icon in pileIcons)
            {
                using var image = icon.Texture.GetImage();
                if (image.IsCompressed()) image.Decompress();
                var size = icon.Texture.GetSize();
                float scale = Math.Min(icon.Size.X / size.X, icon.Size.Y / size.Y);
                var offset = (icon.Size - size * scale) * .5f;
                var toIcon = icon.GetGlobalTransformWithCanvas().AffineInverse() * effect.GetGlobalTransformWithCanvas();
                float Alpha(Vector2 point)
                {
                    var pixel = (toIcon * point - offset) / scale;
                    return pixel.X >= 0 && pixel.Y >= 0 && pixel.X < image.GetWidth() && pixel.Y < image.GetHeight()
                        ? image.GetPixel((int)pixel.X, (int)pixel.Y).A : 0;
                }
                if (Alpha(anchor - normal * 3) >= .5f && Alpha(anchor + normal * 3) < .5f) return true;
            }
            return false;
        }
        for (int index = 0; index < endpoints.Length; index++)
        {
            var point = endpoints[index];
            var normal = normals[index];
            var anchor = point - normal * gap;
            if (!normal.IsFinite() || Math.Abs(normal.Length() - 1) > .001f)
                throw new InvalidOperationException("Receiving direction is invalid");
            bool attached = outlines.Any(outline => !Geometry2D.IsPointInPolygon(point, outline)
                && Enumerable.Range(0, outline.Length).Any(edge => Geometry2D.GetClosestPointToSegment(anchor,
                    outline[edge], outline[(edge + 1) % outline.Length]).DistanceTo(anchor) < .05f));
            attached |= OnPileBorder(anchor, normal);
            if (!attached || normal.Dot((origin - anchor).Normalized()) < .99f)
                throw new InvalidOperationException("Receiving point is not outside a target on its source-facing side");
        }
    }

    private static async Task Prepare(Player player, bool dynamic, bool dense)
    {
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card);
        }
        foreach (var enemy in player.Creature.CombatState!.GetOpponentsOf(player.Creature))
        {
            await CreatureCmd.SetMaxHp(enemy, 10000);
            await CreatureCmd.SetCurrentHp(enemy, 10000);
        }
        if (dynamic)
        {
            var entrance = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
            var strike = player.RunState.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
            var defend = player.RunState.CreateCard(ModelDb.Card<DefendIronclad>(), player);
            await DynamicKnowledge.Prepare(new ThrowingPlayerChoiceContext(), entrance, [entrance, strike, defend]);
            await CardPileCmd.Add(entrance, PileType.Deck, skipVisuals: true);
            using var single = KnowledgeCombat.SingleCard();
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JK126", player), PileType.Hand, player);
            await CardPileCmd.AddGeneratedCardToCombat(player.Creature.CombatState.CreateCard(ModelDb.Card<StrikeIronclad>(), player), PileType.Hand, player);
            await CardPileCmd.AddGeneratedCardToCombat(player.Creature.CombatState.CreateCard(ModelDb.Card<DefendIronclad>(), player), PileType.Draw, player);
        }
        else
        {
            using var single = KnowledgeCombat.SingleCard();
            foreach (var (id, pile) in new[] { ("JK32", PileType.Hand), ("JK32B", PileType.Hand),
                         ("JK32C", PileType.Hand), ("JK32B", PileType.Draw), ("JK32C", PileType.Draw),
                         ("JK32B", PileType.Discard), ("JK32C", PileType.Exhaust),
                         ("JK133", PileType.Hand), ("JK133", PileType.Hand) })
                await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), pile, player);
        }
        if (dense)
        {
            using var single = KnowledgeCombat.SingleCard();
            for (int index = 0; index < 7; index++)
                await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JK32B", player), PileType.Draw, player);
            for (int index = 0; index < 2; index++)
                await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JK32B", player), PileType.Hand, player);
            await CardPileCmd.AddGeneratedCardToCombat(player.Creature.CombatState.CreateCard(ModelDb.Card<StrikeIronclad>(), player), PileType.Draw, player);
            await CardPileCmd.AddGeneratedCardToCombat(player.Creature.CombatState.CreateCard(ModelDb.Card<StrikeIronclad>(), player), PileType.Discard, player);
        }
        var ui = NCombatRoom.Instance!.Ui;
        ui.DrawPile.Initialize(player);
        ui.DiscardPile.Initialize(player);
        ui.ExhaustPile.Initialize(player);
        await PlayerCmd.SetEnergy(10, player);
    }
}
