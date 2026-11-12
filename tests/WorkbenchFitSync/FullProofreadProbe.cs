using System.Text.Json;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed partial class Workbench5000Command
{
    private sealed class ProofreadSelector(int requested) : ICardSelector
    {
        public int Minimum = -1, Maximum = -1;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> cards, int minSelect, int maxSelect)
        {
            Minimum = minSelect; Maximum = maxSelect;
            return Task.FromResult(cards.Take(Math.Max(requested, minSelect)));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> cards,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new NotSupportedException();
    }
    private static readonly string ProofreadResult = Path.GetFullPath("../../../out/tmp/full-card-proofread/results.json", Environment.CurrentDirectory);
    private static async Task Proofread(Player player)
    {
        var rows = new List<object>();
        async Task Case(string name, Func<Task> test)
        {
            try { await Reset(player); await test(); rows.Add(new { name, pass = true }); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            Directory.CreateDirectory(Path.GetDirectoryName(ProofreadResult)!);
            File.WriteAllText(ProofreadResult, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        double speed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 5;
        try
        {
            foreach (bool upgraded in new[] { false, true })
            {
                foreach (int count in new[] { 0, 1, 2 })
                    await Case("up to two materials " + count + " upgraded=" + upgraded, async () =>
                    {
                        var selector = new ProofreadSelector(count);
                        using var selection = CardSelectCmd.UseSelector(selector);
                        var first = await Card(player, "JB01");
                        var second = await Card(player, "JB02");
                        var source = await Card(player, "JK84", upgraded);
                        await Play(source);
                        Expect(selector.Minimum == 0 && selector.Maximum == 2, "Selection must allow zero through two cards");
                        Expect(CardChoices.Prompt(source, 2).GetFormattedText().Contains("至多2"), "Prompt must say up to two");
                        Expect(ArrowCombat.Deployed.Count == count, "Only selected actual cards are deployed");
                        Expect(ArrowCombat.Deployed.All(a => a.Remaining == 5), "Selected cards wait five turns");
                        Expect(new[] { first, second }.Count(c => c.Pile?.Type == PileType.Hand) == 2 - count, "Unselected actual cards stay in hand");
                        Expect(source.Pile?.Type == (upgraded ? PileType.Discard : PileType.Exhaust), "Version-specific source destination");
                    });
                await Case("enemy arrow haste HUD upgraded=" + upgraded, async () =>
                {
                    await Play(await Card(player, "JA96", upgraded));
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA96");
                    var description = power.SmartDescription; power.DynamicVars.AddTo(description);
                    Expect(description.GetFormattedText().Contains("目标为敌人"), "Power HUD must restrict the target scope");
                });
                await Case("enemy arrow haste controls upgraded=" + upgraded, async () =>
                {
                    var enemyArrow = await Card(player, "JA38"); await Play(enemyArrow);
                    var ownArrow = await Card(player, "JA15"); await Play(ownArrow);
                    await Play(await Card(player, "JA96", upgraded));
                    var foe = player.Creature.CombatState!.Enemies.First();
                    await CreatureCmd.Damage(Context, [player.Creature], 2, ValueProp.Unpowered, foe, null, null);
                    Expect(ArrowCombat.Find(enemyArrow)?.Remaining == 2 && ArrowCombat.Find(ownArrow)?.Remaining == 1, "Non-attack damage does not accelerate");
                    await CreatureCmd.GainBlock(player.Creature, 2, ValueProp.Unpowered, null);
                    await CreatureCmd.Damage(Context, [player.Creature], 2, ValueProp.Move, foe, null, null);
                    Expect(ArrowCombat.Find(enemyArrow)?.Remaining == 2 && ArrowCombat.Find(ownArrow)?.Remaining == 1, "Fully blocked attack does not accelerate");
                    await CreatureCmd.LoseBlock(Context, player.Creature, player.Creature.Block, null);
                    await CreatureCmd.Damage(Context, [player.Creature], 2, ValueProp.Move, foe, null, null);
                    Expect(ArrowCombat.Find(enemyArrow)?.Remaining == 1 && ArrowCombat.Find(ownArrow)?.Remaining == 1, "Unblocked attack accelerates enemy target only");
                });
            }
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            File.WriteAllText(Path.ChangeExtension(ProofreadResult, ".completed"), "completed");
        }
    }
    private static async Task PrepareProofread(Player player)
    {
        await Reset(player, visualize: true); player.PlayerCombatState!.Energy = 5;
        await CreatureCmd.SetMaxHp(player.Creature, 70); await CreatureCmd.SetCurrentHp(player.Creature, 70);
        foreach (var foe in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(foe, 300); await CreatureCmd.SetCurrentHp(foe, 300); }
        foreach (var id in new[] { "JK84", "JB01", "JB02", "JA96", "JA38", "JA15" }) await Card(player, id, visualize: true);
        for (int i = 0; i < 5; i++) await Card(player, "JB01", pile: PileType.Draw);
    }
}
