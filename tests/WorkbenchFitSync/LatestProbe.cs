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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class LatestWorkbenchCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_latest";
    public override string Args => "check | prepare";
    public override string Description => "Checks the current workbench changes.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly string ResultPath = Path.GetFullPath("../../../out/tmp/sync-workbench/results.json", Environment.CurrentDirectory);
    private sealed class Selector : ICardSelector
    {
        public bool Knowledge;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            return Task.FromResult(cards.Any(c => c is JunnaPrepareArrows)
                ? cards.Where(c => Knowledge ? c is JunnaPrepareKnowledge : c is JunnaPrepareArrows) : cards.Take(maxSelect));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static async Task Reset(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 100000); await CreatureCmd.SetCurrentHp(enemy, 100000);
            await CreatureCmd.LoseBlock(Context, enemy, enemy.Block, null);
        }
        await CreatureCmd.LoseBlock(Context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState.Energy = 99;
    }
    private static async Task<CardModel> Card(Player player, string id, bool upgraded = false, PileType pile = PileType.Hand)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, skipVisuals: false);
        return card;
    }
    private static Task<bool> Play(CardModel card) => CardActions.PaidPlay(Context, card, card.Owner.Creature.CombatState!.Enemies.First());
    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        var selector = new Selector();
        using var choices = CardSelectCmd.UseSelector(selector);
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 3;
        void Expect(bool pass, string detail) { if (!pass) throw new InvalidOperationException(detail); }
        async Task Case(string name, Func<Task> test)
        {
            try { await Reset(player); await test(); rows.Add(new { name, pass = true }); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
            File.WriteAllText(ResultPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            await Case("Current catalog and new group counts", async () =>
            {
                Expect(CardCatalog.Revision == 4272 && CardCatalog.Cards.Count == 98 && CardCatalog.Groups.Count == 19, "Catalog revision/counts");
                Expect(KnowledgeCombat.Group(await Card(player, "JK235"))!.Members.Sum(c => c.Count) == 3, "Textbook plus two reading cards");
                Expect(CardCatalog.Get("JK11B").Pool && !CardCatalog.Get("JK11").Pool, "Reversed main card");
                Expect(new[] { "JG09", "JF10", "JG34", "JH60" }.All(id => !CardCatalog.Get(id).Pool), "Retired rewards excluded");
            });
            foreach (bool upgraded in new[] { false, true })
            {
                await Case("New card descriptions " + upgraded, async () =>
                {
                    foreach (var id in new[] { "JS33", "JK200", "JK200B", "JK206", "JK206B", "JK235", "JK235B", "JA17" })
                    {
                        var card = await Card(player, id, upgraded, PileType.Draw);
                        Expect(!card.GetDescriptionForPile(PileType.Hand).Contains("ERROR"), id + " description");
                    }
                });
                await Case("Question area damage " + upgraded, async () =>
                {
                    int before = player.Creature.CombatState!.Enemies.Sum(e => e.CurrentHp), count = player.Creature.CombatState.Enemies.Count();
                    await Play(await Card(player, "JK200", upgraded));
                    Expect(before - player.Creature.CombatState.Enemies.Sum(e => e.CurrentHp) == count * (upgraded ? 12 : 8), "Two hits on every enemy");
                });
                await Case("Question support only hand " + upgraded, async () =>
                {
                    var hand = await Card(player, "JB01"); var draw = await Card(player, "JB01", pile: PileType.Draw);
                    var source = await Card(player, "JK200B", upgraded); await Play(source);
                    Expect(player.Creature.Block == (upgraded ? 7 : 5), "Block");
                    Expect(DamageModifiers.CaptureDamageModifierSnapshot(hand).DamageBonus == (upgraded ? 3 : 2) && DamageModifiers.CaptureDamageModifierSnapshot(draw).DamageBonus == 0, "Hand-only bonus");
                    Expect(source.Pile?.Type == PileType.Discard && !source.Keywords.Contains(CardKeyword.Retain), "Current printed keywords");
                });
                foreach (var id in new[] { "JG36", "JK131" })
                    await Case(id + " all combat zones " + upgraded, async () =>
                    {
                        var cards = new List<CardModel>();
                        foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust }) cards.Add(await Card(player, "JB01", pile: pile));
                        var arrow = await Card(player, "JA38"); await Play(arrow); cards.Add(arrow);
                        await Play(await Card(player, id, upgraded));
                        int bonus = id == "JG36" ? upgraded ? 3 : 2 : upgraded ? 4 : 3;
                        Expect(cards.All(c => DamageModifiers.CaptureDamageModifierSnapshot(c).DamageBonus == bonus), "All five zones");
                    });
                await Case("Deck size damage and preview " + upgraded, async () =>
                {
                    var source = (CatalogCard)await Card(player, "JK206", upgraded);
                    int expected = player.Deck.Cards.Count + (upgraded ? 8 : 0), before = player.Creature.CombatState!.Enemies.First().CurrentHp;
                    Expect(source.DynamicVars.CalculatedDamage.Calculate(player.Creature.CombatState.Enemies.First()) == expected, "Dynamic preview");
                    DamageModifiers.Add(source, 2); await Play(source);
                    Expect(before - player.Creature.CombatState.Enemies.First().CurrentHp == expected + 2, "Deck damage with combat bonus");
                });
                await Case("Knowledge search " + upgraded, async () =>
                {
                    var first = await Card(player, "B", pile: PileType.Draw); var second = await Card(player, "B", pile: PileType.Draw);
                    var ordinary = await Card(player, "JB01", pile: PileType.Draw); await Play(await Card(player, "JK206B", upgraded));
                    Expect(player.PlayerCombatState!.Hand.Cards.Count == (upgraded ? 2 : 1) && ordinary.Pile?.Type == PileType.Draw, "Select only knowledge count");
                    Expect(first.Pile?.Type == PileType.Hand || second.Pile?.Type == PileType.Hand, "Actual selected card");
                });
                await Case("Reading block and filtered draw " + upgraded, async () =>
                {
                    var ordinary = await Card(player, "JB01", pile: PileType.Draw); var knowledge = await Card(player, "B", pile: PileType.Draw);
                    await Play(await Card(player, "JK235B", upgraded));
                    Expect(player.Creature.Block == (upgraded ? 11 : 8) && knowledge.Pile?.Type == PileType.Hand && ordinary.Pile?.Type == PileType.Draw, "Block and actual knowledge draw");
                });
                foreach (bool knowledge in new[] { false, true })
                    await Case("Preparation branch " + upgraded + knowledge, async () =>
                    {
                        selector.Knowledge = knowledge;
                        for (int i = 0; i < 4; i++) { await Card(player, "JA38", pile: PileType.Draw); await Card(player, "B", pile: PileType.Draw); }
                        await Play(await Card(player, "JS33", upgraded));
                        var hand = player.PlayerCombatState!.Hand.Cards;
                        Expect(hand.Count == (knowledge ? 2 : upgraded ? 3 : 2), "Branch count");
                        Expect(hand.All(knowledge ? CardCatalog.IsKnowledge : CardCatalog.IsArrow), "Branch filter");
                    });
                await Case("Textbook triggers and stacks " + upgraded, async () =>
                {
                    var source = await Card(player, "JK235", upgraded); DamageModifiers.Add(source, 2); await Play(source);
                    int hp = player.Creature.CombatState!.Enemies.Sum(e => e.CurrentHp);
                    await Play(await Card(player, "JS35"));
                    Expect(player.Creature.CombatState.Enemies.Sum(e => e.CurrentHp) == hp, "Non-knowledge ignored");
                    await Play(await Card(player, "JK49B"));
                    int damage = (upgraded ? 4 : 3) + 2;
                    Expect(hp - player.Creature.CombatState.Enemies.Sum(e => e.CurrentHp) == damage, "Knowledge triggers installed snapshot once");
                    await Play(await Card(player, "JK235"));
                    hp = player.Creature.CombatState.Enemies.Sum(e => e.CurrentHp);
                    await Play(await Card(player, "JK49B"));
                    Expect(hp - player.Creature.CombatState.Enemies.Sum(e => e.CurrentHp) == damage + 3, "Independent stacked grants");
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK235");
                    var description = power.SmartDescription; power.DynamicVars.AddTo(description);
                    Expect(!description.GetFormattedText().Contains("ERROR"), "Power description");
                });
                await Case("Limit training destination " + upgraded, async () =>
                {
                    var source = await Card(player, "JG33", upgraded); await Play(source);
                    Expect(source.Pile?.Type == PileType.Discard && DamageModifiers.CaptureDamageModifierSnapshot(source).DamageBonus == 8, "Normal discard and self investment");
                });
                await Case("Native exhaust and energy updates " + upgraded, async () =>
                {
                    foreach (var id in new[] { "JS34", "JS35" })
                    {
                        var source = await Card(player, id, upgraded); await Play(source);
                        Expect(source.Pile?.Type == PileType.Discard, id + " no exhaust");
                    }
                    await Play(await Card(player, "JA17", upgraded));
                    Expect(player.Creature.GetPower<EnergyNextTurnPower>()!.Amount == (upgraded ? 3 : 2), "Deferred energy");
                    var north = await Card(player, "JH59", upgraded); await Play(north);
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JH59");
                    int energy = player.PlayerCombatState!.Energy; await power.AfterPlayerTurnStart(Context, player);
                    Expect(player.PlayerCombatState.Energy == energy + 1 && north.EnergyCost.GetWithModifiers(CostModifiers.All) == (upgraded ? 0 : 1), "North star cost upgrade");
                    var description = power.SmartDescription; power.DynamicVars.AddTo(description);
                    Expect(description.GetFormattedText().Contains("获得") && description.GetFormattedText().Contains("1"), "North star localized description");
                });
                await Case("Formation count and adaptation power " + upgraded, async () =>
                {
                    await Play(await Card(player, "JA87", upgraded));
                    var source = await Card(player, "JK235"); await Play(await Card(player, "JT02"));
                    Expect(HandArrow.IsArrow(source) && source.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "Adaptation accepts power");
                    var extra = await Card(player, "JB01");
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA87");
                    await power.BeforeFlush(Context, player);
                    Expect(ArrowCombat.Deployed.Count == 1 && extra.Pile?.Type == PileType.Hand, "Formation deploys one card");
                    await ArrowCombat.Accelerate(Context, ArrowCombat.Find(source)!, int.MaxValue);
                    Expect(CatalogPower.Has(player, "JK235"), "Adapted power installs on completion");
                });
                await Case("Formation acceleration " + upgraded, async () =>
                {
                    var arrow = await Card(player, "JA38"); await Play(arrow); int before = ArrowCombat.Find(arrow)!.Remaining;
                    await Play(await Card(player, "JA43", upgraded));
                    int remaining = Math.Max(0, before - (upgraded ? 2 : 1));
                    Expect(remaining == 0 ? ArrowCombat.Find(arrow) == null && arrow.Pile?.Type == PileType.Discard
                        : ArrowCombat.Find(arrow)?.Remaining == remaining, "Accelerated count or completion");
                });
            }
        }
        finally { Godot.Engine.TimeScale = speed; File.WriteAllText(Path.ChangeExtension(ResultPath, ".completed"), "completed"); }
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player); player.PlayerCombatState!.Energy = 9;
        foreach (var id in new[] { "JK235", "JK235B", "JK206", "JK200", "JS33" }) await Card(player, id);
        foreach (var id in new[] { "JA38", "B", "JA57", "B", "JA80", "B" }) await Card(player, id, pile: PileType.Draw);
    }
}
