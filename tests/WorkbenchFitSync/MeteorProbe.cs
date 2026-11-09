using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class MeteorGenerationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_meteor_fix";
    public override string Args => "check | prepare";
    public override string Description => "Checks generated card versions and shared improvisation limits.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly string ResultPath = Path.GetFullPath("../../../out/tmp/meteor-fix/results.json", Environment.CurrentDirectory);
    private static readonly System.Reflection.FieldInfo Turn = AccessTools.Field(typeof(PlayerCombatState), "<TurnNumber>k__BackingField");
    private static async Task Reset(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 100000); await CreatureCmd.SetCurrentHp(enemy, 100000);
            await CreatureCmd.LoseBlock(Context, enemy, enemy.Block, null);
        }
        Turn.SetValue(player.PlayerCombatState, player.PlayerCombatState.TurnNumber + 1);
        player.PlayerCombatState.Energy = 99;
    }
    private static async Task<CardModel> Card(Player player, string id, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        return card;
    }
    private static Task<bool> Play(CardModel card) => CardActions.PaidPlay(Context, card, card.Owner.Creature.CombatState!.Enemies.First());
    private static ArrowDeployment[] Meteors(Player player) => ArrowCombat.Deployed
        .Where(a => a.Card.Owner == player && CardCatalog.DesignId(a.Card) == "JA81B").ToArray();
    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        double speed = Godot.Engine.TimeScale;
        int originalTurn = player.PlayerCombatState!.TurnNumber;
        Godot.Engine.TimeScale = 3;
        void Save() => File.WriteAllText(ResultPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        void Expect(string name, object actual, object expected)
        { rows.Add(new { name, actual, expected, pass = Equals(actual, expected) }); Save(); }
        async Task Case(string name, Func<Task> test)
        {
            try { await Reset(player); await test(); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); Save(); }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
        try
        {
            foreach (bool upgraded in new[] { false, true })
            {
                foreach (var id in new[] { "JA81", "JA82", "JA76", "JZ05" })
                    await Case(id + " generation " + upgraded, async () =>
                    {
                        var source = await Card(player, id, upgraded);
                        if (id == "JA82") player.PlayerCombatState.Energy = 3;
                        await Play(source);
                        if (id == "JZ05") await Play(await Card(player, "JB01"));
                        var meteors = Meteors(player);
                        int count = id == "JA82" ? 3 : id == "JA76" ? player.Creature.CombatState!.Enemies.Count() : 1;
                        int damage = id == "JA82" ? upgraded ? 18 : 14 : id == "JA81" ? upgraded ? 14 : 10 : id == "JA76" ? upgraded ? 16 : 12 : 6;
                        Expect(id + " count " + upgraded, meteors.Length, count);
                        Expect(id + " base generated version " + upgraded, meteors.All(a => !a.Card.IsUpgraded), true);
                        Expect(id + " two-turn deployment " + upgraded, meteors.All(a => a.Remaining == 2), true);
                        Expect(id + " recorded damage " + upgraded, meteors.All(a => a.Card.DynamicVars.Damage.IntValue == damage), true);
                    });
                foreach (var id in new[] { "JA76", "JA81", "JA82" })
                    await Case(id + " preview " + upgraded, async () =>
                    {
                        var card = await Card(player, id, upgraded);
                        var preview = card.HoverTips.OfType<CardHoverTip>().Select(t => t.Card).Single(c => CardCatalog.DesignId(c) == "JA81B");
                        Expect(id + " preview base version " + upgraded, preview.IsUpgraded, false);
                        Expect(id + " preview two turns " + upgraded, HandArrow.Turns(preview), 2);
                    });
                await Case("Explicit generated upgrade " + upgraded, async () =>
                {
                    await Play(await Card(player, "JK46", upgraded));
                    var reading = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK46");
                    await reading.AfterPlayerTurnStart(Context, player);
                    var generated = player.PlayerCombatState.Hand.Cards.Single(c => CardCatalog.DesignId(c) == "JK46B");
                    Expect("Explicit reading version " + upgraded, generated.IsUpgraded, upgraded);
                });
            }
            foreach (var upgrades in new[] { new[] { false, false }, new[] { false, true }, new[] { true, false }, new[] { true, true } })
                await Case("Stacked improvisation " + string.Join(',', upgrades), async () =>
                {
                    foreach (bool upgraded in upgrades) await Play(await Card(player, "JZ05", upgraded));
                    int limit = upgrades.Sum(upgraded => upgraded ? 2 : 1);
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JZ05");
                    Expect("Stack limit counter " + string.Join(',', upgrades), power.Amount, limit);
                    var text = power.SmartDescription; power.DynamicVars.AddTo(text);
                    string description = Regex.Replace(text.GetFormattedText(), @"\[[^\]]+\]", "");
                    Expect("Stack limit description " + string.Join(',', upgrades), description.Contains($"前{limit}次") && description.Contains("打出1张"), true);
                    for (int hit = 1; hit <= limit + 1; hit++)
                    {
                        await Play(await Card(player, "JB01"));
                        Expect("One meteor per event " + string.Join(',', upgrades) + " event " + hit, Meteors(player).Length, Math.Min(hit, limit));
                    }
                    Expect("Stacked meteor versions " + string.Join(',', upgrades), Meteors(player).All(a => !a.Card.IsUpgraded && a.Remaining == 2), true);
                    Turn.SetValue(player.PlayerCombatState, player.PlayerCombatState.TurnNumber + 1);
                    await Play(await Card(player, "JB01"));
                    Expect("Next turn refresh " + string.Join(',', upgrades), Meteors(player).Length, limit + 1);
                });
            await Case("Install respects prior attack history", async () =>
            {
                await Play(await Card(player, "JB01"));
                await Play(await Card(player, "JZ05"));
                await Play(await Card(player, "JB01"));
                Expect("Installing does not reset history", Meteors(player).Length, 0);
            });
            await Case("Delayed meteor damage shares event cap", async () =>
            {
                await Play(await Card(player, "JZ05")); await Play(await Card(player, "JZ05"));
                await Play(await Card(player, "JB01"));
                var first = Meteors(player).First(); await ArrowCombat.Accelerate(Context, first, int.MaxValue);
                Expect("Completion produces one remaining child", Meteors(player).Length, 1);
                var second = Meteors(player).First(); await ArrowCombat.Accelerate(Context, second, int.MaxValue);
                Expect("Third damage event is over cap", Meteors(player).Length, 0);
            });
            await Case("Explicit meteor upgrade remains supported", async () =>
            {
                await CardActions.Fly(Context, player, 7, player.Creature.CombatState!.Enemies.First(), upgraded: true);
                Expect("Explicit upgraded meteor takes one turn", Meteors(player).Single().Remaining, 1);
            });
        }
        finally
        {
            Godot.Engine.TimeScale = speed; Turn.SetValue(player.PlayerCombatState, originalTurn);
            File.WriteAllText(Path.ChangeExtension(ResultPath, ".completed"), "completed");
        }
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player); player.PlayerCombatState!.Energy = 9;
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 300); await CreatureCmd.SetCurrentHp(enemy, 300); }
        await Card(player, "JZ05"); await Card(player, "JZ05", true); await Card(player, "JA81", true);
        for (int i = 0; i < 4; i++) await Card(player, "JB01");
    }
}
