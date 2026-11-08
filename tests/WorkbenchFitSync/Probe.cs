using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

[ModInitializer(nameof(Initialize))]
public static class ProbeStartup
{
    public static void Initialize()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        Godot.RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), Godot.RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => Godot.RenderingServer.ForceDraw(false);
    }
}

public sealed class WorkbenchFitCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_fit_sync";
    public override string Args => "check | energy | prepare | prepare-energy";
    public override string Description => "Targeted workbench synchronization checks.";
    public override bool IsNetworked => false;
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "energy" ? CheckEnergy(player) : args.FirstOrDefault() == "prepare-energy" ? PrepareEnergy(player) : args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static async Task Reset(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
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
        await CardPileCmd.AddGeneratedCardToCombat(card, pile, player);
        return card;
    }
    private static Task<bool> Play(CardModel card, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target = null)
        => CardActions.PaidPlay(Context, card, target ?? card.Owner.Creature.CombatState!.Enemies.First());
    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        var state = player.PlayerCombatState!;
        var turn = AccessTools.Field(typeof(PlayerCombatState), "<TurnNumber>k__BackingField");
        int originalTurn = state.TurnNumber;
        try
        {
            foreach (var id in new[] { "JF13", "JA119", "JK184", "JK184B", "JK184C", "JK184D", "JA144", "JA145", "JS34", "JS35" })
                foreach (bool upgraded in new[] { false, true })
                {
                    await Reset(player);
                    var card = await Card(player, id, upgraded);
                    Expect(id + " description " + upgraded, !card.GetDescriptionForPile(PileType.Hand).Contains("ERROR"));
                    if (id == "JF13") Expect("Innate " + upgraded, card.Keywords.Contains(CardKeyword.Innate));
                }
            foreach (var (id, amount, draw) in new[] { ("JF13", 5, 1), ("JK184C", 8, 0), ("JS35", 10, 0), ("JK184D", 0, 2), ("JS34", 0, 4) })
            {
                await Reset(player);
                for (int i = 0; i < 6; i++) await Card(player, "JB01", pile: PileType.Draw);
                await Play(await Card(player, id));
                Expect(id + " block", player.Creature.Block == amount);
                Expect(id + " draw", state.Hand.Cards.Count == draw);
                Expect(id + " exhaust", state.ExhaustPile.Cards.Any(c => CardCatalog.DesignId(c) == id));
            }
            foreach (bool upgraded in new[] { false, true })
            {
                await Reset(player);
                await Play(await Card(player, "JA145", upgraded));
                var generated = state.Hand.Cards.Where(c => CardCatalog.DesignId(c) == "JA119").ToArray();
                Expect("aimed shot generates " + upgraded, generated.Length == (upgraded ? 2 : 1) && generated.All(c => !c.IsUpgraded));
            }
            await Reset(player);
            var foe = player.Creature.CombatState!.Enemies.First();
            var attack = await Card(player, "JA38"); await Play(attack);
            var defense = await Card(player, "JA15"); await Play(defense);
            var attackArrow = ArrowCombat.Find(attack)!; var selfArrow = ArrowCombat.Find(defense)!;
            int beforeEnemy = attackArrow.Remaining, beforeSelf = selfArrow.Remaining;
            var token = await Card(player, "JA119");
            Expect("token accepts self", token.IsValidTarget(player.Creature));
            Expect("token accepts enemy", token.IsValidTarget(foe));
            Expect("token requires target", !token.IsValidTarget(null));
            await Play(token, player.Creature);
            Expect("self arrow accelerated", selfArrow.Remaining == beforeSelf - 1);
            Expect("other target unchanged", attackArrow.Remaining == beforeEnemy);
            await Play(await Card(player, "JA119"), foe);
            Expect("enemy arrow accelerated", attackArrow.Remaining == beforeEnemy - 1);
            await Reset(player);
            await Play(await Card(player, "JA144")); await Play(await Card(player, "JA144", true));
            var installed = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JA144");
            await installed.AfterPlayerTurnStart(Context, player);
            Expect("stacked steady progress", state.Hand.Cards.Count(c => CardCatalog.DesignId(c) == "JA119") == 2);
            foreach (bool upgraded in new[] { false, true })
            {
                await Reset(player);
                JunnaGem gem = upgraded ? (JunnaGem)ModelDb.Relic<JunnaBurningGem>().ToMutable() : (JunnaGem)ModelDb.Relic<JunnaGreenGem>().ToMutable();
                gem.Owner = player;
                turn.SetValue(state, 1); await gem.BeforeHandDraw(player, Context, player.Creature.CombatState!);
                Expect("gem turn1 " + upgraded, state.Hand.Cards.Count == 1);
                turn.SetValue(state, 2); await gem.BeforeHandDraw(player, Context, player.Creature.CombatState!);
                Expect("gem turn2 " + upgraded, state.Hand.Cards.Count == (upgraded ? 2 : 1));
                Expect("gem does not grant energy " + upgraded, state.Energy == 99);
            }
            await Reset(player);
            var groups = new List<CardModel[]>();
            for (int i = 0; i < 2; i++)
            {
                var main = player.RunState.CreateCard(CardCatalog.Model("JK184"), player);
                int count = player.Deck.Cards.Count;
                await CardPileCmd.Add(main, PileType.Deck, skipVisuals: true);
                var group = player.Deck.Cards.Skip(count).ToArray(); groups.Add(group);
                Expect("seven-card group " + i, group.Length == 7);
                foreach (var id in new[] { "JK184B", "JK184C", "JK184D" }) Expect("two " + id + " group " + i, group.Count(c => CardCatalog.DesignId(c) == id) == 2);
            }
            var firstMain = groups[0].Single(c => CardCatalog.DesignId(c) == "JK184");
            var secondMain = groups[1].Single(c => CardCatalog.DesignId(c) == "JK184");
            var members = groups[0].OfType<CatalogCard>().Where(c => c.DesignId != "JK184").ToArray();
            foreach (var member in members)
            {
                var context = new ShineDepletionContext(member, member, player, Guid.NewGuid().ToString());
                await member.OnShineDepleted(context); await member.OnShineDepleted(context);
            }
            Expect("growth once per actual member", KnowledgeProgress.PermanentHits(firstMain) == 6);
            Expect("other acquired group isolated", KnowledgeProgress.PermanentHits(secondMain) == 0);
            var saved = player.RunState.LoadCard(firstMain.ToSerializable(), player);
            Expect("growth survives save", KnowledgeProgress.PermanentHits(saved) == 6);
        }
        catch (Exception error) { rows.Add(new { name = "exception", pass = false, error = error.ToString() }); }
        finally
        {
            turn.SetValue(state, originalTurn);
            string path = Path.GetFullPath("../results.json", Environment.CurrentDirectory);
            File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private sealed class Selector(bool later) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            return Task.FromResult<IEnumerable<CardModel>>(cards.Any(c => c is JunnaEnergyNow)
                ? cards.Where(c => later ? c is JunnaEnergyLater : c is JunnaEnergyNow) : cards.Take(maxSelect));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static async Task CheckEnergy(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        try
        {
            foreach (bool upgraded in new[] { false, true })
            {
                await Reset(player);
                await Play(await Card(player, "JH59", upgraded));
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "JH59");
                int before = player.PlayerCombatState!.Energy;
                await power.AfterPlayerTurnStart(Context, player);
                Expect("north star energy " + upgraded, player.PlayerCombatState.Energy == before + (upgraded ? 2 : 1));
                var description = power.SmartDescription;
                power.DynamicVars.AddTo(description);
                Expect("north star description " + upgraded, !description.GetFormattedText().Contains("ERROR"));
                foreach (bool later in new[] { false, true })
                {
                    await Reset(player);
                    using var chartChoices = CardSelectCmd.UseSelector(new Selector(later));
                    var chart = await Card(player, "JH60", upgraded);
                    await Play(chart);
                    Expect("chart immediate " + upgraded + later, player.PlayerCombatState.Energy == 99 + (later ? 0 : upgraded ? 3 : 2));
                    Expect("chart deferred " + upgraded + later, (player.Creature.GetPower<EnergyNextTurnPower>()?.Amount ?? 0) == (later ? upgraded ? 4 : 3 : 0));
                    Expect("chart exhaust " + upgraded + later, chart.Pile?.Type == PileType.Exhaust);
                    Expect("chart Shine " + upgraded + later, Shine.GetState(chart)?.Remaining == 5);
                }
            }
            using var otherChoices = CardSelectCmd.UseSelector(new Selector(false));
            await Reset(player);
            var strike = await Card(player, "JB01");
            await Play(await Card(player, "JG09"));
            Expect("warm up selected hand bonus", DamageModifiers.CaptureDamageModifierSnapshot(strike).DamageBonus == 2);
            await Reset(player);
            var first = await Card(player, "JB01"); var second = await Card(player, "JA38");
            await Play(await Card(player, "JG36"));
            Expect("blade practice all hand", new[] { first, second }.All(c => DamageModifiers.CaptureDamageModifierSnapshot(c).DamageBonus == 2));
            await Reset(player);
            var drawn = await Card(player, "JB01", pile: PileType.Draw); var discarded = await Card(player, "JB01", pile: PileType.Discard);
            await Play(await Card(player, "JK78"));
            Expect("training draw pile only", DamageModifiers.CaptureDamageModifierSnapshot(drawn).DamageBonus == 6 && DamageModifiers.CaptureDamageModifierSnapshot(discarded).DamageBonus == 0);
            await Reset(player);
            var token = await Card(player, "JA119");
            var view = NCard.Create(token)!;
            NCombatRoom.Instance!.CombatVfxContainer.AddChild(view);
            var manager = NTargetManager.Instance;
            manager.StartTargeting(TargetType.AnyEnemy, view, TargetMode.ClickMouseToTarget, null, null);
            Expect("native selector accepts self", manager.AllowedToTargetNode(NCombatRoom.Instance.GetCreatureNode(player.Creature)!));
            Expect("native selector accepts enemy", manager.AllowedToTargetNode(NCombatRoom.Instance.GetCreatureNode(player.Creature.CombatState!.Enemies.First())!));
            manager.CancelTargeting();
            view.QueueFree();
            Expect("latest common aimed shot", CardCatalog.Model("JA145").Rarity == CardRarity.Common);
            Expect("latest uncommon bow", CardCatalog.Model("JA84").Rarity == CardRarity.Uncommon);
        }
        catch (Exception error) { rows.Add(new { name = "exception", pass = false, error = error.ToString() }); }
        finally
        {
            File.WriteAllText(Path.GetFullPath("../energy-results.json", Environment.CurrentDirectory), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static async Task PrepareEnergy(Player player)
    {
        await Reset(player); player.PlayerCombatState!.Energy = 3;
        await Card(player, "JH59"); await Card(player, "JH60");
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player);
        player.PlayerCombatState!.Energy = 5;
        foreach (var id in new[] { "JA38", "JA145", "JA144", "JA119", "JK184", "JK184D" }) await Card(player, id);
        for (int i = 0; i < 8; i++) await Card(player, "JB01", pile: PileType.Draw);
    }
}
