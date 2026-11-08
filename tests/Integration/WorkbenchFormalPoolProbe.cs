using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public static partial class Workbench3650Probe
{
    private static readonly string FormalDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/formal-sync"));

    public static async Task RunFormalUpdate(Player player)
    {
        Directory.CreateDirectory(FormalDirectory);
        var results = new List<object>();
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 2;
        async Task Case(string name, Func<Task> test)
        {
            try
            {
                await Reset(player);
                await MegaCrit.Sts2.Core.Models.ModelDb.GetById<JunnaCombatHooks>(MegaCrit.Sts2.Core.Models.ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
                await test();
                results.Add(new { name, pass = true });
            }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
            File.WriteAllText(Path.Combine(FormalDirectory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            foreach (bool upgraded in new[] { false, true })
                await Case("Ordered physical arrow deployment and source payment " + upgraded, async () =>
                {
                    Expect(CardCatalog.Cards.Count == 98 && CardCatalog.Groups.Count == 18, "Formal pool snapshot");
                    var paper = (CatalogCard)CardCatalog.Create("JK148", player);
                    Expect(paper.Value("Damage") == 6, "Updated base paper damage");
                    paper.UpgradeInternal(); paper.FinalizeUpgradeInternal();
                    Expect(paper.Value("Damage") == 8 && CardCatalog.Get("JK148F").Values.Single(v => v.Name == "Turns").Base == 1, "Updated upgraded damage and verification wait");
                    Expect(CardCatalog.Get("JA108").Name == "论证" && CardCatalog.Groups.Single(g => g.MainCardId == "JA108").GrantedCardIds.SequenceEqual(new[] { "JA108B" }), "Current named two-card knowledge group");
                    Expect(!CardCatalog.Cards.Any(c => c.Id == "JG38") && CardCatalog.Get("JG38").Name == "肌肉记忆", "Retired model compatibility");
                    var source = await Card(player, "JA108", upgraded);
                    var answer = await Card(player, "JA108B", true, PileType.Draw);
                    var ordinary = await Card(player, "JB02", pile: PileType.Draw);
                    var aimed = await Card(player, "JA38", pile: PileType.Draw);
                    var energy = await Card(player, "JA80", pile: PileType.Draw);
                    DamageModifiers.Add(answer, 7);
                    var expected = player.PlayerCombatState!.DrawPile.Cards.Where(CardCatalog.IsArrow).ToArray();
                    int before = player.PlayerCombatState.Energy, count = player.PlayerCombatState.AllCards.Count();
                    await Play(source);
                    Expect(player.PlayerCombatState.Energy == before - (upgraded ? 3 : 4), "Only source energy is paid");
                    Expect(source.Pile?.Type == PileType.Discard, "Source discards after the batch");
                    Expect(ArrowCombat.Deployed.Select(a => a.Card).SequenceEqual(expected), "Actual cards deploy once in original draw-pile order");
                    Expect(ordinary.Pile?.Type == PileType.Draw && player.PlayerCombatState.AllCards.Count() == count, "Non-arrow and knowledge count stay intact");
                    Expect(ArrowCombat.Find(answer)?.Remaining == 3 && answer.IsUpgraded && DamageModifiers.CaptureDamageModifierSnapshot(answer).DamageBonus == 7, "Upgrade, bonus and arrow wait retained");
                    Expect(ArrowCombat.Find(aimed)?.Target == player.Creature.CombatState!.HittableEnemies.First(), "First legal enemy is chosen");
                    Expect(ArrowCombat.Find(energy)?.Remaining == 2, "Self-target arrow uses its own wait");
                });
            foreach (bool upgraded in new[] { false, true })
                await Case("Delayed area damage " + upgraded, async () =>
                {
                    var source = await Card(player, "JA108B", upgraded);
                    var enemies = player.Creature.CombatState!.Enemies.ToArray();
                    var before = enemies.Select(e => e.CurrentHp).ToArray();
                    await Play(source);
                    Expect(ArrowCombat.Find(source)?.Remaining == 3 && enemies.Select(e => e.CurrentHp).SequenceEqual(before), "No damage before completion");
                    await Finish(source);
                    Expect(enemies.Select((e, i) => e.CurrentHp == before[i] - (upgraded ? 65 : 50)).All(x => x), "Every enemy receives one damage payload");
                    Expect(source.Pile?.Type == PileType.Discard && ArrowCombat.Find(source) == null, "Normal destination once");
                });
            await Case("Zero-wait draw removes later snapshot members", async () =>
            {
                var source = await Card(player, "JA108");
                var draw = (CatalogCard)await Card(player, "JA15", pile: PileType.Draw);
                draw.DynamicVars["Turns"].BaseValue = 0;
                var first = await Card(player, "JA38", pile: PileType.Draw);
                var second = await Card(player, "JA80", pile: PileType.Draw);
                await CardPileCmd.Add(draw, PileType.Draw, CardPilePosition.Top, skipVisuals: true);
                await Play(source);
                Expect(draw.Pile?.Type == PileType.Discard && ArrowCombat.Find(draw) == null, "Zero-wait arrow finishes immediately");
                Expect(first.Pile?.Type == PileType.Hand && second.Pile?.Type == PileType.Hand && ArrowCombat.Deployed.Count == 0, "Arrows drawn out of the snapshot are skipped");
            });
            await Case("Blocked arrows stay put and X input is zero", async () =>
            {
                var source = await Card(player, "JA108");
                var blocked = await Card(player, "JA38", pile: PileType.Draw);
                blocked.AddKeyword(CardKeyword.Unplayable);
                var x = (CatalogCard)await Card(player, "JA38", pile: PileType.Draw);
                AccessTools.Field(typeof(CardModel), "_energyCost").SetValue(x, new CardEnergyCost(x, x.EnergyCost.Canonical, true));
                Expect(x.EnergyCost.CostsX, "X-cost compatibility fixture");
                x.EnergyCost.CapturedXValue = 7;
                int before = player.PlayerCombatState!.Energy;
                await Play(source);
                Expect(blocked.Pile?.Type == PileType.Draw && ArrowCombat.Find(blocked) == null, "Blocked arrow remains in its original pile");
                Expect(player.PlayerCombatState.Energy == before - 4 && x.EnergyCost.CapturedXValue == 0, "X uses no remaining energy");
                Expect(ArrowCombat.Find(x)?.Remaining == x.Value("Turns") && ArrowCombat.Find(x)?.Play.Resources.EnergySpent == 0, "No X-based wait reduction or extra payment");
            });
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            File.WriteAllText(Path.Combine(FormalDirectory, "completed.txt"), "completed");
        }
    }

    public static async Task PrepareFormal(Player player)
    {
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card);
        await Reset(player);
        await MegaCrit.Sts2.Core.Models.ModelDb.GetById<JunnaCombatHooks>(MegaCrit.Sts2.Core.Models.ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        Godot.Engine.TimeScale = 1;
        await CreatureCmd.SetCurrentHp(player.Creature, player.Creature.MaxHp);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 300); await CreatureCmd.SetCurrentHp(enemy, 300); }
        player.PlayerCombatState!.Energy = 4;
        using var single = KnowledgeCombat.SingleCard();
        foreach (bool upgraded in new[] { false, true })
        {
            var card = CardCatalog.Create("JA108", player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }
        foreach (string id in new[] { "JA108B", "JA38", "JA80" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Draw, player);
        for (int i = 0; i < 15; i++)
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JB02", player), PileType.Draw, player);
    }
}
