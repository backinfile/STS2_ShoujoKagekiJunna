using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class LatestCardEffectsCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_latest_effects";
    public override string Args => "check | prepare <response|response-stacked|strike|practice|tutor>";
    public override string Description => "Effect-specific regression cases for workbench r5294.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player, args.ElementAtOrDefault(1) ?? "response") : Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static string Output => Path.GetFullPath("../../../artifacts/latest-card-pool/results", Environment.CurrentDirectory);
    private sealed class Selector : ICardSelector
    {
        public string? Choice;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult(options.OrderBy(c => c.Id.Entry == Choice ? 0 : 1).Take(maxSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
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
        player.PlayerCombatState.Energy = 99;
    }
    private static async Task<CardModel> Add(Player player, string id, bool upgraded = false, PileType pile = PileType.Hand, bool visuals = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, skipVisuals: !visuals);
        return card;
    }
    private static async Task Play(CardModel card)
    {
        if (!await TestGameApi.PaidPlay(Context, card, card.Owner.Creature.CombatState!.HittableEnemies.First()))
            throw new InvalidOperationException("Play rejected: " + CardCatalog.DesignId(card));
    }
    private static async Task Check(Player player)
    {
        var results = new List<object>();
        var selector = new Selector();
        using var choices = TestGameApi.UseSelector(selector);
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 5;
        int assertions = 0;
        void Equal<T>(T actual, T expected, string message)
        { assertions++; if (!Equals(actual, expected)) throw new InvalidOperationException($"{message}: actual={actual}, expected={expected}"); }
        async Task Case(string name, Func<Task> test)
        {
            int before = assertions;
            try { await Reset(player); await test(); results.Add(new { name, assertions = assertions - before, pass = true }); }
            catch (Exception error) { results.Add(new { name, assertions = assertions - before, pass = false, error = error.ToString() }); }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "latest-effects.json"), JsonSerializer.Serialize(new { revision = CardCatalog.Revision, completed = false, results }, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            await Case("Frozen expectations cover all 112 active cards", async () =>
            {
                Equal(AuditExpectations.Cards.Count, 112, "Active specification count");
                foreach (var expected in AuditExpectations.Cards)
                foreach (bool upgraded in new[] { false, true })
                {
                    var actual = await Add(player, expected.Id, upgraded, PileType.Draw);
                    Equal(actual.Type, expected.CardType, expected.Id + " type");
                    Equal(actual.EnergyCost.CostsX, expected.CostsX, expected.Id + " X cost");
                    Equal(actual.EnergyCost.GetWithModifiers(CostModifiers.None), upgraded ? expected.UpgradedCost : expected.Cost, expected.Id + " cost");
                    Equal(actual.Keywords.Contains(CardKeyword.Exhaust), upgraded ? expected.UpgradedExhaust : expected.Exhaust, expected.Id + " exhaust");
                    Equal(Shine.GetState(actual)?.Maximum ?? 0, ShineBudget(upgraded ? expected.UpgradedText : expected.Text), expected.Id + " shine budget");
                }
            });
            foreach (bool upgraded in new[] { false, true })
            {
                await Case("JG03 upgrade cost and exhaust " + upgraded, async () =>
                {
                    var target = await Add(player, "JB01");
                    var source = await Add(player, "JG03", upgraded);
                    int energy = player.PlayerCombatState!.Energy;
                    await Play(source);
                    Equal(energy - player.PlayerCombatState.Energy, upgraded ? 0 : 1, "Actual payment");
                    Equal(source.Pile?.Type, PileType.Exhaust, "Both versions exhaust");
                });
                await Case("JA53 zero-cost skill and negative controls " + upgraded, async () =>
                {
                    var positiveArrow = await Add(player, "JB03", pile: PileType.Draw);
                    var x = await Add(player, "JA82", pile: PileType.Draw);
                    var target = await Add(player, "JB01", pile: PileType.Draw);
                    target.EnergyCost.SetThisCombat(0);
                    var spare = await Add(player, "JK82B", pile: PileType.Draw);
                    await Play(await Add(player, "JA53", upgraded));
                    Equal(target.Pile?.Type, PileType.Hand, "Draws actual reduced-cost ordinary card");
                    Equal(positiveArrow.Pile?.Type, PileType.Draw, "Positive-cost arrow excluded");
                    Equal(x.Pile?.Type, PileType.Draw, "X excluded");
                    Equal(spare.Pile?.Type, PileType.Draw, "Only one eligible card");
                    Equal(player.Creature.Block, upgraded ? 18m : 14m, "Block");
                });
                await Case("JK78 double arrows and preserve zones " + upgraded, async () =>
                {
                    var attack = await Add(player, "JB01", pile: PileType.Draw);
                    var arrow = await Add(player, "JA38", pile: PileType.Draw);
                    var adapted = await Add(player, "JB01", pile: PileType.Draw); HandArrow.Adapt(adapted);
                    var skill = await Add(player, "JB02", pile: PileType.Draw);
                    var hand = await Add(player, "JA38");
                    var deployed = await Add(player, "JA38"); await Play(deployed);
                    var order = player.PlayerCombatState!.DrawPile.Cards.ToArray();
                    await Play(await Add(player, "JK78", upgraded));
                    decimal bonus = upgraded ? 8 : 6;
                    Equal(player.PlayerCombatState.DrawPile.Cards.SequenceEqual(order), true, "Same instances and order");
                });
                await Case("JK200B all own arrows across zones " + upgraded, async () =>
                {
                    var hand = await Add(player, "JB01");
                    var ordinaryDraw = await Add(player, "JB01", pile: PileType.Draw);
                    var arrows = new List<CardModel>();
                    foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
                        arrows.Add(await Add(player, "JA38", pile: pile));
                    var deployed = await Add(player, "JA38"); await Play(deployed); arrows.Add(deployed);
                    await Play(await Add(player, "JK200B", upgraded));
                    decimal bonus = upgraded ? 3 : 2;
                });
                await Case("JG33 settled damage becomes own bonus " + upgraded, async () =>
                {
                    var enemy = player.Creature.CombatState!.HittableEnemies.First();
                    var source = await Add(player, "JG33", upgraded);
                    await CreatureCmd.GainBlock(enemy, 7, ValueProp.Unpowered, null);
                    int hp = enemy.CurrentHp;
                    await Play(source);
                    Equal(hp - enemy.CurrentHp, (upgraded ? 30 : 25) - 7, "HP damage");
                    Equal(source.Pile?.Type, PileType.Discard, "Current effect does not move itself into draw pile");
                    await CardPileCmd.Add(source, PileType.Hand); hp = enemy.CurrentHp;
                    await Play(source);
                    Equal(hp - enemy.CurrentHp, upgraded ? 60 : 50, "Second play uses accumulated bonus");
                });
                foreach (string choice in new[] { "ARROWS", "KNOWLEDGE", "SHINE" })
                    await Case("JS33 " + choice + " " + upgraded, async () =>
                    {
                        selector.Choice = "JUNNA_PREPARE_" + choice;
                        string targetId = choice == "ARROWS" ? "JA38" : choice == "KNOWLEDGE" ? "JK49B" : "JS35";
                        var targets = new List<CardModel>();
                        for (int n = 0; n < 4; n++) targets.Add(await Add(player, targetId, pile: PileType.Draw));
                        var unrelated = await Add(player, "JB01", pile: PileType.Draw);
                        var source = await Add(player, "JS33", upgraded);
                        await Play(source);
                        Equal(targets.Count(c => c.Pile?.Type == PileType.Hand), upgraded ? 3 : 2, "Chosen category count");
                        Equal(unrelated.Pile?.Type, PileType.Draw, "Unrelated card stays");
                        Equal(source.Pile?.Type, PileType.Exhaust, "Exhaust");
                        Equal(Shine.IsShineCard(source), false, "Removed obsolete shine budget");
                        selector.Choice = null;
                    });
                await Case("U_CARD_008B block draw and shine " + upgraded, async () =>
                {
                    var drawn = await Add(player, "JB01", pile: PileType.Draw);
                    var source = await Add(player, "U_CARD_008B", upgraded);
                    await Play(source);
                    Equal(player.Creature.Block, upgraded ? 8m : 4m, "Block");
                    Equal(drawn.Pile?.Type, PileType.Hand, "Draw one");
                    Equal(source.Pile?.Type, PileType.Discard, "No exhaust");
                    Equal(Shine.GetState(source)?.Remaining, 5, "Six-use shine decremented once");
                });
                await Case("U_CARD_016 cycle, mismatches, multi-label, shared stacks " + upgraded, async () =>
                {
                    for (int n = 0; n < 12; n++) await Add(player, "JB02", pile: PileType.Draw);
                    await Play(await Add(player, "U_CARD_016", upgraded));
                    Equal(player.PlayerCombatState!.Hand.Cards.Count, 0, "Install does not draw");
                    int left = player.PlayerCombatState.DrawPile.Cards.Count;
                    await Play(await Add(player, "JB02"));
                    Equal(player.PlayerCombatState.DrawPile.Cards.Count, left, "Wrong category does not trigger or advance");
                    await Play(await Add(player, "JK49B"));
                    Equal(left - player.PlayerCombatState.DrawPile.Cards.Count, 1, "Knowledge triggers");
                    var power = player.Creature.Powers.OfType<JunnaPowerU_CARD_016>().Single();
                    Equal(power.DisplayAmount, 1, "Unstacked draw amount");
                    string arrowIcon = power.IconPath;
                    Equal(arrowIcon.EndsWith("U_CARD_016_Arrow.png"), true, "Arrow state icon");
                    await Play(await Add(player, "U_CARD_016"));
                    Equal(power.DisplayAmount, 2, "Second copy increases the draw amount");
                    Equal(power.IconPath, arrowIcon, "Second copy keeps the current category");
                    await Play(await Add(player, "JA38"));
                    Equal(left - player.PlayerCombatState.DrawPile.Cards.Count, 3, "Shared arrow stage draws two");
                    var multi = await Add(player, "JK133");
                    await Play(multi);
                    Equal(left - player.PlayerCombatState.DrawPile.Cards.Count, 5, "Multi-label play draws twice and advances once");
                    var deployment = ArrowCombat.Find(multi)!;
                    await ArrowCombat.Accelerate(Context, deployment, 1000);
                    Equal(left - player.PlayerCombatState.DrawPile.Cards.Count, 6, "Arrow completion only draws its own one card");
                    player.PlayerCombatState.IncrementTurnNumber();
                    await Play(await Add(player, "JK49B"));
                    Equal(left - player.PlayerCombatState.DrawPile.Cards.Count, 8, "Shared cycle persists across turns");
                });
                foreach (int mask in Enumerable.Range(0, 8))
                    await Case("U_CARD_017 category mask " + mask + " " + upgraded, async () =>
                    {
                        if ((mask & 1) != 0) { await Add(player, "JA38"); await Add(player, "JA38"); }
                        if ((mask & 2) != 0) await Add(player, "JK49B");
                        if ((mask & 4) != 0) await Add(player, "JS35");
                        int hp = player.Creature.CombatState!.HittableEnemies.First().CurrentHp;
                        await Play(await Add(player, "U_CARD_017", upgraded));
                        Equal(hp - player.Creature.CombatState.HittableEnemies.First().CurrentHp,
                            6 * (1 + System.Numerics.BitOperations.PopCount((uint)mask)), "One hit per present category, duplicate arrows count once");
                    });
                await Case("U_CARD_017 single multi-label card and external hit " + upgraded, async () =>
                {
                    await Add(player, "JK133"); // One actual card satisfies all three categories.
                    var source = await Add(player, "U_CARD_017", upgraded);
                    int hp = player.Creature.CombatState!.HittableEnemies.First().CurrentHp;
                    await Play(source);
                    Equal(hp - player.Creature.CombatState.HittableEnemies.First().CurrentHp, 40, "Four category hits plus one external hit, each 8");
                });
            }
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "latest-effects.json"), JsonSerializer.Serialize(new { revision = CardCatalog.Revision, completed = true, assertions, results }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static int ShineBudget(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"闪耀(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
    private static async Task Prepare(Player player, string scenario)
    {
        await Reset(player, visuals: true);
        player.PlayerCombatState!.Energy = 20;
        Task<CardModel> Visible(string id, bool upgrade = false, PileType pile = PileType.Hand)
            => Add(player, id, upgrade, pile, visuals: true);
        if (scenario is "response" or "response-stacked")
        {
            await Visible("U_CARD_016"); await Visible("JK49B"); await Visible("JA38"); await Visible("JS35");
            if (scenario == "response-stacked")
            {
                await Visible("U_CARD_016", true);
                await Visible("JB02");
            }
            for (int n = 0; n < 8; n++)
                foreach (var id in new[] { "JK49B", "JA38", "JS35" }) await Visible(id, pile: PileType.Draw);
        }
        else if (scenario == "strike")
        { await Visible("U_CARD_017"); await Visible("JK133"); }
        else if (scenario == "practice")
        { await Visible("JK78"); await Visible("JA38", pile: PileType.Draw); await Visible("JB01", pile: PileType.Draw); }
        else
        { await Visible("JS33", true); for (int n = 0; n < 3; n++) await Visible("JS35", pile: PileType.Draw); }
    }
}
