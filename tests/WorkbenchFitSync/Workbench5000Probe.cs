using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class Workbench5000Command : AbstractConsoleCmd
{
    public override string CmdName => "junna_r5000";
    public override string Args => "check | prepare";
    public override string Description => "Checks the latest card pool delta.";
    public override bool IsNetworked => false;
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly string ResultPath = Path.GetFullPath("../../../out/tmp/workbench-r5000/results.json", Environment.CurrentDirectory);
    private static readonly List<(CardModel Card, CardPilePosition Position)> Generated = [];
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static void Capture(CardModel card, PileType newPileType, CardPilePosition position)
    { if (newPileType == PileType.Draw) Generated.Add((card, position)); }
    private static async Task Reset(Player player, bool visualize = false)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: !visualize);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 10000); await CreatureCmd.SetCurrentHp(enemy, 10000);
            await CreatureCmd.LoseBlock(Context, enemy, enemy.Block, null);
        }
        await CreatureCmd.SetMaxHp(player.Creature, 10000); await CreatureCmd.SetCurrentHp(player.Creature, 10000);
        await CreatureCmd.LoseBlock(Context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState.Energy = 99;
        Generated.Clear();
    }
    private static async Task<CardModel> Card(Player player, string id, bool upgraded = false, PileType pile = PileType.Hand, bool visualize = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, skipVisuals: !visualize);
        return card;
    }
    private static Task<bool> Play(CardModel card, Creature? target = null)
        => CardActions.PaidPlay(Context, card, target ?? card.Owner.Creature.CombatState!.Enemies.First());
    private static void Expect(bool pass, string message)
    { if (!pass) throw new InvalidOperationException(message); }
    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        const string patchId = "JunnaWorkbenchFitSync.R5000";
        var patch = new Harmony(patchId);
        patch.Patch(AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat)),
            prefix: new HarmonyMethod(typeof(Workbench5000Command), nameof(Capture)));
        async Task Case(string name, Func<Task> run)
        {
            try { await Reset(player); await run(); rows.Add(new { name, pass = true }); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
            File.WriteAllText(ResultPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        double speed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 5;
        try
        {
            foreach (bool upgraded in new[] { false, true })
            {
                await Case("whole draw pile training " + upgraded, async () =>
                {
                    var attacks = new List<CardModel>();
                    for (int i = 0; i < 5; i++) attacks.Add(await Card(player, "JB01", pile: PileType.Draw));
                    var defense = await Card(player, "JB02", pile: PileType.Draw);
                    var original = player.PlayerCombatState!.DrawPile.Cards.ToArray();
                    var hand = await Card(player, "JB01");
                    var source = await Card(player, "JK78", upgraded);
                    await Play(source);
                    Expect(attacks.All(c => DamageModifiers.CaptureDamageModifierSnapshot(c).DamageBonus == (upgraded ? 8 : 6)), "All five damage cards receive bonus");
                    Expect(DamageModifiers.CaptureDamageModifierSnapshot(defense).DamageBonus == 0 && DamageModifiers.CaptureDamageModifierSnapshot(hand).DamageBonus == 0, "Eligibility and pile scope");
                    Expect(original.SequenceEqual(player.PlayerCombatState.DrawPile.Cards), "Original draw order retained");
                });
                await Case("new delayed area arrow " + upgraded, async () =>
                {
                    var enemies = player.Creature.CombatState!.Enemies.ToArray();
                    var source = await Card(player, "U_CARD_011", upgraded);
                    await Play(source);
                    Expect(enemies.All(e => e.CurrentHp == 10000) && ArrowCombat.Find(source)?.Remaining == 2, "Waits two turns");
                    await ArrowCombat.AccelerateAll(Context, player, 99);
                    Expect(enemies.All(e => e.CurrentHp == 10000 - (upgraded ? 16 : 12)), "Hits every enemy once");
                    Expect(source.Pile?.Type == PileType.Discard, "Discarded after resolution");
                });
                foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
                    await Case("following arrow " + pile + " " + upgraded, async () =>
                    {
                        var foe = player.Creature.CombatState!.Enemies.Last();
                        var follower = await Card(player, "U_CARD_012", upgraded, pile);
                        await Play(await Card(player, "JA38"), foe);
                        Expect(ArrowCombat.Find(follower) is { Remaining: 1 } deployed && deployed.Target == foe,
                            "Same physical card deploys from original zone to triggering target");
                        Expect(foe.CurrentHp == 10000, "No early damage");
                        await ArrowCombat.AccelerateAll(Context, player, 99);
                        Expect(foe.CurrentHp == 10000 - 18 - (upgraded ? 20 : 16), "Delayed damage reaches enemy");
                        Expect(follower.Pile?.Type == PileType.Discard, "Follower discarded once");
                    });
                await Case("following arrows avoid recursion and redeploy " + upgraded, async () =>
                {
                    var first = await Card(player, "U_CARD_012", upgraded);
                    var second = await Card(player, "U_CARD_012", upgraded, PileType.Exhaust);
                    await Play(await Card(player, "JA15"));
                    Expect(ArrowCombat.Deployed.Count == 3 && ArrowCombat.Find(first)?.Target?.IsEnemy == true
                        && ArrowCombat.Find(second)?.Target?.IsEnemy == true, "Self-target trigger automatically selects enemies once per follower");
                    await Play(await Card(player, "JA38"));
                    Expect(ArrowCombat.Deployed.Count == 4 && ArrowCombat.Deployed.Count(a => a.Card == first) == 1
                        && ArrowCombat.Find(first)?.Remaining == 1, "Already deployed follower restarts without clone or recursive cascade");
                });
                await Case("cage reduction and expiry " + upgraded, async () =>
                {
                    var enemies = player.Creature.CombatState!.Enemies.ToArray();
                    await Play(await Card(player, "U_CARD_011"));
                    var cage = await Card(player, "U_CARD_013", upgraded);
                    Expect(cage.EnergyCost.GetWithModifiers(CostModifiers.All) == (upgraded ? 0 : 1), "Correct cost");
                    await Play(cage); await Play(await Card(player, "U_CARD_013"));
                    Expect(enemies.All(e => e.Powers.Count(p => p is JunnaCagedPower) == 1), "Each target receives one nonstacking reduction");
                    var foe = enemies[0]; var power = foe.GetPower<JunnaCagedPower>()!;
                    int hp = player.Creature.CurrentHp;
                    await CreatureCmd.Damage(Context, [player.Creature], 20, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, foe, null, null);
                    Expect(player.Creature.CurrentHp == hp - 10, "Actual outgoing damage halved once");
                    await power.AfterSideTurnEnd(Context, CombatSide.Player, [player.Creature]);
                    Expect(foe.HasPower<JunnaCagedPower>(), "Persists through player turn end");
                    await power.AfterSideTurnEnd(Context, CombatSide.Enemy, enemies);
                    Expect(!foe.HasPower<JunnaCagedPower>(), "Expires after enemy response");
                });
                await Case("research copy position and limit " + upgraded, async () =>
                {
                    await Play(await Card(player, "JK15", upgraded));
                    var source = await Card(player, "JK49B", true);
                    Generated.Clear(); await Play(source);
                    Expect(Generated.Count == 1 && Generated[0].Position == CardPilePosition.Random
                        && Generated[0].Card != source && Generated[0].Card.Id == source.Id && Generated[0].Card.IsUpgraded,
                        "One upgraded clone uses native random placement");
                    await Play(await Card(player, "JK49B"));
                    Expect(Generated.Count == 1, "Second knowledge play does not copy again this turn");
                });
                await Case("updated formation cost and exhaust " + upgraded, async () =>
                {
                    var source = await Card(player, "JK84", upgraded);
                    Expect(source.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "Zero cost");
                    await Play(source);
                    Expect(source.Pile?.Type == (upgraded ? PileType.Discard : PileType.Exhaust), "Base exhaust and upgrade destination");
                });
                await Case("updated turn start acceleration " + upgraded, async () =>
                {
                    var arrow = await Card(player, "JB03"); await Play(arrow);
                    int before = ArrowCombat.Find(arrow)!.Remaining;
                    await Play(await Card(player, "JA35", upgraded));
                    await player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA35").AfterPlayerTurnStart(Context, player);
                    int remaining = Math.Max(0, before - (upgraded ? 3 : 2));
                    Expect(remaining == 0 ? ArrowCombat.Find(arrow) == null : ArrowCombat.Find(arrow)?.Remaining == remaining, "Uses new two/three haste");
                });
            }
            await Case("cage with no deployed arrows", async () =>
            {
                await Play(await Card(player, "U_CARD_013"));
                Expect(player.Creature.CombatState!.Enemies.All(e => !e.HasPower<JunnaCagedPower>()), "No unrelated enemy is affected");
            });
        }
        finally
        {
            patch.UnpatchAll(patchId); Godot.Engine.TimeScale = speed;
            File.WriteAllText(Path.ChangeExtension(ResultPath, ".completed"), "completed");
        }
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player, visualize: true); player.PlayerCombatState!.Energy = 9;
        foreach (var id in new[] { "U_CARD_012", "U_CARD_011", "U_CARD_013", "JK78", "JA77", "JA35" }) await Card(player, id, visualize: true);
        for (int i = 0; i < 5; i++) await Card(player, "JB01", pile: PileType.Draw);
    }
}
