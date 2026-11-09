using System.Text.Json;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowBatchBlockProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_batch_block";
    public override string Args => "";
    public override string Description => "Check each arrow in a selected batch triggers Arrow Formation independently.";
    public override bool IsNetworked => false;
    private sealed class Selector(CardModel[] selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult(options.Where(selected.Contains).Take(maxSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Arrow batch block checks started");
    private static async Task Check(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        var rows = new List<object>();
        void Expect(string name, decimal actual, decimal expected) => rows.Add(new { name, actual, expected, pass = actual == expected });
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        { await CreatureCmd.SetMaxHp(enemy, 1000); await CreatureCmd.SetCurrentHp(enemy, 1000); }
        foreach (bool upgraded in new[] { false, true })
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            using var single = KnowledgeCombat.SingleCard();
            var formation = CardCatalog.Create("JA118", player);
            if (upgraded) { formation.UpgradeInternal(); formation.FinalizeUpgradeInternal(); }
            var source = CardCatalog.Create("JA88", player);
            var selected = new[] { CardCatalog.Create("JB01", player), CardCatalog.Create("JB02", player), CardCatalog.Create("JB03", player) };
            await CardPileCmd.Add(selected.Append(source).Append(formation), PileType.Hand, skipVisuals: true);
            player.PlayerCombatState!.Energy = 20;
            await CardActions.PaidPlay(context, formation);
            using var selector = CardSelectCmd.UseSelector(new Selector(selected));
            await CardActions.PaidPlay(context, source);
            int amount = upgraded ? 4 : 3;
            Expect($"upgraded={upgraded} selected arrows deploy", selected.Count(c => ArrowCombat.Find(c)?.Remaining == 5), 3);
            Expect($"upgraded={upgraded} three plays each grant block", player.Creature.Block, 3 * amount);
            foreach (var card in selected)
                if (ArrowCombat.Find(card) is { } arrow) await ArrowCombat.Accelerate(context, arrow, 5);
            Expect($"upgraded={upgraded} countdown resolution grants only the skill's block", player.Creature.Block, 3 * amount + 5);
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "arrow-batch-block-results.json")),
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
