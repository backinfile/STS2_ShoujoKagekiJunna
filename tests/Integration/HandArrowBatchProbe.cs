using System.Text.Json;
using HarmonyLib;
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

public sealed class HandArrowBatchProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_batch";
    public override string Args => "";
    public override string Description => "Check that the chosen hand-arrow batch moves before its first play.";
    public override bool IsNetworked => false;
    private static Action<CardModel>? _observePlay;
    private static void BeforePlay(CardModel card) => _observePlay?.Invoke(card);

    private sealed class Selector(CardModel[] selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult(options.Where(selected.Contains).Take(maxSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }

    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Batch checks started");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        var context = new ThrowingPlayerChoiceContext();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
        using var single = KnowledgeCombat.SingleCard();
        var selected = new[] { CardCatalog.Create("JB01", player), CardCatalog.Create("JB02", player) };
        var unselected = CardCatalog.Create("JF10", player);
        var source = CardCatalog.Create("JA88", player);
        await CardPileCmd.Add(selected.Append(unselected).Append(source), PileType.Hand, skipVisuals: true);
        player.PlayerCombatState!.Energy = 20;
        using var selectorScope = CardSelectCmd.UseSelector(new Selector(selected));
        var harmony = new Harmony("JunnaIntegration.HandArrowBatch");
        int started = 0;
        _observePlay = _ =>
        {
            if (started++ == 0)
                Expect("entire chosen batch is in play before the first effect", selected.All(card => card.Pile?.Type == PileType.Play));
        };
        try
        {
            harmony.Patch(AccessTools.Method(typeof(HandArrow), nameof(HandArrow.Play)), prefix: new HarmonyMethod(typeof(HandArrowBatchProbe), nameof(BeforePlay)));
            await CardActions.PaidPlay(context, source);
            Expect("each chosen card plays exactly once", started == selected.Length);
            Expect("all chosen cards deploy", selected.All(card => ArrowCombat.Find(card)?.Remaining == 5));
            Expect("unchosen card stays in hand", unselected.Pile?.Type == PileType.Hand);
            Expect("source exhausts", source.Pile?.Type == PileType.Exhaust);
        }
        finally
        {
            _observePlay = null;
            harmony.UnpatchAll(harmony.Id);
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "..", "hand-arrow-batch-results.json")),
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
