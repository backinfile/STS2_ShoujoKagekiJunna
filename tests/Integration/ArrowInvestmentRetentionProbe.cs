using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

[ModInitializer(nameof(Initialize))]
public static class InvestmentProbeStartup
{
    public static void Initialize()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => RenderingServer.ForceDraw(false);
    }
}

public sealed class ArrowInvestmentRetentionProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_bonus_retention";
    public override string Args => "check <phase>";
    public override string Description => "Check persistent Deliberation damage investment through deployment, resolution and replay.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player, args.ElementAtOrDefault(1) ?? "retention"), true, "Checking retained arrow bonuses");
    private static async Task Check(Player player, string phase)
    {
        string root = System.Environment.GetEnvironmentVariable("JUNNA_KNOWLEDGE_STACK_OUTPUT")!;
        var rows = new List<object>();
        void Expect(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.First(enemy => enemy.IsAlive);
        async Task<CardModel> Add(string id, bool upgraded = false)
        {
            var card = player.Creature.CombatState!.CreateCard(CardCatalog.Model(id), player);
            if (upgraded) card.UpgradeInternal();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            return card;
        }
        try
        {
            foreach (bool upgraded in new[] { false, true }) foreach (bool adapted in new[] { false, true })
            {
                foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
                foreach (var card in CardActions.All(player).ToArray()) { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
                foreach (var power in target.Powers.ToArray()) await PowerCmd.Remove(power);
                await CreatureCmd.SetMaxHp(target, 10000); await CreatureCmd.SetCurrentHp(target, 10000);
                await TestGameApi.LoseBlock(context, target, target.Block, null);
                player.PlayerCombatState!.Energy = 30;
                var deliberation = await Add("JK67", upgraded);
                await TestGameApi.PaidPlay(context, deliberation, target);
                var arrow = await Add(adapted ? "JB01" : "JB03");
                int printed = (int)arrow.DynamicVars.Damage.BaseValue;
                if (adapted) await HandArrow.Play(context, arrow); else await TestGameApi.PaidPlay(context, arrow, target);
                var powerModel = player.Creature.Powers.OfType<CatalogPower>().Single(power => power.DesignId == "JK67");
                await powerModel.BeforeSideTurnEnd(context, CombatSide.Player, [player.Creature]);
                decimal bonus = upgraded ? 3m : 2m;
                string tag = $"upgraded={upgraded} adapted={adapted}";
                Expect(tag + " turn-end bonus applied", DamageModifiers.CaptureDamageModifierSnapshot(arrow).DamageBonus, bonus);
                int hp = target.CurrentHp;
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(arrow)!, 1000);
                Expect(tag + " first resolution damage includes bonus", hp - target.CurrentHp, printed + (int)bonus);
                Expect(tag + " bonus retained after resolution", DamageModifiers.CaptureDamageModifierSnapshot(arrow).DamageBonus, bonus);
                Expect(tag + " original object in discard", arrow.Pile?.Type == PileType.Discard, true);
                await CardPileCmd.Add(arrow, PileType.Hand);
                hp = target.CurrentHp;
                await TestGameApi.PaidPlay(context, arrow, target);
                Expect(tag + " bonus retained after replay", DamageModifiers.CaptureDamageModifierSnapshot(arrow).DamageBonus, bonus);
                if (ArrowCombat.Find(arrow) is { } replay) await ArrowCombat.Accelerate(context, replay, 1000);
                Expect(tag + " second resolution damage includes bonus", hp - target.CurrentHp, printed + (int)bonus);
                Expect(tag + " bonus retained after second resolution", DamageModifiers.CaptureDamageModifierSnapshot(arrow).DamageBonus, bonus);
            }
        }
        catch (Exception error) { rows.Add(new { name = "probe exception", pass = false, error = error.ToString() }); }
        File.WriteAllText(Path.Combine(root, phase + ".json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
