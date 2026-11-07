using System.Text.Json;
using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

// A group arrow lives only in the isolated probe mod; it exercises the public deployment path.
[CustomID("JUNNA_PROBE_GROUP_ARROW")]
[Pool(typeof(JunnaCardPool))]
public sealed class GroupArrowProbe() : CustomCardModel(0, ArrowPile.ArrowType, CardRarity.Common, TargetType.AllEnemies)
{
    public override string CustomPortraitPath => "res://ShoujoKagekiJunna/images/floating_arrows/JUNNA_FULL_DRAW.png";
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}

// Exercises the shared ally-target layout through normal card play.
[CustomID("JUNNA_PROBE_ALLIES_ARROW")]
[Pool(typeof(JunnaCardPool))]
public sealed class AllAlliesArrowProbe() : CustomCardModel(0, ArrowPile.ArrowType, CardRarity.Common, TargetType.AllAllies)
{
    public override string CustomPortraitPath => "res://ShoujoKagekiJunna/images/floating_arrows/JUNNA_FULL_DRAW.png";
    public override List<(string, string)> Localization => new CardLoc("\u53cb\u65b9\u7bad\u77e2", "");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Turns", 3)];
    protected override CardLocation GetResultLocationForCardPlay() => new(Owner, ArrowPile.PileId, CardPilePosition.Bottom);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArrowCombat.Deploy(this, play, 3, (_, _) => Task.CompletedTask);
        await CardPileCmd.Add(this, ArrowPile.PileId);
    }
}

public static class ArrowHudProbe
{
    public static CmdResult Process(Player player, string[] args)
    {
        if (args.FirstOrDefault() == "style")
        {
            if (!int.TryParse(args.ElementAtOrDefault(1), out int style) || style < 0 || style > 3)
                return new(false, "Choose a trajectory from 0 to 3");
            AccessTools.Property(typeof(ArrowDeploymentVfx), "PreviewTrajectory").SetValue(null, style);
            return new(true, $"Arrow trajectory {style}");
        }
        if (args.FirstOrDefault() == "info")
        {
            var room = NCombatRoom.Instance!;
            var hud = room.GetNodeOrNull<ArrowHud>("JunnaArrows");
            var orb = NOrb.Create(true);
            room.AddChild(orb);
            var bounds = orb.GetNode<Control>("Bounds");
            var outline = orb.GetNode<TextureRect>("%Outline");
            var result = new
            {
                canvas = room.GetViewportRect().Size.ToString(),
                relicRow = NRun.Instance!.GlobalUi.RelicInventory.RelicNodes.Take(1).Select(n => new
                {
                    center = n.GetGlobalRect().GetCenter().ToString(), size = n.Size.ToString(),
                    separation = NRun.Instance.GlobalUi.RelicInventory.GetThemeConstant("v_separation", "FlowContainer")
                }).ToArray(),
                orb = new { size = orb.Size.ToString(), scale = orb.Scale.ToString(), bounds = bounds.Size.ToString(),
                    outline = outline.Size.ToString(), outlineScale = outline.Scale.ToString() },
                slots = hud == null ? null : ((Dictionary<ArrowDeployment, Vector2>)AccessTools.Field(typeof(ArrowHud), "_slots").GetValue(hud)!)
                    .Select(pair => new { id = pair.Key.Card.Id.Entry, x = pair.Value.X, y = pair.Value.Y, category = pair.Key.TargetCategory,
                        targetX = pair.Key.Target == null ? (float?)null : room.GetCreatureNode(pair.Key.Target)?.VfxSpawnPosition.X }).ToArray(),
                arrows = hud?.GetChildren().OfType<Control>().Where(n => n.HasNode("TiltedFrame")).Select(n => new
                {
                    position = n.Position.ToString(), size = n.Size.ToString(),
                    angle = n.GetNode<Control>("TiltedFrame").Rotation,
                    count = n.GetNode<Label>("Countdown").Text
                }).ToArray(),
                deployed = ArrowCombat.Deployed.Select(a => new { id = a.Card.Id.Entry, a.Remaining, a.TargetsAllEnemies }).ToArray(),
                creatures = room.CreatureNodes.Select(n => new { name = n.Entity.ModelId.Entry,
                    enemy = n.Entity.IsEnemy, x = n.VfxSpawnPosition.X, y = n.VfxSpawnPosition.Y,
                    position = n.VfxSpawnPosition.ToString(), hitbox = n.Hitbox.GetGlobalRect().ToString() }).ToArray()
            };
            orb.QueueFree();
            return new(true, JsonSerializer.Serialize(result));
        }
        return new(Prepare(player, args.FirstOrDefault() ?? "mixed"), true, "Arrow scene prepared");
    }

    private static async Task Prepare(Player player, string scenario)
    {
        foreach (var old in ArrowCombat.Deployed.ToArray()) await CardPileCmd.Add(old.Card, PileType.Discard);
        AccessTools.Method(typeof(ArrowCombat), "Clear").Invoke(null, null);
        if (scenario == "clear") return;
        // Preview the current source SVG directly, without building a resource pack.
        var room = NCombatRoom.Instance!;
        var hud = room.GetNodeOrNull<ArrowHud>("JunnaArrows");
        if (hud == null) room.AddChild(hud = new ArrowHud { Name = "JunnaArrows" });
        foreach (var (field, file) in new[] { ("_frame", "floating_arrow_c1.svg"), ("_groupFrame", "floating_arrow_group_a.svg") })
        {
            using var frameImage = new Image();
            frameImage.LoadSvgFromString(System.IO.File.ReadAllText(
                "D:/Github/STS2_ShoujoKagekiJunna/ShoujoKagekiJunna/images/" + file), 2);
            AccessTools.Field(typeof(ArrowHud), field).SetValue(hud, ImageTexture.CreateFromImage(frameImage));
        }
        var state = player.Creature.CombatState!;
        var targets = state.GetOpponentsOf(player.Creature).ToArray();
        int count = int.TryParse(scenario, out var requested) ? requested : 3;
        for (int i = 0; i < count; i++)
        {
            bool group = scenario == "mixed" && i == 0 || scenario == "group";
            CardModel card = state.CreateCard(group ? ModelDb.Card<GroupArrowProbe>() : ModelDb.Card<JunnaFullDraw>(), player);
            var play = new CardPlay { Card = card, Player = player, Target = group ? null : targets[i % targets.Length],
                ResultPile = ArrowPile.PileId, Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            await CardPileCmd.Add(card, ArrowPile.PileId);
            if (group) ArrowCombat.Deploy(card, play, 3, (context, original) =>
                DamageCmd.Attack(1).FromCard(card, original).TargetingAllOpponents(player.Creature.CombatState!).Execute(context));
            else ArrowCombat.Deploy((JunnaFullDraw)card, play, 3);
        }
    }
}
