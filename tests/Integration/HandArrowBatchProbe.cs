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
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class HandArrowBatchProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_batch";
    public override string Args => "prepare";
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
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Batch checks started");

    private static async Task Prepare(Player player)
    {
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        }
        using var single = KnowledgeCombat.SingleCard();
        var state = player.Creature.CombatState!;
        var cards = new[]
        {
            state.CreateCard(ModelDb.Card<Dazed>(), player), state.CreateCard(ModelDb.Card<Injury>(), player),
            CardCatalog.Create("JB02", player), CardCatalog.Create("JK133", player),
            CardCatalog.Create("JA88", player), CardCatalog.Create("JA94", player)
        };
        await CardPileCmd.Add(cards, PileType.Hand, skipVisuals: false);
        await CreatureCmd.SetMaxHp(player.Creature, 300);
        await CreatureCmd.SetCurrentHp(player.Creature, 300);
        player.PlayerCombatState!.Energy = 20;
    }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        var context = new ThrowingPlayerChoiceContext();
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            await CreatureCmd.SetMaxHp(enemy, 1000);
            await CreatureCmd.SetCurrentHp(enemy, 1000);
        }
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
        using var single = KnowledgeCombat.SingleCard();
        var state = player.Creature.CombatState!;
        var selected = new[]
        {
            CardCatalog.Create("JB01", player), CardCatalog.Create("JB02", player),
            state.CreateCard(ModelDb.Card<Inflame>(), player),
            state.CreateCard(ModelDb.Card<Whirlwind>(), player),
            state.CreateCard(ModelDb.Card<Dazed>(), player),
            state.CreateCard(ModelDb.Card<Injury>(), player),
            CardCatalog.Create("JB03", player)
        };
        selected[0].AddKeyword(CardKeyword.Unplayable);
        var originalTypes = selected.Select(card => card.Type).ToArray();
        var originalKeywords = selected.Select(card => card.Keywords.ToHashSet()).ToArray();
        var unselected = CardCatalog.Create("JF10", player);
        var source = CardCatalog.Create("JA88", player);
        await CardPileCmd.Add(selected.Append(unselected).Append(source), PileType.Hand, skipVisuals: true);
        player.PlayerCombatState!.Energy = 20;
        int beforeEnergy = player.PlayerCombatState.Energy;
        var target = state.HittableEnemies.First();
        int beforeHp = target.CurrentHp;
        int beforeBlock = player.Creature.Block;
        Expect("ordinary play still rejects unplayable material", !await CardActions.PaidPlay(context, selected[0], target));
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
            Expect("source enters its result pile", source.Pile?.Type == (source.Keywords.Contains(CardKeyword.Exhaust) ? PileType.Exhaust : PileType.Discard));
            Expect("material cards spend no energy", player.PlayerCombatState.Energy == beforeEnergy - CardActions.Cost(source));
            Expect("material effects stay deferred", player.Creature.Block == beforeBlock && !player.Creature.Powers.Any());
            int beforeMaterialHp = target.CurrentHp;
            for (int i = 0; i < selected.Length; i++)
            {
                var material = selected[i];
                Expect(material.Id + " deploys for five turns", ArrowCombat.Find(material)?.Remaining == 5);
                Expect(material.Id + " keywords survive deployment", material.Keywords.SetEquals(originalKeywords[i]));
                if (ArrowCombat.Find(material) is { } arrow) await ArrowCombat.Accelerate(context, arrow, 5);
                if (i == 0) Expect("unplayable attack resolves its damage", target.CurrentHp < beforeMaterialHp);
                Expect(material.Id + " resolves and leaves arrow pile", ArrowCombat.Find(material) == null && material.Pile?.Type != ArrowPile.PileId);
                Expect(material.Id + " restores original type", material.Type == originalTypes[i]);
                Expect(material.Id + " preserves keywords after resolution", material.Keywords.SetEquals(originalKeywords[i]));
            }
            Expect("attacks resolve damage", target.CurrentHp < beforeHp);
            Expect("skill resolves block", player.Creature.Block > beforeBlock);
            Expect("power resolves its original effect", player.Creature.Powers.Any());
            foreach (bool upgraded in new[] { false, true })
            {
                var starfall = CardCatalog.Create("JK133", player);
                if (upgraded) { starfall.UpgradeInternal(); starfall.FinalizeUpgradeInternal(); }
                await CardPileCmd.Add(starfall, PileType.Hand, skipVisuals: true);
                Expect($"starfall upgraded={upgraded} starts at shine six", Shine.GetState(starfall) is { Maximum: 6, Remaining: 6 });
                await CardActions.PaidPlay(context, starfall, target);
                Expect($"starfall upgraded={upgraded} deploy preserves shine", Shine.GetState(starfall)?.Remaining == 6);
                if (ArrowCombat.Find(starfall) is { } arrow) await ArrowCombat.Accelerate(context, arrow, 1);
                Expect($"starfall upgraded={upgraded} resolution spends one shine", Shine.GetState(starfall)?.Remaining == 5);
            }
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
