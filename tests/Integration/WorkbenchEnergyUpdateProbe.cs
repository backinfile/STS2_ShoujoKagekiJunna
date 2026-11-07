using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public static partial class Workbench3650Probe
{
    public static async Task RunEnergyUpdate(Player player)
    {
        Directory.CreateDirectory(DirectoryPath);
        var results = new List<object>();
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 2;
        async Task Case(string name, Func<Task> test)
        {
            try
            {
                await Reset(player);
                await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
                await test();
                results.Add(new { name, pass = true });
            }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
            File.WriteAllText(Path.Combine(DirectoryPath, "energy-update-results.json"),
                JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            foreach (bool upgraded in new[] { false, true })
                await Case("启明星 waits two turns and grants current-turn energy " + upgraded, async () =>
                {
                    var definition = CardCatalog.Cards.Single(c => c.Id == "JA80");
                    Expect(definition.Pool && definition.Cost == 0 && definition.UpgradedCost == 0, "Active zero-cost reward model");
                    var source = (CatalogCard)await Card(player, "JA80", upgraded);
                    var state = player.PlayerCombatState!;
                    state.ResetEnergy();
                    int normalEnergy = state.Energy;
                    state.Energy = 10;
                    await Play(source);
                    Expect(state.Energy == 10 && ArrowCombat.Find(source)?.Remaining == 2, "Deployment grants no immediate energy");
                    await Finish(source);
                    Expect(state.Energy == 10 + (upgraded ? 4 : 3), "Correct actual energy gain");
                    Expect(source.Pile?.Type == PileType.Discard && ArrowCombat.Find(source) == null, "Normal completion discards");
                    Expect(player.Creature.GetPower<EnergyReserve>() == null, "No retention granted");
                    state.ResetEnergy();
                    Expect(state.Energy == normalEnergy, "Unused energy does not carry over");
                });
            await Case("启明星 preserves retention granted by another source", async () =>
            {
                var state = player.PlayerCombatState!;
                state.ResetEnergy(); int normalEnergy = state.Energy;
                await EnergyReserve.Add(Context, player, 2);
                state.Energy = 10;
                var source = await Card(player, "JA80");
                await Play(source); await Finish(source);
                Expect(player.Creature.GetPower<EnergyReserve>()?.Amount == 2, "Existing retention amount is unchanged");
                state.ResetEnergy();
                Expect(state.Energy == normalEnergy + 2, "Only the other source's retention applies");
            });
            await Case("速射 completes 启明星 immediately at zero and consumes it", async () =>
            {
                await Play(await Card(player, "JA105"));
                await Play(await Card(player, "JA105"));
                var source = await Card(player, "JA80");
                player.PlayerCombatState!.Energy = 10;
                await Play(source);
                Expect(ArrowCombat.Find(source) == null && source.Pile?.Type == PileType.Exhaust, "Zero-wait completion and consumption");
                Expect(player.PlayerCombatState.Energy == 13, "Single base energy grant");
                Expect(player.Creature.GetPower<EnergyReserve>() == null, "Haste does not add retention");
            });
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            File.WriteAllText(Path.Combine(DirectoryPath, "energy-update-completed.txt"), "completed");
        }
    }

    public static async Task PrepareEnergy(Player player)
    {
        foreach (var current in player.PlayerCombatState!.AllCards.ToArray())
            if (current.Pile != null) await CardPileCmd.RemoveFromCombat(current);
        await Reset(player);
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        Godot.Engine.TimeScale = 1;
        await CreatureCmd.SetCurrentHp(player.Creature, player.Creature.MaxHp);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 300); await CreatureCmd.SetCurrentHp(enemy, 300); }
        player.PlayerCombatState!.Energy = 0;
        foreach (bool upgraded in new[] { false, true })
        {
            var card = CardCatalog.Create("JA80", player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }
        for (int i = 0; i < 15; i++)
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JB02", player), PileType.Draw, player);
    }
}
