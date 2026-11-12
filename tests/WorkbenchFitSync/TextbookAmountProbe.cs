using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using ShoujoKageki.Core.V1;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed partial class Workbench5000Command
{
    private static readonly List<int> TextbookHits = [];
    private static void CaptureTextbookDamage(CatalogPower __instance, DamageResult result, CardModel? cardSource)
    {
        if (__instance.DesignId == "JK235" && cardSource is CatalogCard { DesignId: "JK235" })
            TextbookHits.Add(result.TotalDamage);
    }
    private static async Task TextbookAmount(Player player)
    {
        var rows = new List<object>();
        string path = Path.GetFullPath("../../../out/tmp/textbook-amount/results.json", Environment.CurrentDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var patch = new Harmony("JunnaWorkbenchFitSync.TextbookAmount");
        patch.Patch(AccessTools.Method(typeof(CatalogPower), nameof(CatalogPower.AfterDamageGiven)),
            postfix: new HarmonyMethod(typeof(Workbench5000Command), nameof(CaptureTextbookDamage)));
        async Task Case(string name, Func<Task<object>> run)
        {
            try { await Reset(player); rows.Add(new { name, pass = true, actual = await run() }); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        double previousSpeed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 5;
        try
        {
            foreach (bool upgraded in new[] { false, true })
                foreach (var modifier in new[] { (Bonus: 0, Hits: 0), (Bonus: 2, Hits: 0), (Bonus: 0, Hits: 2), (Bonus: 2, Hits: 2) })
                    await Case($"textbook upgraded={upgraded} bonus={modifier.Bonus} extraHits={modifier.Hits}", async () =>
                    {
                        var source = await Card(player, "JK235", upgraded);
                        DamageModifiers.Add(source, modifier.Bonus, modifier.Hits);
                        await Play(source);
                        var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK235");
                        TextbookHits.Clear();
                        await Play(await Card(player, "JS35"));
                        Expect(TextbookHits.Count == 0, "Non-knowledge does not trigger textbook");
                        await Play(await Card(player, "JK49B"));
                        int amount = (upgraded ? 4 : 3) + modifier.Bonus;
                        int count = 1 + modifier.Hits;
                        var actual = new { power.Amount, expectedAmount = amount * count, damageEvents = TextbookHits.ToArray() };
                        Expect(TextbookHits.SequenceEqual(Enumerable.Repeat(amount, count)), JsonSerializer.Serialize(actual));
                        Expect(power.Amount == amount * count, JsonSerializer.Serialize(actual));
                        var tooltip = power.SmartDescription; power.DynamicVars.AddTo(tooltip);
                        string text = Regex.Replace(tooltip.GetFormattedText(), @"\[[^\]]+\]", "");
                        Expect(text.Contains(amount + "点伤害" + (count == 1 ? "。" : count + "次")), text);
                        return actual;
                    });
            await Case("textbook mixed stacked damage and hits", async () =>
            {
                var first = await Card(player, "JK235"); DamageModifiers.Add(first, 2, 2); await Play(first);
                await Play(await Card(player, "JK235", true));
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK235");
                TextbookHits.Clear(); await Play(await Card(player, "JK49B"));
                var actual = new { power.Amount, damageEvents = TextbookHits.ToArray() };
                Expect(TextbookHits.SequenceEqual(new[] { 5, 5, 5, 4 }), JsonSerializer.Serialize(actual));
                Expect(power.Amount == 19, JsonSerializer.Serialize(actual));
                return actual;
            });
            foreach (bool upgraded in new[] { false, true })
            {
                await Case("textbook endurance adds hit upgraded=" + upgraded, async () =>
                {
                    var source = await Card(player, "JK235", upgraded); DamageModifiers.Add(source, 6, 1); await Play(source);
                    await PowerCmd.Apply<JunnaEndurancePower>(Context, player.Creature, 1, player.Creature, null);
                    await Play(await Card(player, "JS35"));
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK235");
                    TextbookHits.Clear(); await Play(await Card(player, "JK49B"));
                    int damage = upgraded ? 10 : 9;
                    var actual = new { power.Amount, damageEvents = TextbookHits.ToArray() };
                    Expect(TextbookHits.SequenceEqual(Enumerable.Repeat(damage, 3)), JsonSerializer.Serialize(actual));
                    Expect(power.Amount == damage * 3, JsonSerializer.Serialize(actual));
                    return actual;
                });
                await Case("depletion hit preview excludes external hits upgraded=" + upgraded, async () =>
                {
                    var source = (CatalogCard)await Card(player, "JK314", upgraded);
                    int before = KnowledgeProgress.DepletedPartners(source);
                    CardModel permanent;
                    using (KnowledgeCombat.SingleCard())
                    {
                        permanent = player.RunState.CreateCard(CardCatalog.Model("JK122B"), player);
                        await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
                    }
                    Shine.SetState(permanent, 1, 3);
                    var desire = await Card(player, "JK122B"); desire.DeckVersion = permanent;
                    Shine.SetState(desire, 1, 3); await Play(desire);
                    int depleted = KnowledgeProgress.DepletedPartners(source);
                    Expect(depleted == before + 1, "Actual depletion is archived");
                    DamageModifiers.Add(source, extraHits: 2);
                    player.PlayerCombatState!.RecalculateCardValues();
                    string description = Regex.Replace(source.GetDescriptionForPile(PileType.Draw), @"\[[^\]]+\]", "");
                    int hp = player.Creature.CombatState!.Enemies.Sum(e => e.CurrentHp);
                    await Play(source);
                    int actualDamage = hp - player.Creature.CombatState.Enemies.Sum(e => e.CurrentHp);
                    int printedHits = 2 + depleted;
                    var actual = new { depleted, printedHits, description, actualDamage };
                    Expect(actualDamage == (upgraded ? 12 : 8) * (printedHits + 2), JsonSerializer.Serialize(actual));
                    Expect(description.Contains("点伤害" + printedHits + "次"), JsonSerializer.Serialize(actual));
                    return actual;
                });
            }
        }
        finally
        {
            Godot.Engine.TimeScale = previousSpeed;
            patch.UnpatchAll("JunnaWorkbenchFitSync.TextbookAmount");
        }
        File.WriteAllText(Path.ChangeExtension(path, ".completed"), "completed");
    }
    private static async Task PrepareTextbook(Player player)
    {
        await Reset(player, visualize: true);
        await CreatureCmd.SetMaxHp(player.Creature, 70); await CreatureCmd.SetCurrentHp(player.Creature, 70);
        foreach (var foe in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(foe, 1000); await CreatureCmd.SetCurrentHp(foe, 1000); }
        player.PlayerCombatState!.Energy = 8;
        await Card(player, "JK235", visualize: true);
        var stronger = await Card(player, "JK235", true, visualize: true); DamageModifiers.Add(stronger, 2, 2);
        await Card(player, "JS35", visualize: true); await Card(player, "JK49B", visualize: true);
        CardModel permanent;
        using (KnowledgeCombat.SingleCard())
        {
            permanent = player.RunState.CreateCard(CardCatalog.Model("JK122B"), player);
            await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
        }
        Shine.SetState(permanent, 1, 3);
        var desire = await Card(player, "JK122B", visualize: true); desire.DeckVersion = permanent;
        Shine.SetState(desire, 1, 3);
        var star = await Card(player, "JK314", true, visualize: true); DamageModifiers.Add(star, extraHits: 2);
    }
}
