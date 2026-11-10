using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using Godot;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Models;

namespace JunnaAncientPortraitProbe;

public sealed class AncientPortraitCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_ancient_art";
    public override string Args => "check | prepare";
    public override string Description => "Check both ancient cards through native card rendering.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        return new(args.Contains("prepare") ? Prepare(player) : Check(player), true, "Ancient portrait command started");
    }

    private static async Task Prepare(Player player)
    {
        foreach (var old in player.PlayerCombatState!.Hand.Cards.ToArray())
            await CardPileCmd.RemoveFromCombat(old);
        foreach (string id in new[] { "JZ03", "JK127", "JK126", "JK32", "JK32B", "JK32C", "JK122B" })
            await CardPileCmd.Add(CardCatalog.Create(id, player), PileType.Hand);
    }

    private static async Task Check(Player player)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/ancient-art"));
        Directory.CreateDirectory(directory);
        var rows = new List<object>();
        var nodes = new List<NCard>();
        try
        {
            foreach (string id in new[] { "JZ03", "JK127" })
            {
                var card = CardCatalog.Create(id, player);
                var node = NCard.Create(card)!;
                tree.Root.AddChild(node);
                node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
                node.Position = new Vector2(id == "JZ03" ? 420 : 870, 330);
                node.Scale = Vector2.One;
                nodes.Add(node);
                for (int i = 0; i < 3; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                string expected = $"res://ShoujoKagekiJunna/images/cards/{id}.jpg";
                var textures = node.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Select(rect => new
                {
                    name = rect.GetPath().ToString(),
                    visible = rect.IsVisibleInTree(),
                    texture = rect.Texture is AtlasTexture atlas ? atlas.Atlas?.ResourcePath : rect.Texture?.ResourcePath,
                    position = rect.Position.ToString(),
                    size = rect.Size.ToString(), stretch = rect.StretchMode.ToString(), sourceSize = rect.Texture?.GetSize().ToString()
                }).ToArray();
                rows.Add(new { id, expected, portrait = card.Portrait.ResourcePath,
                    pass = textures.Any(rect => rect.visible && rect.texture == expected && rect.stretch == "KeepAspectCovered"), textures });
            }
            RenderingServer.ForceDraw(false);
            tree.Root.GetTexture().GetImage().SavePng(Path.Combine(directory, "cards.png"));
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            foreach (var node in nodes) node.QueueFree();
        }
    }
}

[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Initialize))]
public static class Startup
{
    public static void Initialize()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => RenderingServer.ForceDraw(false);
    }
}
