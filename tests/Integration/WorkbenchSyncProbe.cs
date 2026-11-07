using System.Text.Encodings.Web;
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
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class WorkbenchSyncCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_sync";
    public override string Args => "run | prepare";
    public override string Description => "Targeted workbench synchronization checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() switch { "energy-update" => Workbench3650Probe.RunEnergyUpdate(player), "prepare-energy" => Workbench3650Probe.PrepareEnergy(player), "r3650" => Workbench3650Probe.Run(player), "prepare3650" => Workbench3650Probe.Prepare(player), "r3366" => Workbench3366Probe.Run(player, string.Join(" ", args.Skip(1))), "prepare3366" => Workbench3366Probe.Prepare(player), "prepare" => WorkbenchSyncProbe.Prepare(player), _ => WorkbenchSyncProbe.Run(player) }, true, "Sync checks started");
}

public static class WorkbenchSyncProbe
{
    private sealed class Selector : ICardSelector
    {
        public int? Count;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult(options.Take(Count ?? maxSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private sealed record Result(string Name, bool Pass, string? Error);
    private static void Expect(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static async Task Reset(Player player)
    {
        foreach (var p in player.Creature.Powers.ToArray()) await PowerCmd.Remove(p);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            foreach (var p in enemy.Powers.ToArray()) await PowerCmd.Remove(p);
            await CreatureCmd.SetMaxHp(enemy, 1000000); await CreatureCmd.SetCurrentHp(enemy, 1000000);
            await CreatureCmd.LoseBlock(Context, enemy, enemy.Block, null);
        }
        await CreatureCmd.SetMaxHp(player.Creature, 100000); await CreatureCmd.SetCurrentHp(player.Creature, 100000);
        await CreatureCmd.LoseBlock(Context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState!.Energy = 30;
    }
    private static async Task<CardModel> Card(Player player, string id, PileType pile = PileType.Hand, bool up = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (up) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, skipVisuals: true);
        return card;
    }
    private static Task<bool> Play(CardModel card) => CardActions.PaidPlay(Context, card, card.CombatState!.Enemies.First());
    private static Task Finish(CardModel card) => ArrowCombat.Find(card) is { } a ? ArrowCombat.Accelerate(Context, a, int.MaxValue) : Task.CompletedTask;
    private static decimal Bonus(CardModel card) => DamageModifiers.CaptureDamageModifierSnapshot(card).DamageBonus;

    public static async Task Run(Player player)
    {
        var results = new List<Result>(); var selector = new Selector();
        var enemy = player.Creature.CombatState!.Enemies.First();
        double speed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 25;
        using var choices = CardSelectCmd.UseSelector(selector);
        async Task Case(string name, Func<Task> test)
        {
            try { selector.Count = null; await Reset(player); await test(); results.Add(new(name, true, null)); }
            catch (Exception e) { results.Add(new(name, false, e.ToString())); }
        }
        try
        {
            await Case("catalog, models, upgrades and starter deck", async () =>
            {
                Expect(CardCatalog.Revision == 3366 && CardCatalog.Cards.Count == 115 && CardCatalog.Groups.Count == 23, "Catalog counts");
                foreach (var d in CardCatalog.Cards)
                {
                    var c = await Card(player, d.Id, PileType.Discard);
                    Expect(c.Title == d.Name && c.TargetType == d.TargetType && c.Type == d.CardType, d.Id + " base metadata");
                    c.UpgradeInternal(); c.FinalizeUpgradeInternal();
                    Expect(c.EnergyCost.CostsX || CardActions.Cost(c) == d.UpgradedCost, d.Id + " upgraded cost");
                    foreach (var v in d.Values.Where(v => c.DynamicVars.ContainsKey(v.Name)))
                        Expect(c.DynamicVars[v.Name].BaseValue == v.Upgraded, d.Id + "/" + v.Name + " upgrade");
                }
                var deck = CardCatalog.StartingDeck.ToArray();
                Expect(deck.Length == 10 && deck.Count(c => CardCatalog.DesignId(c) == "JB01") == 4 && deck.Count(c => CardCatalog.DesignId(c) == "JH26") == 1, "Starter deck");
            });
            foreach (bool up in new[] { false, true })
            {
                await Case("举弓: actual card, free deployment, front enemy, delayed damage " + up, async () =>
                {
                    var source = await Card(player, "JA84", up: up); var material = await Card(player, "JH10");
                    DamageModifiers.Add(material, 2); int hp = enemy.CurrentHp; player.PlayerCombatState!.Energy = 1;
                    await Play(source); var arrow = ArrowCombat.Find(material);
                    Expect(arrow?.Remaining == 5 && arrow.Play.Target == enemy && arrow.Play.Resources.EnergyValue == 3 && arrow.Play.Resources.EnergySpent == 0, "Deployment/target/payment");
                    Expect(CardCatalog.IsArrow(material) && enemy.CurrentHp == hp && player.PlayerCombatState.Energy == 0 && player.Creature.Block == (up ? 11 : 7), "Initial state");
                    await Finish(material);
                    Expect(material.Type == CardType.Attack && material.Pile?.Type == PileType.Discard && hp - enemy.CurrentHp == 33 && Bonus(material) == 2, "Completion and restoration");
                });
                await Case("从容应对: self only " + up, async () =>
                {
                    var own = await Card(player, "JH11"); var attack = await Card(player, "JB03"); await Play(own); await Play(attack);
                    await Play(await Card(player, "JA94", up: up));
                    Expect(ArrowCombat.Find(own) == null && ArrowCombat.Find(attack)?.Remaining == 3, "Selective acceleration");
                    Expect(player.Creature.Block == (up ? 13 : 10), "Block and completed 微光");
                });
                await Case("预习: card count and exhaust " + up, async () =>
                {
                    var source = await Card(player, "JK84", up: up); await Card(player, "JB01"); await Card(player, "JB02");
                    await Play(source); Expect(ArrowCombat.Deployed.Count == (up ? 2 : 1) && source.Pile?.Type == PileType.Exhaust, "Material count");
                });
                await Case("连斩 and 学以致用 " + up, async () =>
                {
                    var power = await Card(player, "JK75", up: up); var slash = await Card(player, "JK75B", up: up); var receiver = await Card(player, "JB01");
                    await Play(power); Expect(Bonus(receiver) == 0 && Bonus(slash) == 0 && CardCatalog.IsKnowledge(power), "No self trigger");
                    int hp = enemy.CurrentHp; await Play(slash);
                    Expect(hp - enemy.CurrentHp == (up ? 18 : 12) && Bonus(receiver) == (up ? 2 : 1), "Knowledge payoff");
                    Expect(KnowledgeCombat.Group(power)?.GrantedCardIds.Single() == "JK75B", "New knowledge group");
                });
                await Case("稳住阵脚: each attack damage event " + up, async () =>
                {
                    var arrow = await Card(player, "JB03"); await Play(arrow); await Play(await Card(player, "JA96", up: up));
                    await CreatureCmd.Damage(Context, [player.Creature], 1, ValueProp.Move, enemy, null, null);
                    Expect(ArrowCombat.Find(arrow)?.Remaining == (up ? 1 : 2), "First damage event");
                    await CreatureCmd.Damage(Context, [player.Creature], 1, ValueProp.Move, enemy, null, null);
                    Expect(up ? ArrowCombat.Find(arrow) == null : ArrowCombat.Find(arrow)?.Remaining == 1, "Second damage event");
                });
            }
            await Case("temporary ability installs only at completion", async () =>
            {
                var power = await Card(player, "JK75"); await HandArrow.Play(Context, power);
                Expect(!CatalogPower.Has(player, "JK75") && ArrowCombat.Find(power)?.Remaining == 5, "Early ability installation");
                await Finish(power); Expect(CatalogPower.Has(player, "JK75") && power.Type == CardType.Power && power.Pile == null, "Delayed ability destination");
            });
            await Case("material X captures zero", async () =>
            {
                var rain = await Card(player, "JA82"); player.PlayerCombatState!.Energy = 8;
                await HandArrow.Play(Context, rain); await Finish(rain);
                Expect(player.PlayerCombatState.Energy == 8 && !ArrowCombat.Deployed.Any() && rain.EnergyCost.CapturedXValue == 0, "X=0");
            });
            await Case("material exhaust happens only after completion", async () =>
            {
                var card = await Card(player, "JF10"); await HandArrow.Play(Context, card);
                Expect(card.Pile?.Type == ArrowPile.PileId, "Premature exhaust"); await Finish(card);
                Expect(card.Pile?.Type == PileType.Exhaust && card.Type == CardType.Skill, "Completion exhaust");
            });
            await Case("迎击架势: arbitrary batch and decline", async () =>
            {
                await Card(player, "JB01"); await Card(player, "JB02"); var source = await Card(player, "JA88");
                await Play(source); Expect(ArrowCombat.Deployed.Count == 2 && source.Pile?.Type == PileType.Exhaust, "Batch deployment");
            });
            await Case("布阵 chooses before hand flush and can decline", async () =>
            {
                await Play(await Card(player, "JA87")); var material = await Card(player, "JB01");
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA87");
                selector.Count = 0; await power.BeforeFlush(Context, player); Expect(material.Pile?.Type == PileType.Hand, "Decline");
                selector.Count = 1; await power.BeforeFlush(Context, player); Expect(ArrowCombat.Find(material)?.Remaining == 5, "End turn deployment");
            });
            await Case("搭箭 draws then deploys and keeps normal destination", async () =>
            {
                var material = await Card(player, "JB01", PileType.Draw); var source = await Card(player, "JG37");
                await Play(source); Expect(ArrowCombat.Find(material)?.Remaining == 5 && source.Pile?.Type == PileType.Discard, "Draw then choose");
            });
            await Case("改编 persists zero-cost five-turn form", async () =>
            {
                var source = await Card(player, "JT02"); var material = await Card(player, "JH10"); await Play(source);
                Expect(CardActions.Cost(material) == 0 && CardCatalog.IsArrow(material), "Adapted form");
                await Play(material); Expect(ArrowCombat.Find(material)?.Remaining == 5, "Adapted countdown"); await Finish(material);
                Expect(CardCatalog.IsArrow(material) && CardActions.Cost(material) == 0 && material.Pile?.Type == PileType.Discard, "Persistent adaptation");
            });
            await Case("next-card bonus applies before damage once", async () =>
            {
                await Play(await Card(player, "JG09")); var first = await Card(player, "JB01"); var second = await Card(player, "JB01");
                int hp = enemy.CurrentHp; await Play(first); await Play(second);
                Expect(hp - enemy.CurrentHp == 14 && Bonus(first) == 2 && Bonus(second) == 0, "Next card bonus");
            });
            await Case("挥刃练习 prepares the next card", async () =>
            {
                var first = await Card(player, "JG36"); int hp = enemy.CurrentHp; await Play(first); var next = await Card(player, "JB01"); await Play(next);
                Expect(hp - enemy.CurrentHp == 18 && Bonus(next) == 2, "Attack then prepare");
            });
            await Case("双星 pays again and retains one deployment", async () =>
            {
                await Play(await Card(player, "JH19")); var arrow = await Card(player, "JB03"); player.PlayerCombatState!.Energy = 2;
                await Play(arrow); Expect(player.PlayerCombatState.Energy == 0 && ArrowCombat.Deployed.Count == 1 && ArrowCombat.Find(arrow)?.PlayCount == 2, "Additional payment");
                int hp = enemy.CurrentHp; await Finish(arrow); Expect(hp - enemy.CurrentHp == 50 && arrow.Pile?.Type == PileType.Discard, "Both payloads resolve");
            });
            await Case("双星 with insufficient extra energy", async () =>
            {
                await Play(await Card(player, "JH19")); var strike = await Card(player, "JB01"); player.PlayerCombatState!.Energy = 1;
                int hp = enemy.CurrentHp; await Play(strike); Expect(hp - enemy.CurrentHp == 6 && !CatalogPower.Has(player, "JH19"), "Unaffordable replay");
            });
            await Case("牵制箭 applies both debuffs to all enemies", async () =>
            {
                var arrow = await Card(player, "JA58"); await Play(arrow); await Finish(arrow);
                Expect(player.Creature.CombatState!.Enemies.All(e => e.GetPower<VulnerablePower>()?.Amount == 1 && e.GetPower<WeakPower>()?.Amount == 1), "All enemy debuffs");
            });
            await Case("反复推敲 only improves deployed arrows", async () =>
            {
                var deployed = await Card(player, "JB03"); var waiting = await Card(player, "JB03"); await Play(deployed); await Play(await Card(player, "JK67"));
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JK67");
                await power.BeforeSideTurnEnd(Context, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [player.Creature]);
                Expect(Bonus(deployed) == 2 && Bonus(waiting) == 0, "Deployed scope");
            });
            await Case("缓步起行 improves only the retrieved card", async () =>
            {
                var drawn = await Card(player, "JK43", PileType.Draw); var other = await Card(player, "JK43", PileType.Discard);
                int originalCost = CardActions.Cost(drawn);
                await Play(await Card(player, "JK43B")); Expect(Bonus(drawn) == 3 && Bonus(other) == 0 && CardActions.Cost(drawn) == originalCost, "Retrieved instance scope");
            });
            await Case("论据+ plays all actual hand copies for free", async () =>
            {
                var a = await Card(player, "JK50"); var b = await Card(player, "JK50"); var source = await Card(player, "JK50B", up: true);
                player.PlayerCombatState!.Energy = 2; await Play(source);
                Expect(a.Pile?.Type == PileType.Discard && b.Pile?.Type == PileType.Discard && player.PlayerCombatState.Energy == 0, "Free full batch");
            });
            await Case("筹备 is a lasting high-cost trigger", async () =>
            {
                await Play(await Card(player, "JH31")); var zero = await Card(player, "JH11", PileType.Draw); var material = await Card(player, "JH10");
                await HandArrow.Play(Context, material); Expect(zero.Pile?.Type == PileType.Hand && CatalogPower.Has(player, "JH31"), "Free play retains high-cost attribute");
            });
            await Case("观摩 first-play limit and upgraded energy", async () =>
            {
                await Play(await Card(player, "JK10", up: true)); await Card(player, "JB01", PileType.Draw); player.PlayerCombatState!.Energy = 0;
                await HandArrow.Play(Context, await Card(player, "JH10")); Expect(player.PlayerCombatState.Energy == 2, "First high cost reward");
                await HandArrow.Play(Context, await Card(player, "JH10")); Expect(player.PlayerCombatState.Energy == 2, "Once per turn");
            });
            await Case("挺身反抗 and 默然忍受 retain their current forms", async () =>
            {
                var a = await Card(player, "JK49"); var b = await Card(player, "JK49B"); await Play(a); await Play(b);
                Expect(CardCatalog.DesignId(a) == "JK49" && CardCatalog.DesignId(b) == "JK49B", "Removed transformation");
            });
            await Case("连星 includes two base hits", async () =>
            {
                var card = await Card(player, "JH13"); player.PlayerCombatState!.Energy = 3;
                int hp = enemy.CurrentHp; await Play(card); Expect(hp - enemy.CurrentHp == 18, "Two base hits plus one remaining energy");
            });
            await Case("追击 counts only arrow plays", async () =>
            {
                var pursuit = await Card(player, "JT03"); await Play(pursuit); await Play(await Card(player, "JB01"));
                Expect(ArrowCombat.Find(pursuit)?.AttacksWhileWaiting == 0, "Attack is outside scope");
                await HandArrow.Play(Context, await Card(player, "JB02"));
                Expect(ArrowCombat.Find(pursuit)?.AttacksWhileWaiting == 1, "Material arrow counts once");
            });
            await Case("伏击箭 keeps its one-turn delay", async () =>
            {
                var selected = await Card(player, "JB03", PileType.Draw); var source = await Card(player, "JA65");
                await Play(source); Expect(ArrowCombat.Find(source)?.Remaining == 1 && selected.Pile?.Type == PileType.Draw, "Delay before selection");
                await Finish(source); Expect(ArrowCombat.Find(selected)?.Remaining == 3, "Selected arrow plays on completion");
            });
            await Case("论辩 attacks then accelerates all deployed arrows", async () =>
            {
                var a = await Card(player, "JB03"); var b = await Card(player, "JH11"); await Play(a); await Play(b);
                int hp = enemy.CurrentHp; await Play(await Card(player, "JK84B"));
                Expect(hp - enemy.CurrentHp == 12 && ArrowCombat.Find(a)?.Remaining == 2 && ArrowCombat.Find(b) == null, "Attack and acceleration");
            });
            await Case("adapted material keeps its persistent form after temporary deployment", async () =>
            {
                var c = await Card(player, "JB01"); HandArrow.Adapt(c); await HandArrow.Play(Context, c); await Finish(c);
                Expect(CardCatalog.IsArrow(c) && CardActions.Cost(c) == 0, "Previous adaptation survives");
            });
            await Case("external skill retains its complete original effect", async () =>
            {
                var c = player.Creature.CombatState!.CreateCard(ModelDb.Card<ExternalDirectCard>(), player);
                await CardPileCmd.Add(c, PileType.Hand, skipVisuals: true); await HandArrow.Play(Context, c);
                int hp = enemy.CurrentHp; await Finish(c);
                Expect(hp - enemy.CurrentHp == 4 && player.Creature.Block == 7 && c.Type == CardType.Skill, "External delayed payload");
            });
            await Case("双星 expires at the end of the turn", async () =>
            {
                await Play(await Card(player, "JH19")); var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JH19");
                await power.AfterSideTurnEnd(Context, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [player.Creature]);
                Expect(!CatalogPower.Has(player, "JH19"), "Turn expiration");
            });
            await Case("改编 makes an X card numeric zero and keeps X payload zero", async () =>
            {
                var rain = await Card(player, "JA82"); HandArrow.Adapt(rain); player.PlayerCombatState!.Energy = 5;
                await Play(rain); await Finish(rain);
                Expect(!rain.EnergyCost.CostsX && CardActions.Cost(rain) == 0 && player.PlayerCombatState.Energy == 5 && !ArrowCombat.Deployed.Any(), "Fixed zero X payload");
            });
        }
        finally { Godot.Engine.TimeScale = speed; }
        string output = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "workbench-sync-results.json"));
        File.WriteAllText(output, JsonSerializer.Serialize(new { completed = true, revision = CardCatalog.Revision, passed = results.Count(r => r.Pass), total = results.Count, results },
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
    public static async Task Prepare(Player player)
    {
        await Reset(player); Godot.Engine.TimeScale = 1; player.PlayerCombatState!.Energy = 10;
        await CreatureCmd.SetMaxHp(player.Creature, 70); await CreatureCmd.SetCurrentHp(player.Creature, 70);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(enemy, 200); await CreatureCmd.SetCurrentHp(enemy, 200); }
        using var single = KnowledgeCombat.SingleCard();
        foreach (var id in new[] { "JA84", "JH10", "JA88", "JK75", "JK75B", "JA94", "JK84B", "JA71" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Hand, player);
        foreach (var id in new[] { "JB01", "JB02" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Draw, player);
    }
}
