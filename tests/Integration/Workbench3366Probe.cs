using System.Text.Json;
using BaseLib.Patches.Saves;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public static class Workbench3366Probe
{
    private sealed class Selector : ICardSelector
    {
        public bool Energy;
        public bool Skip;
        public CardModel? Preferred;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            IEnumerable<CardModel> chosen = Skip && minSelect == 0 ? [] :
                Energy && cards.Any(c => c is JunnaNotesEnergy) ? cards.Where(c => c is JunnaNotesEnergy) :
                Preferred != null && cards.Contains(Preferred) ? [Preferred] : cards.Take(maxSelect);
            return Task.FromResult(chosen);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => new() { card = options.First(c => CardCatalog.DesignId(c.Card) == "JK126").Card };
    }
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly string DirectoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/workbench3366"));
    private static void Expect(bool pass, string error) { if (!pass) throw new InvalidOperationException(error); }

    private static async Task Reset(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 1000000); await CreatureCmd.SetCurrentHp(enemy, 1000000);
            await TestGameApi.LoseBlock(Context, enemy, enemy.Block, null);
        }
        await TestGameApi.LoseBlock(Context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState.Energy = 99;
    }
    private static async Task<CardModel> Card(Player player, string id, bool upgraded = false, PileType pile = PileType.Hand)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, skipVisuals: true);
        return card;
    }
    private static async Task<CardModel> Permanent(Player player, string id, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = player.RunState.CreateCard(CardCatalog.Model(id), player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, PileType.Deck, skipVisuals: true);
        return card;
    }
    private static async Task<CardModel> CombatCopy(Player player, CardModel permanent)
    {
        var card = player.Creature.CombatState!.CloneCard(permanent);
        card.DeckVersion = permanent;
        using var single = KnowledgeCombat.SingleCard();
        await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
        return card;
    }
    private static Task<bool> Play(CardModel card) => CardActions.PaidPlay(Context, card, card.Owner.Creature.CombatState!.Enemies.First());
    private static Task Finish(CardModel card) => ArrowCombat.Find(card) is { } arrow
        ? ArrowCombat.Accelerate(Context, arrow, int.MaxValue) : Task.CompletedTask;

    public static async Task Run(Player player, string? only = null)
    {
        Directory.CreateDirectory(DirectoryPath);
        var results = new List<object>();
        var selector = new Selector();
        using var choices = CardSelectCmd.UseSelector(selector);
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 1;
        void Save() => File.WriteAllText(Path.Combine(DirectoryPath, string.IsNullOrEmpty(only) ? "results.json" : "targeted-results.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        async Task Case(string name, Func<Task> test)
        {
            if (!string.IsNullOrEmpty(only) && !name.Contains(only, StringComparison.Ordinal)) return;
            try
            {
                selector.Energy = false; selector.Skip = false; selector.Preferred = null;
                await Reset(player); await test();
                results.Add(new { name, pass = true });
            }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
            Save();
        }
        try
        {
            await Case("r3366 metadata, upgrade values and legacy models", async () =>
            {
                Expect(CardCatalog.Revision == 3366 && CardCatalog.Cards.Count == 115 && CardCatalog.Groups.Count == 23, "Catalog counts");
                foreach (var id in new[] { "JK112", "JK112B", "JS19", "JK122", "JK122B", "JK126", "JK127", "JK130", "JK130B", "JK131", "JK131B", "JK133", "JK137", "JK139", "JK139B" })
                {
                    var card = (CatalogCard)await Card(player, id);
                    Expect(card.Title == card.Definition.Name && card.TargetType == card.Definition.TargetType, id);
                    Expect(!card.HoverTips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>().Any(t => t.Description?.Contains("LOCERROR") == true), id + " hover localization");
                    card.UpgradeInternal(); card.FinalizeUpgradeInternal();
                    foreach (var number in card.Definition.Values)
                        Expect(card.DynamicVars[number.Name].BaseValue == number.Upgraded, id + "/" + number.Name);
                }
                foreach (var id in new[] { "JK15", "JK15B", "JK44", "JK44B", "JZ06" })
                    Expect(!CardCatalog.Get(id).Pool && CardCatalog.Model(id) != null, "Old save model " + id);
            });
            foreach (bool up in new[] { false, true })
            {
                await Case("new attacks and blocks " + up, async () =>
                {
                    var enemy = player.Creature.CombatState!.Enemies.First();
                    foreach (var (id, expected) in new[] { ("JK112B", 8), ("JK122", up ? 22 : 16), ("JK130B", up ? 24 : 18), ("JK131B", up ? 80 : 60) })
                    {
                        var card = await Card(player, id, up); int hp = enemy.CurrentHp;
                        await Play(card); await Finish(card);
                        Expect(hp - enemy.CurrentHp == expected, id + " damage");
                    }
                    await Play(await Card(player, "JK130", up));
                    await Play(await Card(player, "JS19", up));
                    Expect(player.Creature.Block == (up ? 11 : 8) + 18, "Block " + player.Creature.Block);
                    var fallacy = await Card(player, "JK122B", up); await Play(fallacy);
                    Expect(fallacy.Pile?.Type == PileType.Exhaust && Shine.GetState(fallacy)?.Remaining == 2, "Fallacy exhaust and shine");
                });
                await Case("引言 and 分句 normal discard " + up, async () =>
                {
                    for (int i = 0; i < 8; i++) await Card(player, "JB01", pile: PileType.Draw);
                    var intro = await Card(player, "JK131", up); await Play(intro);
                    Expect(player.PlayerCombatState!.Hand.Cards.Count == (up ? 4 : 3) && intro.Pile?.Type == PileType.Discard, "Introduction draw/discard");
                    var clause = await Card(player, "JK133", up); int hp = player.Creature.CombatState!.Enemies.First().CurrentHp;
                    await Play(clause); Expect(Shine.GetState(clause)?.Remaining == 9, "Deployment spends no shine");
                    await Finish(clause);
                    Expect(hp - player.Creature.CombatState.Enemies.First().CurrentHp == (up ? 12 : 9) && Shine.GetState(clause)?.Remaining == 8 && clause.Pile?.Type == PileType.Discard, "Completion damage, draw, shine");
                });
                foreach (bool energy in new[] { false, true })
                    await Case("手札 choice " + up + "/" + energy, async () =>
                    {
                        selector.Energy = energy;
                        for (int i = 0; i < 4; i++) await Card(player, "JB01", pile: PileType.Draw);
                        var notes = await Card(player, "JK137", up); int before = player.PlayerCombatState!.Energy;
                        await Play(notes);
                        Expect(energy ? player.PlayerCombatState.Energy == before + (up ? 3 : 2) :
                            player.PlayerCombatState.Hand.Cards.Count == (up ? 3 : 2), "Choice payload");
                        Expect(notes.Pile?.Type == PileType.Discard && Shine.GetState(notes)?.Remaining == 5, "Notes discard and shine");
                    });
                await Case("领悟 per-turn energy " + up, async () =>
                {
                    await Play(await Card(player, "JK139", up));
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK139");
                    int before = player.PlayerCombatState!.Energy;
                    await power.AfterPlayerTurnStart(Context, player);
                    Expect(player.PlayerCombatState.Energy == before + (up ? 2 : 1), "Energy");
                    Expect(!power.Description.GetFormattedText().Contains("LOCERROR"), "Power localization");
                });
                await Case("重订 persistent hits, serialization and temporary sources " + up, async () =>
                {
                    var target = await Permanent(player, "JK112B");
                    var source = await Permanent(player, "JK112", up);
                    selector.Preferred = target;
                    var combatTarget = await CombatCopy(player, target);
                    await Play(await CombatCopy(player, source));
                    Expect(KnowledgeProgress.PermanentHits(target) == 1 && KnowledgeProgress.PermanentHits(combatTarget) == 1, "Permanent growth target=" + KnowledgeProgress.PermanentHits(target) + " combat=" + KnowledgeProgress.PermanentHits(combatTarget) + " source=" + Shine.GetState(source));
                    var loaded = CardModel.FromSerializable(target.ToSerializable());
                    Expect(KnowledgeProgress.PermanentHits(loaded) == 1, "Saved growth");
                    DamageModifiers.Add(combatTarget, 2); int hp = player.Creature.CombatState!.Enemies.First().CurrentHp;
                    await Play(combatTarget);
                    Expect(hp - player.Creature.CombatState.Enemies.First().CurrentHp == 18, "Three hits with damage bonus");
                    await CardPileCmd.Add(combatTarget, PileType.Hand, skipVisuals: true);
                    selector.Preferred = combatTarget;
                    await Play(await Card(player, "JK112", up));
                    Expect(KnowledgeProgress.PermanentHits(target) == 1 && DamageModifiers.CaptureDamageModifierSnapshot(combatTarget).ExtraHits == 2, "Temporary source only combat growth");
                });
            }
            foreach (var (id, expected) in new[] { ("JK133", 3), ("JK137", 2) })
                await Case("same-model group " + id, async () =>
                {
                    int before = player.Deck.Cards.Count;
                    var main = player.RunState.CreateCard(CardCatalog.Model(id), player);
                    await CardPileCmd.Add(main, PileType.Deck, skipVisuals: true);
                    Expect(player.Deck.Cards.Count == before + expected, "Physical acquisition count");
                    Expect(KnowledgePreviewCards.Create(main).Count == expected, "Preview count");
                    var copies = player.Deck.Cards.Skip(before).ToArray();
                    Shine.SetState(copies[0], 1, expected == 3 ? 9 : 6);
                    Expect(copies.Skip(1).All(c => Shine.GetState(c)!.Remaining > 1), "Independent Shine budgets");
                    var selected = await CombatCopy(player, copies[0]);
                    foreach (var other in copies.Skip(1))
                    {
                        var combat = await CombatCopy(player, other);
                        await CardPileCmd.Add(combat, PileType.Draw, skipVisuals: true);
                    }
                    await KnowledgeCombat.DrawPartners(Context, selected);
                    Expect(player.PlayerCombatState!.Hand.Cards.Count == expected, "Same-model partner draw");
                });
            await Case("dynamic choice preserves upgrades, ordinary cards and static groups", async () =>
            {
                var source = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
                var ordinary = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
                ordinary.UpgradeInternal(); ordinary.FinalizeUpgradeInternal();
                var entrance = player.RunState.CreateCard(CardCatalog.Model("A"), player);
                var chosen = await CardSelectCmd.FromChooseACardScreen(Context, [source, ordinary, entrance], player);
                await CardPileCmd.Add(chosen!, PileType.Deck, skipVisuals: true);
                Expect(DynamicKnowledge.Members(source).Count == 3 && ordinary.IsUpgraded && CardCatalog.IsKnowledge(ordinary), "Actual relation and original upgrade");
                Expect(player.Deck.Cards.Count(c => CardCatalog.DesignId(c) == "B") >= 2, "Static companions");
                var save = CardModel.FromSerializable(ordinary.ToSerializable());
                Expect(DynamicKnowledge.IsLinked(save) && save.Keywords.Contains(JunnaKeywords.Knowledge), "Saved non-knowledge card membership");
                var second = await Permanent(player, "JK126");
                Expect(DynamicKnowledge.Members(second).Count == 0, "Independent unoffered entrance");
                var combatSource = await CombatCopy(player, source);
                var combatOther = await CombatCopy(player, ordinary);
                await CardPileCmd.Add(combatOther, PileType.Draw, skipVisuals: true);
                await Play(combatSource);
                Expect(combatOther.Pile?.Type == PileType.Hand && player.Creature.Block == 8, "Draw actual dynamic partner");
            });
            await Case("reward acquisition chooses two of four alternatives without recursion", async () =>
            {
                var options = new[] { "JK126", "JK126", "JB01", "JB02", "JK130" }
                    .Select(id => player.RunState.CreateCard(CardCatalog.Model(id), player)).ToArray();
                int before = player.Deck.Cards.Count;
                var reward = new CardReward(options, CardCreationSource.Other, player,
                    new CardCreationOptions([], CardCreationSource.Other, MegaCrit.Sts2.Core.Runs.CardRarityOddsType.Uniform));
                await reward.SelectUnsynchronized();
                Expect(DynamicKnowledge.Members(options[0]).Count == 3 && player.Deck.Cards.Count == before + 3, "Reward adds exactly two chosen others");
                Expect(DynamicKnowledge.Members(options[1]).Count == 3 && !player.Deck.Cards.Contains(options[3]), "Attached dynamic entrance does not recursively recruit");
            });
            await Case("誊写 copies one card with current shine budget and supports skipping", async () =>
            {
                var source = await Permanent(player, "JK137", true);
                Shine.SetState(source, 2, 6);
                selector.Preferred = source;
                int count = player.Deck.Cards.Count;
                await KnowledgeProgress.CopyDeckCard(player);
                var copy = player.Deck.Cards.Last();
                Expect(player.Deck.Cards.Count == count + 1 && copy != source && copy.IsUpgraded && Shine.GetState(copy)?.Remaining == 2, "Single permanent copy keeps budget");
                selector.Skip = true; selector.Preferred = null;
                await KnowledgeProgress.CopyDeckCard(player);
                Expect(player.Deck.Cards.Count == count + 1, "Optional skip");
            });
            await Case("誊写 registered reward survives native removal of combat powers", async () =>
            {
                var source = await Permanent(player, "JB02");
                selector.Preferred = source;
                await Play(await Card(player, "JK127"));
                foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
                int count = player.Deck.Cards.Count;
                await KnowledgeProgress.WonCombat();
                Expect(player.Deck.Cards.Count == count + 1, "Reward remains after combat power cleanup");
                await KnowledgeProgress.WonCombat();
                Expect(player.Deck.Cards.Count == count + 1, "Victory callback is idempotent");
            });
            await Case("学有所成 real depletion optional permanent removal", async () =>
            {
                var source = await Permanent(player, "JS19");
                var target = await Permanent(player, "JB01");
                selector.Preferred = target;
                Shine.SetState(source, 1, 9);
                await Play(await CombatCopy(player, source));
                Expect(!player.Deck.Cards.Contains(source) && !player.Deck.Cards.Contains(target), "Source and selected permanent card removed");
                Expect(player.Creature.Block == 18, "Last block resolves before depletion");
                var eternal = await Permanent(player, "JB01");
                eternal.AddKeyword(CardKeyword.Eternal);
                selector.Preferred = eternal;
                var second = await Permanent(player, "JS19");
                Shine.SetState(second, 1, 9);
                await Play(await CombatCopy(player, second));
                Expect(player.Deck.Cards.Contains(eternal), "Eternal cards cannot be chosen for removal");
            });
            await Case("推导 depletion registers independently persisted base opening reward", async () =>
            {
                var source = await Permanent(player, "JK139B", true);
                Shine.SetState(source, 1, 6);
                await Play(await CombatCopy(player, source));
                Expect(!player.Deck.Cards.Contains(source), "True source depletion");
                var save = player.ToSerializable();
                var extended = ExtendedSaveHandlers<Player, SerializablePlayer>.ExtendedData[save].DictForType<int>();
                Expect(extended.GetValueOrDefault("spirefield_Player_JunnaOpeningInsights") == 1, "Player save contains reward after source removal");
                var fields = typeof(KnowledgeProgress).GetField("OpeningInsights", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
                var get = fields.GetType().GetMethod("Get")!;
                Expect((int)get.Invoke(fields, [player])! == 1, "One reward from one depletion");
                await Play(await Card(player, "JK139B"));
                Expect((int)get.Invoke(fields, [player])! == 1, "Temporary source gives no permanent reward");
                KnowledgeProgress.ResetCombat();
                var hooks = ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>());
                await hooks.BeforeHandDraw(player, Context, player.Creature.CombatState!);
                var insight = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK139");
                Expect(insight.Amount == 1, "Upgraded deduction produces base Insight");
                await hooks.BeforeHandDraw(player, Context, player.Creature.CombatState!);
                Expect(insight.Amount == 1, "Only once per combat opening");
            });
        }
        finally { Godot.Engine.TimeScale = speed; Save(); }
        File.WriteAllText(Path.Combine(DirectoryPath, "completed.txt"), "completed");
    }

    public static async Task Prepare(Player player)
    {
        foreach (var current in player.PlayerCombatState!.AllCards.ToArray())
            if (current.Pile != null) await CardPileCmd.RemoveFromCombat(current);
        await Reset(player);
        Godot.Engine.TimeScale = 1;
        player.PlayerCombatState!.Energy = 12;
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 300); await CreatureCmd.SetCurrentHp(enemy, 300); }
        using var single = KnowledgeCombat.SingleCard();
        foreach (var id in new[] { "JK139", "JK133", "JK131", "JK137", "JK112", "JK112B", "JA71", "JK130B", "JK127" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Hand, player);
        for (int i = 0; i < 6; i++)
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JB01", player), PileType.Draw, player);
    }
}
