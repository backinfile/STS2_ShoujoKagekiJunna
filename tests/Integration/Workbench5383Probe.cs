using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class Workbench5383Command : AbstractConsoleCmd
{
    public override string CmdName => "junna_sync_5383";
    public override string Args => "check | prepare";
    public override string Description => "Targeted workbench r5383 effect checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private sealed class Selector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult(options.Take(maxSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static async Task Reset(Player player, bool visuals = false)
    {
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: !visuals); }
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 100000);
            await CreatureCmd.SetCurrentHp(enemy, 100000);
            await TestGameApi.LoseBlock(Context, enemy, enemy.Block, null);
        }
        await TestGameApi.LoseBlock(Context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState.Energy = 20;
    }
    private static async Task<CardModel> Add(Player player, string id, bool upgraded = false,
        PileType pile = PileType.Hand, bool visuals = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, skipVisuals: !visuals);
        return card;
    }
    private static async Task Play(CardModel card)
    {
        if (!await TestGameApi.PaidPlay(Context, card, card.Owner.Creature.CombatState!.HittableEnemies.Last()))
            throw new InvalidOperationException("Play rejected: " + CardCatalog.DesignId(card));
    }
    private static async Task Check(Player player)
    {
        int assertions = 0;
        var results = new List<object>();
        using var selection = TestGameApi.UseSelector(new Selector());
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 6;
        void Equal<T>(T actual, T expected, string message)
        { assertions++; if (!Equals(actual, expected)) throw new InvalidOperationException($"{message}: {actual} != {expected}"); }
        async Task Case(string name, Func<Task> run)
        {
            try { await Reset(player); await run(); results.Add(new { name, pass = true }); }
            catch (Exception e) { results.Add(new { name, pass = false, error = e.ToString() }); }
        }
        try
        {
            foreach (bool up in new[] { false, true })
            {
                await Case("JZ03 deferred all-enemy damage " + up, async () =>
                {
                    var enemies = player.Creature.CombatState!.HittableEnemies.ToArray();
                    Equal(enemies.Length >= 2, true, "Two living enemies required");
                    var before = enemies.Select(e => e.CurrentHp).ToArray();
                    var card = await Add(player, "JZ03", up);
                    Equal(card.TargetType, TargetType.AllEnemies, "No single-enemy target required");
                    await Play(card);
                    Equal(enemies.Select(e => e.CurrentHp).SequenceEqual(before), true, "Deployment has no immediate damage");
                    await ArrowCombat.Accelerate(Context, ArrowCombat.Find(card)!, 1);
                    Equal(enemies.Select((e, i) => before[i] - e.CurrentHp).All(n => n == (up ? 32 : 25)), true, "Every enemy receives the current version's damage");
                });
                await Case("JS35 current block, Innate and Shine " + up, async () =>
                {
                    var card = await Add(player, "JS35", up);
                    Equal(card.Keywords.Contains(CardKeyword.Innate), true, "Both versions remain Innate");
                    Equal(Shine.GetState(card)?.Maximum, 6, "Six Shine preserved");
                    await Play(card);
                    Equal(player.Creature.Block, up ? 15m : 11m, "Current block");
                    Equal(Shine.GetState(card)?.Remaining, 5, "One real play spends one Shine");
                });
                await Case("U_CARD_017 upgraded damage with zero or three categories " + up, async () =>
                {
                    var enemy = player.Creature.CombatState!.HittableEnemies.Last();
                    int hp = enemy.CurrentHp, damage = up ? 8 : 6;
                    await Play(await Add(player, "U_CARD_017", up));
                    Equal(hp - enemy.CurrentHp, damage, "No category: one hit");
                    await Add(player, "JK400"); await Add(player, "JA15"); await Add(player, "JS35");
                    hp = enemy.CurrentHp;
                    await Play(await Add(player, "U_CARD_017", up));
                    Equal(hp - enemy.CurrentHp, damage * 4, "Three categories: four hits");
                });
                await Case("JA177 delayed block before acceleration " + up, async () =>
                {
                    var other = await Add(player, "U_CARD_025"); await Play(other);
                    var card = await Add(player, "JA177", up); await Play(card);
                    Equal(player.Creature.Block, 0m, "No block during deployment");
                    Equal(ArrowCombat.Find(other)?.Remaining, 5, "Other arrow unchanged before completion");
                    await ArrowCombat.Accelerate(Context, ArrowCombat.Find(card)!, 1);
                    Equal(player.Creature.Block, 4m, "Completion grants four block");
                    Equal(ArrowCombat.Find(other)?.Remaining, up ? 3 : 4, "Upgrade changes acceleration only");
                });
                await Case("JA105 versioned haste, exhaustion and stacking " + up, async () =>
                {
                    var existing = await Add(player, "JA81B"); await Play(existing);
                    var power = await Add(player, "JA105", up); int energy = player.PlayerCombatState!.Energy; await Play(power);
                    Equal(player.PlayerCombatState.Energy, energy - 1, "Both versions cost one");
                    Equal(ArrowCombat.Find(existing)?.Remaining, 2, "Installing leaves existing arrows unchanged");
                    Equal(player.Creature.GetPower<JunnaPowerJA105>()!.Amount, up ? 2 : 1, "Displayed haste amount");
                    var fresh = await Add(player, "JA81B"); await Play(fresh);
                    Equal(ArrowCombat.Find(fresh)?.Remaining ?? 0, up ? 0 : 1, "Fresh arrow haste follows actual version");
                    Equal(fresh.Keywords.Contains(CardKeyword.Exhaust), true, "New arrow exhausts");
                    if (up) Equal(fresh.Pile?.Type, PileType.Exhaust, "Immediate completion exhausts once");
                    await Play(await Add(player, "JA105", !up));
                    Equal(player.Creature.GetPower<JunnaPowerJA105>()!.Amount, 3, "Mixed grants display three");
                    var combined = await Add(player, "JA81B"); await Play(combined);
                    Equal(combined.Pile?.Type, PileType.Exhaust, "Mixed grants complete and exhaust");
                });
                await Case("JA53 current block and two actual zero-cost cards " + up, async () =>
                {
                    var zero = await Add(player, "U_CARD_025", pile: PileType.Draw);
                    var second = await Add(player, "JA177", pile: PileType.Draw);
                    var nonmatch = await Add(player, "JB02", pile: PileType.Draw);
                    await Play(await Add(player, "JA53", up));
                    Equal(player.Creature.Block, up ? 14m : 10m, "Current block");
                    Equal(zero.Pile?.Type, PileType.Hand, "First zero-cost actual card");
                    Equal(second.Pile?.Type, PileType.Hand, "Second zero-cost actual card");
                    Equal(nonmatch.Pile?.Type, PileType.Draw, "Positive-cost card remains");
                });
                await Case("JK84 selects three actual cards " + up, async () =>
                {
                    var targets = new List<CardModel>();
                    for (int n = 0; n < 3; n++) targets.Add(await Add(player, "JG03"));
                    var card = await Add(player, "JK84", up); await Play(card);
                    Equal(targets.All(c => ArrowCombat.Find(c)?.Remaining == 5), true, "All three selected cards deployed");
                    Equal(card.Pile?.Type, up ? PileType.Discard : PileType.Exhaust, "Versioned destination");
                });
                await Case("JK133 nine Shine and one spend " + up, async () =>
                {
                    var card = await Add(player, "JK133", up);
                    Equal(Shine.GetState(card)?.Maximum, 9, "Canonical Shine budget");
                    await Play(card);
                    Equal(Shine.GetState(card)?.Remaining, 9, "Deployment preserves Shine until completion");
                    await ArrowCombat.Accelerate(Context, ArrowCombat.Find(card)!, 1);
                    Equal(Shine.GetState(card)?.Remaining, 8, "One completed resolution spends once");
                });
                await Case("JK200B five block and hand-arrow bonus " + up, async () =>
                {
                    var hand = await Add(player, "U_CARD_018");
                    var arrow = await Add(player, "JK200"); await Play(arrow);
                    var draw = await Add(player, "U_CARD_018", pile: PileType.Draw);
                    await Play(await Add(player, "JK200B", up));
                    Equal(player.Creature.Block, 5m, "Upgrade retains five block");
                });
                await Case("U_CARD_016 current cost and upgraded draw " + up, async () =>
                {
                    for (int n = 0; n < 5; n++) await Add(player, "JB02", pile: PileType.Draw);
                    int energy = player.PlayerCombatState!.Energy;
                    await Play(await Add(player, "U_CARD_016", up));
                    Equal(player.PlayerCombatState.Energy, energy - 1, "Current cost one");
                    await Play(await Add(player, "JK200B"));
                    Equal(player.PlayerCombatState.DrawPile.Cards.Count, up ? 3 : 4, "Knowledge trigger draws one or two");
                });
                await Case("JK400 zero effective cost, free payment and mixed grants " + up, async () =>
                {
                    var power = await Add(player, "JK400", up); await Play(power);
                    var meteor = (CatalogCard)await Add(player, "JA81B"); meteor.SetGeneratedDamage(8); await Play(meteor);
                    decimal bonus = up ? 2 : 1;
                    var free = await Add(player, "JB02"); await CardActions.FreePlay(Context, free, player.Creature);
                    var reduced = await Add(player, "JB02"); reduced.EnergyCost.SetThisCombat(0); await Play(reduced);
                    int count = JunnaCombatHooks.CountArrowPlays(player);
                    await ArrowCombat.CopyAll(Context, player);
                    Equal(JunnaCombatHooks.CountArrowPlays(player), count, "Direct deployment copies are not plays");
                    await Play(await Add(player, "JK400", !up));
                    await Play(await Add(player, "JA177"));
                    Equal(player.Creature.GetPower<JunnaPowerJK400>()!.Amount, 3, "Displayed accumulated bonus");
                });
                await Case("JK400B end-turn base meteors and JK400 interaction " + up, async () =>
                {
                    await Play(await Add(player, "JK400", up));
                    var source = await Add(player, "JK400B", up);
                    int energy = player.PlayerCombatState!.Energy; await Play(source);
                    Equal(player.PlayerCombatState.Energy, energy - (up ? 0 : 1), "Only cost changes on upgrade");
                    await Play(await Add(player, "JK400B", !up));
                    var power = player.Creature.GetPower<JunnaPowerJK400B>()!;
                    await power.AfterPlayerTurnStart(Context, player);
                    Equal(ArrowCombat.Deployed.Count, 0, "No start-turn generation");
                    await power.BeforeSideTurnEnd(Context, CombatSide.Enemy, [player.Creature]);
                    Equal(ArrowCombat.Deployed.Count, 0, "Enemy side does not generate");
                    await power.BeforeSideTurnEnd(Context, CombatSide.Player, [player.Creature]);
                    var meteors = ArrowCombat.Deployed.ToArray();
                    Equal(meteors.Length, 2, "One meteor per grant");
                    Equal(meteors.All(a => a.Card is CatalogCard c && c.DesignId == "JA81B" && !c.IsUpgraded && c.DynamicVars.Damage.BaseValue == 8 && a.Remaining == 2 && c.Keywords.Contains(CardKeyword.Exhaust)), true, "Each meteor has base damage, version, wait and exhaust");
                    Equal(JunnaCombatHooks.CountArrowPlays(player), 2, "Generated actual plays enter battle history");
                });
                await Case("U_CARD_030 fixed batch, later growth and history boundaries " + up, async () =>
                {
                    Equal(JunnaCombatHooks.CountArrowPlays(player), 0, "Fresh combat starts at zero");
                    await Play(await Add(player, "U_CARD_030", up));
                    Equal(ArrowCombat.Deployed.Count, 0, "Zero previous plays generate no meteors");
                    var first = await Add(player, "JK133"); await Play(first);
                    Equal(JunnaCombatHooks.CountArrowPlays(player), 1, "An actual arrow play counts once");
                    await ArrowCombat.Accelerate(Context, ArrowCombat.Find(first)!, 1);
                    Equal(JunnaCombatHooks.CountArrowPlays(player), 1, "Completion does not count as a play");
                    await Play(await Add(player, "JA81B"));
                    await ArrowCombat.CopyAll(Context, player);
                    Equal(JunnaCombatHooks.CountArrowPlays(player), 2, "Direct copying is excluded");
                    var prior = ArrowCombat.Deployed.Select(a => a.Card).ToHashSet();
                    var payoff = await Add(player, "U_CARD_030", up);
                    await Play(payoff);
                    var created = ArrowCombat.Deployed.Where(a => !prior.Contains(a.Card)).ToArray();
                    Equal(created.Length, 2, "Batch snapshots two before generation");
                    Equal(created.All(a => a.Card is CatalogCard c && c.DynamicVars.Damage.BaseValue == (up ? 10 : 8) && !c.IsUpgraded && a.Remaining == 2), true, "Base meteor version and source-upgraded damage");
                    Equal(JunnaCombatHooks.CountArrowPlays(player), 4, "New meteors grow the next batch count");
                    prior = ArrowCombat.Deployed.Select(a => a.Card).ToHashSet();
                    await CardActions.FreePlay(Context, payoff);
                    Equal(ArrowCombat.Deployed.Count(a => !prior.Contains(a.Card)), 4, "Next complete resolution snapshots four");
                    Equal(JunnaCombatHooks.CountArrowPlays(player), 8, "Next batch has finite growth");
                });
                foreach (string id in new[] { "U_CARD_018", "U_CARD_021", "U_CARD_023" })
                    await Case(id + " current initial and loop payload " + up, async () =>
                    {
                        for (int n = 0; n < 4; n++) await Add(player, "JB02", pile: PileType.Draw);
                        var card = await Add(player, id, up);
                        var enemy = player.Creature.CombatState!.HittableEnemies.Last();
                        int damage = id == "U_CARD_018" ? up ? 15 : 11 : id == "U_CARD_021" ? up ? 11 : 8 : 0;
                        int block = id == "U_CARD_023" ? up ? 10 : 7 : 0;
                        int hp = enemy.CurrentHp;
                        await Play(card);
                        Equal(hp - enemy.CurrentHp, damage, "Initial damage");
                        Equal(player.Creature.Block, (decimal)block, "Initial block");
                        Equal(ArrowCombat.Find(card)?.Remaining, 5, "Initial five-turn deployment");
                        await ArrowCombat.Accelerate(Context, ArrowCombat.Find(card)!, 5);
                        Equal(hp - enemy.CurrentHp, damage * 2, "Repeated damage");
                        Equal(player.Creature.Block, (decimal)block * 2, "Repeated block");
                        Equal(ArrowCombat.Find(card)?.Remaining, 5, "Redeployment remains five turns");
                    });
            }
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            string output = Path.GetFullPath("../../../out/tmp/sync-workbench-20261117/targeted-results.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(new { revision = CardCatalog.Revision, completed = true, assertions, results }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player, true);
        await CreatureCmd.SetMaxHp(player.Creature, 200); await CreatureCmd.SetCurrentHp(player.Creature, 200);
        foreach (string id in new[] { "JZ03", "JK127", "JB03", "JB06", "JK400", "JK400B", "JS35", "U_CARD_017", "JA71" })
            await Add(player, id, id == "U_CARD_017", visuals: true);
        for (int n = 0; n < 8; n++) await Add(player, "JB02", pile: PileType.Draw, visuals: true);
    }
}
