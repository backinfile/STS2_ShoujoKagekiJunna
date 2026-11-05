using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class StarterRelicProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_relics";
    public override string Args => "check";
    public override string Description => "Starter gem energy, retention and upgrade checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Starter relic checks started");

    private static async Task Check(Player player)
    {
        const string report = "D:/Github/STS2_ShoujoKagekiJunna/artifacts/starter-relics/results.json";
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var combat = player.PlayerCombatState!;
        int originalEnergy = combat.Energy, originalTurn = combat.TurnNumber;
        var turnField = AccessTools.Field(typeof(PlayerCombatState), "<TurnNumber>k__BackingField");
        void CheckValue(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        void Turn(int turn) => turnField.SetValue(combat, turn);
        async Task ClearReserve()
        {
            if (player.Creature.GetPower<EnergyReserve>() is { } reserve) await PowerCmd.Remove(reserve);
        }
        async Task Refresh()
        {
            combat.ResetEnergy();
            if (player.Creature.GetPower<EnergyReserve>() is { } reserve) await reserve.AfterEnergyReset(player);
        }
        try
        {
            CheckValue("new run starter", ModelDb.Character<Junna>().StartingRelics.Single().Id, ModelDb.Relic<JunnaGreenGem>().Id);
            CheckValue("natural first-turn energy", combat.Energy, combat.MaxEnergy + 1);
            CheckValue("natural first-turn reserve", player.Creature.GetPower<EnergyReserve>()?.Amount ?? 0, 1);
            CheckValue("relic pool energy icon", ResourceLoader.Exists(ModelDb.RelicPool<JunnaRelicPool>().TextEnergyIconPath), true);
            foreach (var (canonical, amount) in new (RelicModel, int)[] { (ModelDb.Relic<JunnaGreenGem>(), 1), (ModelDb.Relic<JunnaBurningGem>(), 3) })
            {
                var relic = (JunnaGem)canonical.ToMutable();
                relic.Owner = player;
                string label = relic.Id.Entry;
                string description = relic.DynamicDescription.GetFormattedText();
                CheckValue(label + " description", !description.Contains("ERROR") && !description.Contains("{"), true);
                CheckValue(label + " icon", ResourceLoader.Exists(relic.PackedIconPath), true);
                CheckValue(label + " icon outline", relic.IconOutline.GetSize(), new Vector2(128, 128));
                CheckValue(label + " pool", relic.Pool.Id, ModelDb.RelicPool<JunnaRelicPool>().Id);
                foreach (int remaining in new[] { 0, 1, 9 }.Distinct())
                {
                    await ClearReserve(); Turn(1); combat.Energy = combat.MaxEnergy;
                    await relic.BeforeHandDraw(player, context, state);
                    CheckValue(label + $" gain before remainder {remaining}", combat.Energy, combat.MaxEnergy + amount);
                    CheckValue(label + $" capacity before remainder {remaining}", player.Creature.GetPower<EnergyReserve>()!.Amount, amount);
                    combat.Energy = remaining; Turn(2); await Refresh();
                    CheckValue(label + $" carries remainder {remaining}", combat.Energy, combat.MaxEnergy + Math.Min(remaining, amount));
                    CheckValue(label + " reserve removed", player.Creature.GetPower<EnergyReserve>() == null, true);
                    int before = combat.Energy;
                    await relic.BeforeHandDraw(player, context, state);
                    CheckValue(label + " no second-turn grant", combat.Energy, before);
                    Turn(3); await Refresh();
                    CheckValue(label + " no third-turn carry", combat.Energy, combat.MaxEnergy);
                }
                await ClearReserve(); Turn(1); combat.Energy = combat.MaxEnergy;
                await relic.BeforeHandDraw(player, context, state);
                await EnergyReserve.Add(context, player, 2);
                CheckValue(label + " stacks card capacity", player.Creature.GetPower<EnergyReserve>()!.Amount, amount + 2);
                combat.Energy = 9; Turn(2); await Refresh();
                CheckValue(label + " stacked carry", combat.Energy, combat.MaxEnergy + amount + 2);
                await ClearReserve(); Turn(1); combat.Energy = combat.MaxEnergy;
                await relic.BeforeHandDraw(player, context, state);
                combat.Energy = amount; combat.AddMaxEnergyToCurrent();
                if (player.Creature.GetPower<EnergyReserve>() is { } retained) await retained.AfterEnergyReset(player);
                CheckValue(label + " full retention counted once", combat.Energy, combat.MaxEnergy + amount);
                CheckValue(label + " full retention clears reserve", player.Creature.GetPower<EnergyReserve>() == null, true);
            }
            var touch = (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>().ToMutable();
            CheckValue("Orobas replacement", touch.GetUpgradedStarterRelic(ModelDb.Relic<JunnaGreenGem>()).Id, ModelDb.Relic<JunnaBurningGem>().Id);
            CheckValue("Orobas setup", touch.SetupForPlayer(player), true);
            await RelicCmd.Obtain(touch, player);
            CheckValue("old starter replaced", player.Relics.OfType<JunnaGreenGem>().Count(), 0);
            CheckValue("one upgraded starter", player.Relics.OfType<JunnaBurningGem>().Count(), 1);
        }
        catch (Exception ex) { rows.Add(new { name = "exception", pass = false, error = ex.ToString() }); }
        finally
        {
            await ClearReserve(); Turn(originalTurn); combat.Energy = originalEnergy;
            File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
