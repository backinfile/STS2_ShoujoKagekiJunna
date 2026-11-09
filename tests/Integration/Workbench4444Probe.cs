using System.Text.Json;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public static partial class Workbench3650Probe
{
    public static async Task Run4444(Player player)
    {
        string directory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/workbench4444"));
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        var selector = new Selector();
        using var choices = CardSelectCmd.UseSelector(selector);
        double speed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 20;
        async Task Check(string name, Func<Task> test)
        {
            try { await Reset(player); await test(); results.Add(new { name, pass = true }); }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            await Check("107 models and upgrades; 20 knowledge groups", async () =>
            {
                Expect(CardCatalog.Revision == 4444 && CardCatalog.Cards.Count == 107 && CardCatalog.Groups.Count == 20, "snapshot");
                foreach (var definition in CardCatalog.Cards)
                {
                    var card = await Card(player, definition.Id, pile: PileType.Discard);
                    Expect(card.Title == definition.Name, definition.Id + " title");
                    card.UpgradeInternal(); card.FinalizeUpgradeInternal();
                    Expect(card.EnergyCost.CostsX || CardActions.Cost(card) == definition.UpgradedCost, definition.Id + " cost");
                    foreach (var number in definition.Values.Where(n => card.DynamicVars.ContainsKey(n.Name)))
                        Expect(card.DynamicVars[number.Name].BaseValue == number.Upgraded, definition.Id + " value " + number.Name);
                }
            });
            foreach (bool up in new[] { false, true })
            {
                await Check("stardust damage and two child arrows " + up, async () =>
                {
                    var enemy = player.Creature.CombatState!.Enemies.First();
                    var source = await Card(player, "U_CARD_005", up); decimal hp = enemy.CurrentHp;
                    await Play(source); await Finish(source);
                    Expect(hp - enemy.CurrentHp == (up ? 12 : 8), "damage");
                    var children = ArrowCombat.Deployed.ToArray();
                    Expect(children.Length == 2 && children.All(a => a.Card != source && a.Remaining == 3 && !a.Card.IsUpgraded && a.Target == enemy), "two base child arrows");
                });
                await Check("star cluster independent copies " + up, async () =>
                {
                    var source = await Card(player, "JA38"); DamageModifiers.Add(source, 7); await Play(source);
                    var original = ArrowCombat.Find(source)!;
                    original.GetType().GetProperty("PlayCount")!.SetValue(original, 2);
                    await Play(await Card(player, "JA176", up));
                    Expect(ArrowCombat.Deployed.Count == 2, "snapshot copied once");
                    var copy = ArrowCombat.Deployed.Single(a => a.Card != source);
                    Expect(copy.Remaining == original.Remaining && copy.Target == original.Target && copy.PlayCount == 2, "wait, target and replay");
                    Expect(DamageModifiers.CaptureDamageModifierSnapshot(copy.Card).DamageBonus == 7, "damage bonus");
                    decimal hp = copy.Target!.CurrentHp; await Finish(copy.Card);
                    Expect(copy.Target.CurrentHp < hp && ArrowCombat.Find(source) != null, "independent resolution");
                });
                await Check("encouragement acceleration " + up, async () =>
                {
                    var source = await Card(player, "JB03"); await Play(source);
                    var encouragement = await Card(player, "JA177", up); await Play(encouragement); await Finish(encouragement);
                    Expect(ArrowCombat.Find(source)?.Remaining == (up ? 1 : 2), "acceleration");
                });
                await Check("end-turn acceleration " + up, async () =>
                {
                    var source = await Card(player, "JB03"); await Play(source); await Play(await Card(player, "JA35", up));
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA35");
                    await power.AfterPlayerTurnStart(Context, player); Expect(ArrowCombat.Find(source)!.Remaining == 3, "no start trigger");
                    await power.BeforeSideTurnEnd(Context, CombatSide.Player, [player.Creature]);
                    Expect(ArrowCombat.Find(source)?.Remaining == (up ? 1 : 2), "end trigger");
                });
                await Check("knowledge strength once per turn " + up, async () =>
                {
                    await Play(await Card(player, "JK260", up)); await Play(await Card(player, "JH27")); await Play(await Card(player, "JH27"));
                    Expect(player.Creature.Powers.OfType<StrengthPower>().Single().Amount == (up ? 3 : 2), "strength");
                });
                await Check("linked knowledge cards and all-enemy debuffs " + up, async () =>
                {
                    await Play(await Card(player, "JK254", up)); var linked = await Card(player, "JK272B");
                    await Play(await Card(player, "JK272", up));
                    Expect(linked.Pile?.Type == PileType.Exhaust, "actual linked card exhausted");
                    Expect(player.Creature.CombatState!.Enemies.All(e => e.Powers.OfType<WeakPower>().Any() && e.Powers.OfType<VulnerablePower>().Any()), "all enemies debuffed");
                });
                await Check("single-target arrows hit all enemies " + up, async () =>
                {
                    var enemies = player.Creature.CombatState!.Enemies.ToArray(); Expect(enemies.Length >= 2, "multi-enemy fixture");
                    await Play(await Card(player, "JA205", up)); var arrow = await Card(player, "JB03"); await Play(arrow);
                    var hp = enemies.Select(e => e.CurrentHp).ToArray(); await Finish(arrow);
                    Expect(enemies.Select((e, i) => hp[i] - e.CurrentHp).All(d => d == 25), "25 damage each");
                });
                await Check("updated triple-hit damage " + up, async () =>
                {
                    var enemy = player.Creature.CombatState!.Enemies.First(); decimal hp = enemy.CurrentHp;
                    var volley = await Card(player, "JA72", up); await Play(volley); await Finish(volley);
                    Expect(hp - enemy.CurrentHp == 24, "8 times 3");
                    hp = enemy.CurrentHp; var knowledge = await Card(player, "JK131B", up); await Play(knowledge); await Finish(knowledge);
                    Expect(hp - enemy.CurrentHp == (up ? 63 : 48), "knowledge triple hit");
                });
                foreach (bool self in new[] { false, true })
                    await Check("closer step choice " + up + "/" + self, async () =>
                    {
                        selector.SelfTarget = self;
                        var enemyArrow = await Card(player, "JB03"); await Play(enemyArrow);
                        var ownArrow = await Card(player, "JA80"); await Play(ownArrow);
                        int ownWait = ArrowCombat.Find(ownArrow)!.Remaining;
                        await Play(await Card(player, "JA119", up));
                        Expect(ArrowCombat.Find(enemyArrow)?.Remaining == (self ? 3 : 2), "enemy group");
                        Expect(ArrowCombat.Find(ownArrow)?.Remaining == ownWait - (self ? 1 : 0), "self group");
                    });
            }
        }
        finally { Godot.Engine.TimeScale = speed; File.WriteAllText(Path.Combine(directory, "completed.txt"), "completed"); }
    }
    public static async Task Prepare4444(Player player)
    {
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card);
        await Reset(player); Godot.Engine.TimeScale = 1;
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 300); await CreatureCmd.SetCurrentHp(enemy, 300); }
        using var single = KnowledgeCombat.SingleCard();
        foreach (string id in new[] { "JA205", "U_CARD_005", "JA176", "JA177", "JA119" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Hand, player);
        player.PlayerCombatState!.Energy = 20;
    }
}
