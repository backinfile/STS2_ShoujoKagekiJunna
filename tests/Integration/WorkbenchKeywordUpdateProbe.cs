using System.Text.Json;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public static partial class Workbench3650Probe
{
    public static async Task RunKeywordUpdate(Player player)
    {
        Directory.CreateDirectory(FormalDirectory);
        var results = new List<object>();
        var selector = new Selector();
        using var choices = CardSelectCmd.UseSelector(selector);
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 2;
        async Task Case(string name, Func<Task> test)
        {
            try
            {
                selector.Preferred = null;
                await Reset(player);
                await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
                await test();
                results.Add(new { name, pass = true });
            }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
            File.WriteAllText(Path.Combine(FormalDirectory, "keyword-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            foreach (bool upgraded in new[] { false, true })
                await Case("Repeated practice upgrade keywords and real extra hit " + upgraded, async () =>
                {
                    var source = await Card(player, "JG03", upgraded);
                    var target = await Card(player, "JB01");
                    selector.Preferred = target;
                    Expect(source.Keywords.Contains(CardKeyword.Exhaust) == !upgraded && !source.Keywords.Contains(CardKeyword.Retain), "Current native keywords");
                    await Play(source);
                    Expect(source.Pile?.Type == (upgraded ? PileType.Discard : PileType.Exhaust), "Current source destination");
                    Expect(DamageModifiers.CaptureDamageModifierSnapshot(target).ExtraHits == 1, "One actual extra hit");
                    var enemy = player.Creature.CombatState!.Enemies.First(); int hp = enemy.CurrentHp;
                    await Play(target);
                    Expect(enemy.CurrentHp == hp - 12, "Strike resolves two six-damage hits");
                });
            foreach (bool upgraded in new[] { false, true })
                await Case("Counterattack damage before material arrows " + upgraded, async () =>
                {
                    var source = await Card(player, "JA88", upgraded);
                    CardModel? answer = upgraded ? null : await Card(player, "JA108B");
                    CardModel? block = upgraded ? null : await Card(player, "JB02");
                    int energy = player.PlayerCombatState!.Energy;
                    var enemy = player.Creature.CombatState!.Enemies.First(); int hp = enemy.CurrentHp;
                    Expect(source.Type == CardType.Attack && source.TargetType == TargetType.AnyEnemy && !source.Keywords.Contains(CardKeyword.Exhaust), "Attack target and keyword");
                    await Play(source);
                    Expect(enemy.CurrentHp == hp - (upgraded ? 20 : 15) && player.Creature.Block == 0, "Immediate attack replaces old block");
                    Expect(source.Pile?.Type == PileType.Discard && player.PlayerCombatState.Energy == energy - 2, "Only source cost and normal discard");
                    Expect(ArrowCombat.Deployed.Count == (upgraded ? 0 : 2), "Any material count includes zero");
                    if (answer != null && block != null)
                    {
                        Expect(ArrowCombat.Find(answer)?.Remaining == 5 && ArrowCombat.Find(block)?.Remaining == 5, "Actual chosen cards use five turns");
                        Expect(answer.EnergyCost.GetWithModifiers(CostModifiers.All) == 3 && block.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Material printed costs preserved");
                        await Finish(block); Expect(player.Creature.Block == 5, "Original block effect occurs on completion");
                        int before = enemy.CurrentHp; await Finish(answer);
                        Expect(enemy.CurrentHp == before - 50 && answer.Pile?.Type == PileType.Discard, "Original group damage occurs on completion");
                    }
                });
            foreach (bool upgraded in new[] { false, true })
                await Case("Restored string volley completes arrows and discards " + upgraded, async () =>
                {
                    Expect(CardCatalog.Cards.Count == 98 && CardCatalog.Get("JA71").Pool, "Restored formal reward model");
                    var answer = await Card(player, "JA108B"); await Play(answer);
                    var energyArrow = await Card(player, "JA80"); await Play(energyArrow);
                    var source = await Card(player, "JA71", upgraded);
                    var enemies = player.Creature.CombatState!.Enemies.ToArray(); var hp = enemies.Select(e => e.CurrentHp).ToArray();
                    int energy = player.PlayerCombatState!.Energy;
                    Expect(!source.Keywords.Contains(CardKeyword.Exhaust), "Both versions no longer exhaust");
                    await Play(source);
                    Expect(source.Pile?.Type == PileType.Discard && player.Creature.Block == (upgraded ? 15 : 10), "Updated block and source destination");
                    Expect(ArrowCombat.Deployed.Count == 0 && answer.Pile?.Type == PileType.Discard && energyArrow.Pile?.Type == PileType.Discard, "All existing deployments finish once");
                    Expect(player.PlayerCombatState.Energy == energy && enemies.Select((e, i) => e.CurrentHp == hp[i] - 50).All(x => x), "Energy and area payloads both resolve");
                });
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            File.WriteAllText(Path.Combine(FormalDirectory, "keyword-completed.txt"), "completed");
        }
    }

    public static async Task PrepareKeywords(Player player)
    {
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card);
        await Reset(player);
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        Godot.Engine.TimeScale = 1;
        await CreatureCmd.SetCurrentHp(player.Creature, player.Creature.MaxHp);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 300); await CreatureCmd.SetCurrentHp(enemy, 300); }
        player.PlayerCombatState!.Energy = 5;
        using var single = KnowledgeCombat.SingleCard();
        foreach (var (id, upgraded) in new[] { ("JG03", false), ("JG03", true), ("JA88", true), ("JA71", true), ("JA108B", false), ("JB02", false) })
        {
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }
    }
}
