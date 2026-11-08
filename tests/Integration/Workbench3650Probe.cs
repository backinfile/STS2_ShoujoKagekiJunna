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

public static partial class Workbench3650Probe
{
    private sealed class Selector : ICardSelector
    {
        public bool SelfTarget = true;
        public CardModel? Preferred;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            IEnumerable<CardModel> chosen = cards.Any(c => c is JunnaArrowTargetChoice) ? cards.Where(c => SelfTarget ? c is JunnaArrowTargetSelf : c is JunnaArrowTargetEnemy) :
                Preferred != null && cards.Contains(Preferred) ? [Preferred] : cards.Take(maxSelect);
            return Task.FromResult(chosen);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => new() { card = options.First(c => CardCatalog.DesignId(c.Card) == "JK126").Card };
    }
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly string DirectoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/card-fit"));
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


    public static async Task Run(Player player)
    {
        Directory.CreateDirectory(DirectoryPath);
        var results = new List<object>();
        var selector = new Selector();
        using var choices = CardSelectCmd.UseSelector(selector);
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 2;
        async Task Case(string name, Func<Task> test)
        {
            try
            {
                selector.Preferred = null; selector.SelfTarget = true;
                await Reset(player);
                await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
                await test();
                results.Add(new { name, pass = true });
            }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
            File.WriteAllText(Path.Combine(DirectoryPath, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            await Case("r3650 catalog, actual upgrades and native keyword boundaries", async () =>
            {
                Expect(CardCatalog.Revision == 3650 && CardCatalog.Cards.Count == 95 && CardCatalog.Groups.Count == 17, "Catalog snapshot");
                foreach (var id in new[] { "JA104", "JA105", "JH27B", "JG38", "JS19", "JK148", "JK148B", "JK148C", "JK148D", "JK148E", "JK148F" })
                {
                    var card = (CatalogCard)await Card(player, id, pile: PileType.Draw);
                    Expect(card.Title == card.Definition.Name && !card.Title.Contains("LOCERROR"), id + " title");
                    card.UpgradeInternal(); card.FinalizeUpgradeInternal();
                    foreach (var number in card.Definition.Values) Expect(card.Value(number.Name) == number.Upgraded, id + "/" + number.Name);
                }
                Expect(!CardCatalog.Get("JG38").Retain && !CardCatalog.Get("JA105").Exhaust, "Descriptions that grant or preserve keywords");
                Expect(CardCatalog.Get("JK50B").UpgradedRetain && CardCatalog.Get("JK82").Exhaust, "Actual standalone keywords");
                Expect(Shine.GetState(CardCatalog.Create("JK139B", player)) != null, "Retired Shine declaration preserved");
            });
            await Case("星之壁 grants exactly one independent 观星", async () =>
            {
                int before = player.Deck.Cards.Count;
                var main = player.RunState.CreateCard(CardCatalog.Model("JH27"), player);
                await CardPileCmd.Add(main, PileType.Deck);
                Expect(player.Deck.Cards.Count == before + 2, "Two actual permanent cards");
                Expect(CardCatalog.IsKnowledge(main) && CardCatalog.IsKnowledge(player.Deck.Cards.Last(c => CardCatalog.DesignId(c) == "JH27B")), "Both knowledge");
            });
            foreach (bool upgrade in new[] { false, true })
                await Case("观星 real draw and exhaustion " + upgrade, async () =>
                {
                    for (int i = 0; i < 6; i++) await Card(player, "JB01", pile: PileType.Draw);
                    var source = await Card(player, "JH27B", upgrade);
                    await Play(source);
                    Expect(player.PlayerCombatState!.Hand.Cards.Count == (upgrade ? 4 : 3), "Draw count");
                    Expect(source.Pile?.Type == PileType.Exhaust, "Native Exhaust");
                });
            foreach (bool self in new[] { true, false })
                await Case("应变 selects only " + (self ? "self" : "enemy") + " arrows", async () =>
                {
                    var own = await Card(player, "JA15"); await Play(own);
                    var enemy = await Card(player, "JA38"); await Play(enemy);
                    int a = ArrowCombat.Find(own)!.Remaining, b = ArrowCombat.Find(enemy)!.Remaining;
                    selector.SelfTarget = self;
                    await Play(await Card(player, "JA104"));
                    Expect((ArrowCombat.Find(own)?.Remaining ?? 0) == Math.Max(0, a - (self ? 1 : 0)), "Self category");
                    Expect((ArrowCombat.Find(enemy)?.Remaining ?? 0) == Math.Max(0, b - (self ? 0 : 1)), "Enemy category");
                    Expect(player.Creature.Block == 5, "Block resolves first");
                });
            await Case("速射 advances existing deployments and stacks on future arrows", async () =>
            {
                var old = await Card(player, "JA15"); await Play(old);
                int before = ArrowCombat.Find(old)!.Remaining;
                await Play(await Card(player, "JA105"));
                Expect((ArrowCombat.Find(old)?.Remaining ?? 0) == Math.Max(0, before - 1), "Existing countdown advanced immediately");
                Expect(old.Keywords.Contains(CardKeyword.Exhaust), "Existing arrow marked Exhaust");
                var next = await Card(player, "JA15"); await Play(next);
                Expect((ArrowCombat.Find(next)?.Remaining ?? 0) == Math.Max(0, before - 1), "New initial countdown");
                await Finish(next);
                Expect(next.Pile?.Type == PileType.Exhaust, "Completion exhausts");
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA105");
                Expect(!power.Description.GetFormattedText().Contains("LOCERROR"), "Native power description");
            });
            await Case("速射 reaches zero and overrides 再上弦 and 连珠箭 replay", async () =>
            {
                await Play(await Card(player, "JA22"));
                int turns = CardCatalog.Get("JA67").Values.Single(v => v.Name == "Turns").Base;
                for (int i = 0; i < turns; i++) await Play(await Card(player, "JA105"));
                var shot = await Card(player, "JA67");
                int energy = player.PlayerCombatState!.Energy;
                await Play(shot);
                Expect(ArrowCombat.Find(shot) == null && shot.Pile?.Type == PileType.Exhaust, "Immediate single completion");
                Expect(player.PlayerCombatState.Energy == energy - CardCatalog.Get("JA67").Cost, "No paid redeployment");
            });
            await Case("速射 exhausts a temporary ability arrow after installing its effect", async () =>
            {
                for (int i = 0; i < 5; i++) await Play(await Card(player, "JA105"));
                var source = await Card(player, "A");
                await HandArrow.Play(Context, source);
                Expect(source.Pile?.Type == PileType.Exhaust && ArrowCombat.Find(source) == null, "Temporary power arrow consumes");
                Expect(CatalogPower.Has(player, "A"), "Original ability installed");
            });
            await Case("论据 base plays every actual 雄辩", async () =>
            {
                var first = await Card(player, "JK50"); var second = await Card(player, "JK50");
                await Play(await Card(player, "JK50B"));
                Expect(first.Pile?.Type != PileType.Hand && second.Pile?.Type != PileType.Hand, "Both selected models played");
            });
            await Case("论文 binds two acquisitions and advances each true depletion independently", async () =>
            {
                var first = player.RunState.CreateCard(CardCatalog.Model("JK148"), player);
                int start = player.Deck.Cards.Count;
                await CardPileCmd.Add(first, PileType.Deck);
                var members = player.Deck.Cards.Where(c => c != first).Skip(start).ToArray();
                Expect(members.Length == 5, "Five attached actual cards");
                var second = player.RunState.CreateCard(CardCatalog.Model("JK148"), player);
                await CardPileCmd.Add(second, PileType.Deck);
                foreach (var original in members)
                {
                    Shine.SetState(original, 1, 6);
                    var copy = await CombatCopy(player, original);
                    await Play(copy); await Finish(copy);
                }
                Expect(DamageModifiers.CaptureDamageModifierSnapshot(first).ExtraHits == 5, "Five independent permanent increments");
                Expect(DamageModifiers.CaptureDamageModifierSnapshot(second).ExtraHits == 0, "Other acquisition unchanged");
                var restored = player.RunState.LoadCard(first.ToSerializable(), player);
                Expect(DamageModifiers.CaptureDamageModifierSnapshot(restored).ExtraHits == 5, "Research progress saves and restores");
                var temporary = await Card(player, "JK148C");
                Shine.SetState(temporary, 1, 6); await Play(temporary);
                Expect(DamageModifiers.CaptureDamageModifierSnapshot(first).ExtraHits == 5, "Temporary depletion ignored");
            });
            await Case("肌肉记忆 retains first positive bonus once per combat and transfers current investment", async () =>
            {
                var permanent = await Permanent(player, "JG38");
                var current = await CombatCopy(player, permanent);
                DamageModifiers.Add(current, 5); DamageModifiers.Add(current, 3);
                Expect(KnowledgeProgress.PermanentDamageBonus(permanent) == 5, "First gain persisted once");
                Expect(DamageModifiers.CaptureDamageModifierSnapshot(current).DamageBonus == 8, "Current gain counted once");
                var enemy = player.Creature.CombatState!.Enemies.First(); decimal hp = enemy.CurrentHp;
                await Play(current);
                Expect(hp - enemy.CurrentHp == 18, "Two actual hits with full current investment");
                await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
                var next = await CombatCopy(player, permanent);
                Expect(DamageModifiers.CaptureDamageModifierSnapshot(next).DamageBonus == 5, "Next battle restores permanent amount");
                DamageModifiers.Add(next, 2);
                Expect(KnowledgeProgress.PermanentDamageBonus(permanent) == 7, "New battle captures its first gain");
                var restored = player.RunState.LoadCard(permanent.ToSerializable(), player);
                Expect(KnowledgeProgress.PermanentDamageBonus(restored) == 7, "Decimal saved field round-trip");
                var target = await Card(player, "JB01");
                DamageModifiers.TransferBonus([next], target);
                Expect(DamageModifiers.CaptureDamageModifierSnapshot(next).DamageBonus == 0 && DamageModifiers.CaptureDamageModifierSnapshot(target).DamageBonus == 7, "Transfer includes retained bonus");
            });

            await Case("合唱练习 fixed-cost base and upgrade stack their actual damage", async () =>
            {
                await Play(await Card(player, "JG34"));
                await Play(await Card(player, "JG34", true));
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JG34");
                var enemies = player.Creature.CombatState!.Enemies.ToArray();
                var hp = enemies.Select(e => e.CurrentHp).ToArray();
                await power.AfterPlayerTurnStart(Context, player);
                for (int i = 0; i < enemies.Length; i++) Expect(hp[i] - enemies[i].CurrentHp == 7, "3 + 4 damage per enemy");
                Expect(!power.Description.GetFormattedText().Contains("LOCERROR"), "Stacked description");
            });

            await Case("连贯练习 strengthens the current hit and ends with this turn", async () =>
            {
                await Play(await Card(player, "JG31", true));
                var source = await Card(player, "JG38");
                var enemy = player.Creature.CombatState!.Enemies.First();
                decimal before = enemy.CurrentHp;
                await Play(source);
                Expect(before - enemy.CurrentHp == 6, "Two hits already receive +2");
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JG31");
                await power.AfterSideTurnEnd(Context, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [player.Creature]);
                Expect(!CatalogPower.Has(player, "JG31"), "Turn-only source removed");
                var next = await Card(player, "JG38"); before = enemy.CurrentHp;
                await Play(next);
                Expect(before - enemy.CurrentHp == 2, "Following turn has no extra grant");
            });
            await Case("布阵 upgrade stages two selected actual cards before playing", async () =>
            {
                var a = await Card(player, "JB01"); var b = await Card(player, "JB01");
                var source = await Card(player, "JA87", true); await Play(source);
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA87");
                Expect(CardChoices.Prompt(source, 2).GetFormattedText().Contains("2"), "Upgraded selection prompt");
                await power.BeforeFlush(Context, player);
                Expect(ArrowCombat.Find(a)?.Remaining == 5 && ArrowCombat.Find(b)?.Remaining == 5, "Both actual selected cards deployed");
            });
            await Case("学有所成 depletion defers a saved optional copy reward", async () =>
            {
                var source = await Permanent(player, "JS19", true);
                Shine.SetState(source, 1, 9);
                var room = (MegaCrit.Sts2.Core.Rooms.CombatRoom)player.RunState.CurrentRoom!;
                int before = room.ExtraRewards.GetValueOrDefault(player)?.Count ?? 0;
                var target = await Permanent(player, "JH27");
                await Play(await CombatCopy(player, source));
                Expect(!player.Deck.Cards.Contains(source) && player.Deck.Cards.Contains(target), "Only depleted source removed");
                Expect(room.ExtraRewards[player].Count == before + 1, "Reward added without a mid-combat selector");
                var reward = (KnowledgeCopyReward)room.ExtraRewards[player].Last();
                var loaded = Reward.FromSerializable(reward.ToSerializable(), player) as KnowledgeCopyReward;
                Expect(loaded?.Maximum == 2, "Reward type and count restore");
                selector.Preferred = target;
                int count = player.Deck.Cards.Count;
                await loaded!.SelectUnsynchronized();
                Expect(player.Deck.Cards.Count == count + 1, "One selected copy only, no knowledge companions");
                Expect(player.Deck.Cards.Last().Id == target.Id, "Selected actual knowledge copied");
                selector.Preferred = null; count = player.Deck.Cards.Count;
                await new KnowledgeCopyReward(player, 2).SelectUnsynchronized();
                Expect(player.Deck.Cards.Count == count + 2, "Upgrade copies two selected singles");
            });
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            File.WriteAllText(Path.Combine(DirectoryPath, "completed.txt"), "completed");
        }
    }

    public static async Task Prepare(Player player)
    {
        foreach (var current in player.PlayerCombatState!.AllCards.ToArray())
            if (current.Pile != null) await CardPileCmd.RemoveFromCombat(current);
        await Reset(player);
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        Godot.Engine.TimeScale = 1;
        player.PlayerCombatState!.Energy = 25;
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 300); await CreatureCmd.SetCurrentHp(enemy, 300); }
        foreach (var id in new[] { "JA104", "JA105", "JA38", "JH27B", "JG38", "JK148", "JK148D", "JK148F", "JG31" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Hand, player);
        for (int i = 0; i < 6; i++) await Card(player, "JB01", pile: PileType.Draw);
    }
}
