using System.Text.Json;
using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

[ModInitializer(nameof(Initialize))]
public static class ProbeStartup
{
    public static void Initialize()
    {
        LoopSyncCommand.Initialize();
        FullCardAuditCommand.Initialize();
        KnowledgeCopyRewardCommand.Initialize();
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        Godot.RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), Godot.RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => Godot.RenderingServer.ForceDraw(false);
    }
}

// Deliberately lives in another assembly and is a Skill without DamageVar.
[CustomID("JUNNA_PROBE_DIRECT")]
[Pool(typeof(JunnaCardPool))]
public sealed class ExternalDirectCard() : CustomCardModel(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    public override string CustomPortraitPath => "res://ShoujoKagekiJunna/images/cards/JB01.jpg";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await TestGameApi.Damage(context, [play.Target!], 4, ValueProp.Move, Owner.Creature, this, play);
        await CreatureCmd.GainBlock(Owner.Creature, 7, ValueProp.Move, play);
    }
}

public sealed class JunnaProbeCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_probe";
    public override string Args => "run | invest <hand-index> <bonus> <hits> | prepare";
    public override string Description => "Scoped Junna integration checks in the isolated test game.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (args.FirstOrDefault() == "speed")
        {
            Godot.Engine.TimeScale = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1;
            return new(true, "Test animation speed updated");
        }
        if (args.FirstOrDefault() == "render")
        {
            return new(true, "Offscreen rendering enabled; window stays minimized");
        }
        if (args.FirstOrDefault() == "portrait")
        {
            var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
            var icons = tree.Root.FindChildren("*", "TextureRect", true, false).OfType<Godot.TextureRect>()
                .Where(t => t.GetPath().ToString().Contains("TopBar"))
                .Select(t => new { path = t.GetPath().ToString(), texture = t.Texture?.ResourcePath,
                    type = t.Texture?.GetType().Name, size = t.Size.ToString(), region = (t.Texture as Godot.AtlasTexture)?.Region.ToString() });
            return new(true, JsonSerializer.Serialize(icons));
        }
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "catalog") return new(CardPoolProbe.Run(player), true, "Card pool checks started");
        if (args.FirstOrDefault() == "catalog-all") return new(CardCompletionProbe.Run(player, only: args.Length > 1 ? string.Join(" ", args.Skip(1)) : null), true, "All card faces started");
        if (args.FirstOrDefault() == "catalog-edges") return new(CardCompletionProbe.Run(player, true, args.Length > 1 ? string.Join(" ", args.Skip(1)) : null), true, "Card edge cases started");
        if (args.FirstOrDefault() == "catalog-events") return new(CardPoolProbe.Run(player, true), true, "Card event checks started");
        if (args.FirstOrDefault() == "catalog-targeted") return new(CardPoolProbe.Run(player, true, "converted clones"), true, "Converted clone checks started");
        if (args.FirstOrDefault() == "arrows") return ArrowHudProbe.Process(player, args.Skip(1).ToArray());
        if (args.FirstOrDefault() == "duplicates") return new(CheckDuplicateTargets(player), true, "Duplicate target check started");
        return new(args.FirstOrDefault() == "prepare" ? Prepare(player) : CheckDuplicateTargets(player), true, "Probe started");
    }

    private static async Task CheckDuplicateTargets(Player player)
    {
        var state = player.Creature.CombatState!;
        var target = state.GetOpponentsOf(player.Creature).First();
        var card = state.CreateCard(ModelDb.Card<JunnaStrike>(), player);
        int before = target.CurrentHp;
        await TestGameApi.Damage(new ThrowingPlayerChoiceContext(), [target, target], 2,
            ValueProp.Unpowered, player.Creature, card, null);
        int actual = before - target.CurrentHp;
        System.IO.File.WriteAllText("D:/Github/STS2_ShoujoKagekiJunna/artifacts/dev-game/duplicate-targets.json",
            JsonSerializer.Serialize(new { name = "original repeated target entries preserved", actual, expected = 4, pass = actual == 4 }));
    }


    private static async Task Prepare(Player player)
    {
        foreach (var enemy in player.Creature.CombatState!.GetOpponentsOf(player.Creature))
        {
            await CreatureCmd.SetMaxHp(enemy, 10000);
            await CreatureCmd.SetCurrentHp(enemy, 10000);
        }
        await CreatureCmd.SetMaxHp(player.Creature, 1000);
        await CreatureCmd.SetCurrentHp(player.Creature, 1000);
    }
}
