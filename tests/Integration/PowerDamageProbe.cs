using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class PowerDamageProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_power_damage";
    public override string Args => "check | prepare <none|strength|vulnerable|weak|investment|block|combined>";
    public override string Description => "Verify triggered power damage, previews and normal attack modifiers.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        return args.FirstOrDefault() == "prepare"
            ? new(Prepare(player, args.ElementAtOrDefault(1) ?? "combined"), true, "Textbook fixture preparing")
            : new(Check(player), true, "Power damage check started");
    }

    private static async Task<(CatalogCard Book, CatalogCard Trigger, Creature Target)> Prepare(Player player, string scenario,
        string cardId = "JK235")
    {
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.Single();
        foreach (var creature in new[] { player.Creature, target })
        {
            foreach (var power in creature.Powers.ToArray()) await PowerCmd.Remove(power);
            await TestGameApi.LoseBlock(context, creature, creature.Block, null);
        }
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card);
        }
        await CreatureCmd.SetMaxHp(target, 10000);
        await CreatureCmd.SetCurrentHp(target, 10000);
        await PlayerCmd.SetEnergy(10, player);
        if (scenario is "strength" or "investment" or "combined")
            await PowerCmd.Apply<StrengthPower>(context, player.Creature, 9, player.Creature, null);
        if (scenario is "vulnerable" or "investment" or "combined")
            await PowerCmd.Apply<VulnerablePower>(context, target, 1, player.Creature, null);
        if (scenario is "weak" or "investment" or "combined")
            await PowerCmd.Apply<WeakPower>(context, player.Creature, 1, target, null);
        if (scenario == "block") await CreatureCmd.GainBlock(target, 2, ValueProp.Unpowered, null);
        using var single = KnowledgeCombat.SingleCard();
        var book = (CatalogCard)CardCatalog.Create(cardId, player);
        if (scenario == "investment") DamageModifiers.Add(book, damageBonus: 2);
        var trigger = (CatalogCard)CardCatalog.Create("JK32C", player);
        await CardPileCmd.AddGeneratedCardToCombat(book, PileType.Hand, player);
        await CardPileCmd.AddGeneratedCardToCombat(trigger, PileType.Hand, player);
        return (book, trigger, target);
    }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        string file = Path.GetFullPath("../../../out/tmp/power-damage-isolation/results.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var history = typeof(CardActions).Assembly.GetType("ShoujoKagekiJunna.Combat.AttackEventHistory")!;
        int Attacks() => (int)AccessTools.Method(history, "Count").Invoke(null, [player])!;
        foreach (string ability in new[] { "JK235", "JG34", "legacy-energy" })
        foreach (string scenario in new[] { "none", "strength", "vulnerable", "weak", "investment", "block" })
        {
            var (book, trigger, target) = await Prepare(player, scenario, ability == "JG34" ? "JG34" : "JK235");
            book.UpdateDynamicVarPreview(CardPreviewMode.Normal, null, book.DynamicVars);
            decimal? preview = ability == "legacy-energy" ? null : book.DynamicVars.Damage.PreviewValue;
            int expectedDamage = (ability == "legacy-energy" ? 2 : 3) + (scenario == "investment" ? 2 : 0);
            var context = new ThrowingPlayerChoiceContext();
            await TestGameApi.PaidPlay(context, book, player.Creature);
            int before = target.CurrentHp;
            int attacks = Attacks();
            var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == book.DesignId);
            if (ability == "JG34") await power.AfterPlayerTurnStart(context, player);
            else if (ability == "legacy-energy")
                await (Task)AccessTools.Method(typeof(CatalogPower), "EnergyDamage").Invoke(power, [context, 2])!;
            else await TestGameApi.PaidPlay(context, trigger, player.Creature);
            int hpLoss = before - target.CurrentHp;
            int expectedLoss = scenario == "block" ? Math.Max(0, expectedDamage - 2) : expectedDamage;
            int attackEvents = Attacks() - attacks;
            rows.Add(new { ability, scenario, preview, expectedDamage, hpLoss, expectedLoss, attackEvents,
                blockRemaining = target.Block,
                pass = (preview == null || preview == expectedDamage) && hpLoss == expectedLoss
                    && target.Block == 0 && attackEvents == 0 });
            File.WriteAllText(file, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        foreach (string cardId in new[] { "JB01", "JB03" })
        {
            var (book, trigger, target) = await Prepare(player, "combined");
            foreach (var card in new CardModel[] { book, trigger }) await CardPileCmd.RemoveFromCombat(card);
            var attack = CardCatalog.Create(cardId, player);
            await CardPileCmd.AddGeneratedCardToCombat(attack, PileType.Hand, player);
            int hp = target.CurrentHp, attacks = Attacks();
            var context = new ThrowingPlayerChoiceContext();
            await TestGameApi.PaidPlay(context, attack, target);
            if (ArrowCombat.Find(attack) is { } arrow) await ArrowCombat.Accelerate(context, arrow, int.MaxValue);
            int hpLoss = hp - target.CurrentHp;
            int expected = cardId == "JB01" ? 16 : 38;
            int attackEvents = Attacks() - attacks;
            rows.Add(new { ability = cardId, scenario = "attack-control", hpLoss, expectedLoss = expected, attackEvents,
                pass = hpLoss == expected && attackEvents == 1 });
            File.WriteAllText(file, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
