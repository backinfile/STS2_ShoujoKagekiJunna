using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaCompatibilityProbe;

public sealed class ArrowPortraitCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_portrait";
    public override string Args => "check | prepare | hover";
    public override string Description => "Compare arrow portrait rendering with the source card image.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.Contains("hover"))
        {
            var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
            var frame = hud.GetChildren().OfType<Control>().First(child => child.HasNode("TiltedFrame/Portrait"))
                .GetNode<Control>("TiltedFrame");
            var position = frame.GetGlobalTransform() * (frame.Size * .5f);
            frame.GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
            return new(true, "Hovering the deployed arrow");
        }
        return new(args.Contains("prepare") ? Prepare(player) : Check(player), true, "Portrait check started");
    }

    private static async Task Prepare(Player player)
    {
        foreach (var card in CardActions.All(player).ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
        var source = ModelDb.AllCards.Single(card => card.Title == "呼唤");
        await CardPileCmd.Add(player.Creature.CombatState!.CreateCard(source, player), PileType.Hand);
        await CardPileCmd.Add(CardCatalog.Create("JA88", player), PileType.Hand);
        player.PlayerCombatState!.Energy = 20;
    }

    private static async Task Check(Player player)
    {
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/arrow-portrait"));
        Directory.CreateDirectory(directory);
        var rows = new List<object>();
        void Save() => File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        var tree = (SceneTree)Engine.GetMainLoop();
        async Task Frames()
        {
            for (int i = 0; i < 3; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
        }
        try
        {
            foreach (var old in ArrowCombat.Deployed.ToArray())
            {
                ArrowCombat.Cancel(old.Card);
                await CardPileCmd.RemoveFromCombat(old.Card, skipVisuals: true);
            }
            await CheckAtlasVariants(tree, rows, Save);
            var source = ModelDb.AllCards.Single(card => card.Title == "呼唤");
            foreach (var canonical in new[] { source, ModelDb.Card<JunnaStrike>() })
            {
                var card = player.Creature.CombatState!.CreateCard(canonical, player);
                await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
                await HandArrow.Play(new ThrowingPlayerChoiceContext(), card);
                await Frames();
                var node = NCard.Create(card)!;
                tree.Root.AddChild(node);
                node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
                node.Position = new Vector2(850, 320);
                node.Scale = Vector2.One * .8f;
                var cardPolygon = node.Body.GetNode<Polygon2D>("JunnaArrowCard/ArrowPortrait");
                var hud = NCombatRoom.Instance!.GetNode<ArrowHud>("JunnaArrows");
                var hudPolygon = hud.GetChildren().OfType<Control>().Last(child => child.HasNode("TiltedFrame/Portrait"))
                    .GetNode<Polygon2D>("TiltedFrame/Portrait");
                foreach (var (name, polygon) in new[] { ("card", cardPolygon), ("hud", hudPolygon) })
                {
                    var referenceViewport = new SubViewport { Size = (Vector2I)card.Portrait.GetSize(),
                        TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                    referenceViewport.AddChild(new TextureRect { Texture = card.Portrait, Size = card.Portrait.GetSize() });
                    tree.Root.AddChild(referenceViewport);
                    await Frames();
                    var reference = ImageTexture.CreateFromImage(referenceViewport.GetTexture().GetImage());
                    referenceViewport.QueueFree();
                    var actualViewport = Viewport(polygon, polygon.Texture);
                    var expectedViewport = Viewport(polygon, reference);
                    tree.Root.AddChild(actualViewport);
                    tree.Root.AddChild(expectedViewport);
                    await Frames();
                    var actual = actualViewport.GetTexture().GetImage();
                    var expected = expectedViewport.GetTexture().GetImage();
                    int changed = 0, visible = 0;
                    for (int y = 0; y < actual.GetHeight(); y++)
                    for (int x = 0; x < actual.GetWidth(); x++)
                    {
                        var a = actual.GetPixel(x, y);
                        var e = expected.GetPixel(x, y);
                        if (e.A < .5f) continue;
                        visible++;
                        if (Math.Abs(a.R - e.R) + Math.Abs(a.G - e.G) + Math.Abs(a.B - e.B) > .15f) changed++;
                    }
                    double difference = (double)changed / Math.Max(visible, 1);
                    rows.Add(new { card = card.Id.Entry, name, texture = card.Portrait.GetType().Name, difference,
                        visible, pass = visible > 100 && difference < .02 });
                    actual.SavePng(Path.Combine(directory, card.Id.Entry + "-" + name + "-actual.png"));
                    expected.SavePng(Path.Combine(directory, card.Id.Entry + "-" + name + "-expected.png"));
                    actualViewport.QueueFree();
                    expectedViewport.QueueFree();
                    Save();
                }
                node.QueueFree();
            }
        }
        catch (Exception error) { rows.Add(new { pass = false, error = error.ToString() }); }
        Save();
    }

    private static async Task CheckAtlasVariants(SceneTree tree, List<object> rows, Action save)
    {
        var resolve = typeof(ArrowHud).Assembly.GetType("ShoujoKagekiJunna.Combat.ArrowPortraitTexture")!
            .GetMethod("Resolve", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        using var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgb8);
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++) image.SetPixel(x, y, new Color(x / 31f, y / 31f, .3f));
        var source = ImageTexture.CreateFromImage(image);
        var padded = new AtlasTexture { Atlas = source, Region = new Rect2(4, 6, 20, 16), Margin = new Rect2(3, 2, 8, 6) };
        var nested = new AtlasTexture { Atlas = padded, Region = new Rect2(2, 1, 20, 15), Margin = new Rect2(1, 3, 4, 8) };
        var full = new AtlasTexture { Atlas = source };
        foreach (var (name, texture) in new[] { ("atlas margins", padded), ("nested atlas", nested), ("zero region", full) })
        {
            var flattened = (Texture2D)resolve.Invoke(null, [texture])!;
            var viewport = new SubViewport { Size = (Vector2I)texture.GetSize(), TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            viewport.AddChild(new TextureRect { Texture = texture, Size = texture.GetSize() });
            tree.Root.AddChild(viewport);
            for (int i = 0; i < 3; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var expected = viewport.GetTexture().GetImage();
            using var actual = flattened.GetImage();
            int changed = 0;
            for (int y = 0; y < actual.GetHeight(); y++)
            for (int x = 0; x < actual.GetWidth(); x++)
            {
                var a = actual.GetPixel(x, y);
                var e = expected.GetPixel(x, y);
                if (Math.Abs(a.A - e.A) > .02f || e.A > .5f &&
                    Math.Abs(a.R - e.R) + Math.Abs(a.G - e.G) + Math.Abs(a.B - e.B) > .03f) changed++;
            }
            rows.Add(new { name, changed, pass = changed == 0 });
            rows.Add(new { name = name + " cached", pass = ReferenceEquals(flattened, resolve.Invoke(null, [texture])) });
            viewport.QueueFree();
            save();
        }
        rows.Add(new { name = "ordinary texture reused", pass = ReferenceEquals(source, resolve.Invoke(null, [source])) });
        save();
    }
    private static SubViewport Viewport(Polygon2D source, Texture2D texture)
    {
        var points = source.Polygon;
        var minimum = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
        var maximum = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
        var size = maximum - minimum;
        float scale = 180 / Math.Max(size.X, size.Y);
        var viewport = new SubViewport { Size = new Vector2I(192, 192), TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        viewport.AddChild(new Polygon2D { Polygon = points, UV = source.UV, Texture = texture,
            Position = Vector2.One * 6 - minimum * scale, Scale = Vector2.One * scale, Antialiased = true });
        return viewport;
    }
}
