using System.Text.Json;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

[CustomID("JUNNA_LOOP_SYNC_OBSERVER")]
public sealed class LoopSyncObserver : AbstractModel
{
    internal static readonly List<(CardModel Card, CardType Type, int Spent)> Plays = [];
    public override bool ShouldReceiveCombatHooks => true;
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.IsFirstInSeries) Plays.Add((play.Card, play.Card.Type, play.Resources.EnergySpent));
        return Task.CompletedTask;
    }
}

public sealed class LoopSyncCommand : AbstractConsoleCmd
{
    public static void Initialize() => ModHelper.SubscribeForCombatStateHooks("JunnaLoopSync",
        _ => [ModelDb.GetById<LoopSyncObserver>(ModelDb.GetId<LoopSyncObserver>())]);
    public override string CmdName => "junna_loop_sync";
    public override string Args => "check | prepare";
    public override string Description => "Targeted r5324 loop, balance and gem checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly string[] Ids = ["U_CARD_018", "U_CARD_021", "U_CARD_023", "U_CARD_024", "U_CARD_025"];
    private static async Task Reset(Player player, bool visuals = false)
    {
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: !visuals); }
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 100000);
            await CreatureCmd.SetCurrentHp(enemy, 100000);
            await TestGameApi.LoseBlock(Context, enemy, enemy.Block, null);
        }
        await TestGameApi.LoseBlock(Context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState.Energy = 20;
        LoopSyncObserver.Plays.Clear();
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
    private static Task<bool> Play(CardModel card) => TestGameApi.PaidPlay(Context, card,
        card.Owner.Creature.CombatState!.HittableEnemies.Last());
    private static async Task Check(Player player)
    {
        var results = new List<object>();
        int assertions = 0;
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 8;
        string path = Path.GetFullPath("../../../artifacts/task-deliverables/sync-workbench-latest-20261116/targeted-results.json");
        void Equal<T>(T actual, T expected, string message)
        { assertions++; if (!Equals(actual, expected)) throw new InvalidOperationException($"{message}: {actual} != {expected}"); }
        async Task Case(string name, Func<Task> run)
        {
            try { await Reset(player); await run(); results.Add(new { name, pass = true }); }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
        }
        try
        {
            foreach (bool upgraded in new[] { false, true })
            foreach (string id in Ids)
                await Case(id + " initial, two loops and natural tick " + upgraded, async () =>
                {
                    for (int n = 0; n < 12; n++) await Add(player, "JB02", pile: PileType.Draw);
                    var card = await Add(player, id, upgraded);
                    var originalType = card.Type;
                    var target = player.Creature.CombatState!.HittableEnemies.Last();
                    int hp = target.CurrentHp, energy = player.PlayerCombatState!.Energy;
                    int damage = id == "U_CARD_018" ? upgraded ? 14 : 10 : id == "U_CARD_021" ? upgraded ? 12 : 9 : 0;
                    int block = id == "U_CARD_023" ? upgraded ? 6 : 4 : 0;
                    int draws = id == "U_CARD_023" ? 1 : id == "U_CARD_024" ? upgraded ? 3 : 2 : 0;
                    int gain = id == "U_CARD_025" ? upgraded ? 2 : 1 : 0;
                    Equal(await Play(card), true, "Paid initial play");
                    var first = ArrowCombat.Find(card)!;
                    Equal(first?.Remaining, 5, "Initial wait");
                    if (id == "U_CARD_018") Equal(first?.Target, target, "Initial target inherited");
                    Equal(card.Type, ArrowPile.ArrowType, "Temporary arrow type");
                    Equal(HandArrow.OriginalType(card), originalType, "Internal type preserved");
                    Equal(hp - target.CurrentHp, damage, "Immediate damage");
                    Equal(player.Creature.Block, (decimal)block, "Immediate block");
                    Equal(12 - player.PlayerCombatState.DrawPile.Cards.Count, draws, "Immediate draw");
                    Equal(player.PlayerCombatState.Energy, energy - (id == "U_CARD_025" ? 0 : 1) + gain, "Initial payment only");
                    Equal(LoopSyncObserver.Plays.Where(p => p.Card == card).Select(p => p.Type).SequenceEqual(new[] { originalType, ArrowPile.ArrowType }), true, "Original and real arrow plays");
                    Equal(LoopSyncObserver.Plays.Where(p => p.Card == card).Sum(p => p.Spent), id == "U_CARD_025" ? 0 : 1, "Only first play paid");
                    await ArrowCombat.Accelerate(Context, first!, 4);
                    Equal(first!.Remaining, 1, "Partial acceleration");
                    Equal(hp - target.CurrentHp, damage, "No early payload");
                    await ArrowCombat.Accelerate(Context, first, 1);
                    var second = ArrowCombat.Find(card)!;
                    Equal(ReferenceEquals(first, second), false, "New deployment");
                    Equal(second?.Remaining, 5, "New deployment not accelerated");
                    if (id == "U_CARD_018") Equal(second?.Target, target, "Renewed target inherited");
                    Equal(hp - target.CurrentHp, damage * 2, "First completed payload");
                    await ArrowCombat.AccelerateAll(Context, player, 1000);
                    Equal(ArrowCombat.Deployed.Count(a => a.Card == card), 1, "One actual card and deployment");
                    Equal(ArrowCombat.Find(card)?.Remaining, 5, "Snapshot acceleration affects one generation");
                    Equal(hp - target.CurrentHp, damage * 3, "Second completed payload");
                    Equal(player.Creature.Block, (decimal)(block * 3), "Repeated block");
                    Equal(12 - player.PlayerCombatState.DrawPile.Cards.Count, draws * 3, "Repeated draw");
                    Equal(player.PlayerCombatState.Energy, energy - (id == "U_CARD_025" ? 0 : 1) + gain * 3, "Free repeated payloads");
                    Equal(card.EnergyCost.GetWithModifiers(CostModifiers.None), id == "U_CARD_025" ? 0 : 1, "Printed cost preserved");
                    player.PlayerCombatState.IncrementTurnNumber();
                    var hooks = ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>());
                    await hooks.AfterPlayerTurnStartLate(Context, player);
                    await hooks.AfterPlayerTurnStartLate(Context, player);
                    Equal(ArrowCombat.Find(card)?.Remaining, 4, "Natural tick once next turn");
                    if (id == "U_CARD_021")
                        Equal(player.Creature.CombatState.HittableEnemies.All(e => e.CurrentHp == 100000 - damage * 3), true, "All enemy targets");
                });
            await Case("Replay batch preserves one free renewed deployment", async () =>
            {
                var card = await Add(player, "U_CARD_018"); card.ExtraReplayThisTurn = 1;
                DamageModifiers.Add(card, 2, 1);
                card.EnergyCost.SetThisCombat(3);
                var target = player.Creature.CombatState!.HittableEnemies.Last(); int hp = target.CurrentHp;
                await Play(card);
                Equal(hp - target.CurrentHp, 48, "Two payloads with two hits at twelve");
                Equal(ArrowCombat.Find(card)?.PlayCount, 2, "Replay captured");
                await ArrowCombat.AccelerateAll(Context, player, 1000);
                Equal(hp - target.CurrentHp, 96, "Whole replay batch once");
                Equal(ArrowCombat.Find(card)?.PlayCount, 2, "Replay preserved on renewal");
                Equal(player.PlayerCombatState!.Energy, 17, "Only initial modified cost paid");
                Equal(CardActions.Cost(card), 3, "Modified cost preserved");
            });
            await Case("Externally transformed loop waits before first payload", async () =>
            {
                var card = await Add(player, "U_CARD_018");
                int hp = player.Creature.CombatState!.HittableEnemies.First().CurrentHp;
                await HandArrow.Play(Context, card);
                Equal(player.Creature.CombatState.HittableEnemies.First().CurrentHp, hp, "No immediate effect");
                await ArrowCombat.AccelerateAll(Context, player, 1000);
                Equal(player.Creature.CombatState.HittableEnemies.First().CurrentHp, hp - 10, "Deferred effect once");
                Equal(ArrowCombat.Find(card)?.Remaining, 5, "Loop resumes");
            });
            await Case("Exhaust ends loop and restores original type", async () =>
            {
                await Play(await Add(player, "JA105"));
                var card = await Add(player, "U_CARD_018"); await Play(card);
                Equal(ArrowCombat.Find(card)?.Remaining, 4, "Native arrow-play acceleration");
                await ArrowCombat.AccelerateAll(Context, player, 1000);
                Equal(ArrowCombat.Find(card), null, "No new loop after exhaust");
                Equal(card.Pile?.Type, PileType.Exhaust, "Native exhaust destination");
                Equal(card.Type, CardType.Attack, "Temporary form restored");
            });
            await Case("Arrow follower triggered once per renewed play", async () =>
            {
                var follower = await Add(player, "U_CARD_012");
                var card = await Add(player, "U_CARD_018"); await Play(card);
                Equal(LoopSyncObserver.Plays.Count(p => p.Card == follower), 1, "Initial arrow play once");
                await ArrowCombat.AccelerateAll(Context, player, 1000);
                Equal(LoopSyncObserver.Plays.Count(p => p.Card == follower), 2, "Renewal once");
            });
            await Case("Copied loops remain independent", async () =>
            {
                var card = await Add(player, "U_CARD_018"); await Play(card);
                await ArrowCombat.CopyAll(Context, player);
                var copy = ArrowCombat.Deployed.Single(a => a.Card != card).Card;
                await ArrowCombat.Accelerate(Context, ArrowCombat.Find(copy)!, 5);
                Equal(ArrowCombat.Deployed.Count, 2, "Both actual cards retained");
                Equal(ArrowCombat.Find(card)?.Remaining, 5, "Original untouched");
                Equal(ArrowCombat.Find(copy)?.Remaining, 5, "Copy renewed");
            });
            foreach (bool upgraded in new[] { false, true })
            {
                await Case("JA53 draws two numeric zero-cost cards " + upgraded, async () =>
                {
                    var positive = await Add(player, "JB03", pile: PileType.Draw); positive.EnergyCost.SetThisCombat(2);
                    var x = await Add(player, "JA82", pile: PileType.Draw);
                    var first = await Add(player, "JB01", pile: PileType.Draw); first.EnergyCost.SetThisCombat(0);
                    var second = await Add(player, "JB02", pile: PileType.Draw); second.EnergyCost.SetThisCombat(0);
                    var spare = await Add(player, "JK82B", pile: PileType.Draw);
                    var source = (CatalogCard)await Add(player, "JA53", upgraded);
                    await Play(source);
                    Equal(source.Value("Cards"), 2, "Draw count metadata");
                    Equal(first.Pile?.Type, PileType.Hand, "First actual card");
                    Equal(second.Pile?.Type, PileType.Hand, "Second actual card; hand=" + string.Join(",", player.PlayerCombatState!.Hand.Cards.Select(c => CardCatalog.DesignId(c))) + "; draw=" + string.Join(",", player.PlayerCombatState.DrawPile.Cards.Select(c => CardCatalog.DesignId(c) + ":" + CardActions.Cost(c))));
                    Equal(new[] { positive, x, spare }.All(c => c.Pile?.Type == PileType.Draw), true, "Nonmatches and third match remain");
                    Equal(player.Creature.Block, upgraded ? 18m : 14m, "Block unchanged");
                });
                await Case("JA77 five group hits " + upgraded, async () =>
                {
                    var card = await Add(player, "JA77", upgraded); await Play(card);
                    await ArrowCombat.AccelerateAll(Context, player, 1000);
                    Equal(player.Creature.CombatState!.HittableEnemies.All(e => e.CurrentHp == 100000 - (upgraded ? 7 : 5) * 5), true, "Each enemy receives five hits");
                });
            }
            foreach (bool burning in new[] { false, true })
                await Case("Gem per-card value one " + burning, async () =>
                {
                    var originals = player.Relics.OfType<JunnaGem>().ToArray();
                    var deck = player.Deck.Cards.ToHashSet();
                    foreach (var gem in originals) await RelicCmd.Remove(gem);
                    var active = await RelicCmd.Obtain((burning ? (RelicModel)ModelDb.Relic<JunnaBurningGem>() : ModelDb.Relic<JunnaGreenGem>()).ToMutable(), player);
                    try
                    {
                        await CreatureCmd.SetCurrentHp(player.Creature, 20);
                        int max = player.Creature.MaxHp;
                        var cards = Enumerable.Range(0, 2).Select(_ => player.RunState.CreateCard(CardCatalog.Model("JB01"), player)).ToArray();
                        await CardPileCmd.Add(cards, player.Deck, skipVisuals: true);
                        Equal(burning ? player.Creature.MaxHp - max : player.Creature.CurrentHp - 20, 2, "Batch gain per actual card");
                        await CardPileCmd.RemoveFromDeck(cards, showPreview: false);
                        Equal(burning ? player.Creature.MaxHp - max : player.Creature.CurrentHp - 20, 4, "Batch loss per actual card");
                    }
                    finally
                    {
                        await RelicCmd.Remove(active);
                        await CardPileCmd.RemoveFromDeck(player.Deck.Cards.Where(c => !deck.Contains(c)).ToArray(), showPreview: false);
                        foreach (var gem in originals) await RelicCmd.Obtain(((RelicModel)gem.CanonicalInstance).ToMutable(), player);
                    }
                });
            await Case("Dead initial target transfers the loop to another enemy", async () =>
            {
                var enemies = player.Creature.CombatState!.HittableEnemies.ToArray();
                Equal(enemies.Length >= 2, true, "Two enemies required");
                var target = enemies.Last(); await CreatureCmd.SetCurrentHp(target, 10);
                var card = await Add(player, "U_CARD_018"); await Play(card);
                Equal(target.IsDead, true, "Initial payload defeats selected enemy");
                Equal(ArrowCombat.Find(card)?.Target, enemies.First(), "Native death-order replacement");
                await ArrowCombat.AccelerateAll(Context, player, 1000);
                Equal(enemies.First().CurrentHp, 99990, "Renewal uses replacement");
                Equal(ArrowCombat.Find(card)?.Target, enemies.First(), "Replacement stays on next deployment");
            });
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { revision = CardCatalog.Revision, completed = true, assertions, results }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player, true);
        await CreatureCmd.SetMaxHp(player.Creature, 200); await CreatureCmd.SetCurrentHp(player.Creature, 200);
        foreach (string id in Ids) await Add(player, id, visuals: true);
        await Add(player, "JA71", visuals: true);
        for (int n = 0; n < 12; n++) await Add(player, "JB02", pile: PileType.Draw, visuals: true);
    }
}
