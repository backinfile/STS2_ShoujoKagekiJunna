using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class PowerBlockPreviewProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_power_block_preview";
    public override string Args => "check | prepare";
    public override string Description => "Verify Oath's hand preview and triggered block under Frail.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(Run(player, args.FirstOrDefault() == "prepare"), true, "Started");

    private static async Task Run(Player player, bool prepare)
    {
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var target = state.HittableEnemies.First();
        var rows = new List<object>();
        void Equal(string name, decimal actual, decimal expected) =>
            rows.Add(new { name, actual, expected, pass = actual == expected });
        async Task Reset()
        {
            foreach (var card in CardActions.All(player).ToArray())
            {
                ArrowCombat.Cancel(card);
                await CardPileCmd.RemoveFromCombat(card, skipVisuals: !prepare);
            }
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState!.Energy = 20;
        }
        async Task<CardModel> Add(string id, bool upgraded = false)
        {
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom, skipVisuals: !prepare);
            return card;
        }
        decimal Preview(CardModel card)
        {
            card.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, card.DynamicVars);
            return card.DynamicVars.Block.PreviewValue;
        }
        double speed = Godot.Engine.TimeScale;
        if (!prepare) Godot.Engine.TimeScale = 8;
        try
        {
            if (prepare)
            {
                await Reset();
                await PowerCmd.Apply<FrailPower>(context, player.Creature, 3, target, null);
                await Add("JA118");
                await Add("JA118", true);
                await Add("JB02");
                await Add("JB03");
                return;
            }
            foreach (bool upgraded in new[] { false, true })
            foreach (string modifier in new[] { "none", "frail", "dexterity", "frail+dexterity" })
            {
                await Reset();
                var oath = await Add("JA118", upgraded);
                decimal expected = upgraded ? 3 : 2;
                Equal($"upgraded={upgraded} {modifier} before modifier", Preview(oath), expected);
                if (modifier.Contains("frail"))
                    await PowerCmd.Apply<FrailPower>(context, player.Creature, 2, target, null);
                if (modifier.Contains("dexterity"))
                    await PowerCmd.Apply<DexterityPower>(context, player.Creature, 3, player.Creature, null);
                Equal($"upgraded={upgraded} {modifier} hand preview", Preview(oath), expected);
                Equal($"upgraded={upgraded} {modifier} printed value", oath.DynamicVars.Block.BaseValue, expected);
                var defend = await Add("JB02");
                decimal defendExpected = modifier switch
                {
                    "frail" => 3.75m, "dexterity" => 8m, "frail+dexterity" => 6m, _ => 5m
                };
                Equal($"upgraded={upgraded} {modifier} normal block preview", Preview(defend), defendExpected);
                await TestGameApi.PaidPlay(context, oath);
                Equal($"upgraded={upgraded} {modifier} installed power", player.Creature.GetPower<JunnaPowerJA118>()!.Amount, expected);
                var arrow = await Add("JB03");
                await TestGameApi.PaidPlay(context, arrow, target);
                Equal($"upgraded={upgraded} {modifier} triggered block", player.Creature.Block, expected);
            }
            // Skills may install an ongoing effect while their immediate block remains powered.
            foreach (string id in new[] { "JH19", "JH24" })
            foreach (bool upgraded in new[] { false, true })
            {
                await Reset();
                await PowerCmd.Apply<FrailPower>(context, player.Creature, 2, target, null);
                await PowerCmd.Apply<DexterityPower>(context, player.Creature, 3, player.Creature, null);
                var skill = await Add(id, upgraded);
                decimal expected = (skill.DynamicVars.Block.BaseValue + 3) * .75m;
                Equal($"{id} upgraded={upgraded} immediate block preview", Preview(skill), expected);
                await TestGameApi.PaidPlay(context, skill);
                Equal($"{id} upgraded={upgraded} immediate block received", player.Creature.Block, Math.Floor(expected));
            }
        }
        catch (Exception error) { rows.Add(new { name = "probe exception", pass = false, error = error.ToString() }); }
        finally { Godot.Engine.TimeScale = speed; }
        string report = Path.GetFullPath("../../../out/tmp/fix-ja118-block-preview-20261117/results.json");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}