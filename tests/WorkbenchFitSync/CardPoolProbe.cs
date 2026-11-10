using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
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

public sealed class CardPoolSyncCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_pool_sync";
    public override string Args => "check | prepare";
    public override string Description => "Checks the synchronized card effects.";
    public override bool IsNetworked => false;
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly string ResultPath = Path.GetFullPath("../../../out/tmp/card-pool-sync/results.json", Environment.CurrentDirectory);

    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");

    private static async Task Reset(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 100000);
            await CreatureCmd.SetCurrentHp(enemy, 100000);
            await CreatureCmd.LoseBlock(Context, enemy, enemy.Block, null);
        }
        KnowledgeProgress.ResetCombat();
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
        void Expect(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
        async Task Case(string name, Func<Task> test)
        {
            try { await Reset(player); await test(); rows.Add(new { name, pass = true }); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
            File.WriteAllText(ResultPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        foreach (bool upgraded in new[] { false, true })
        {
            await Case("Current descriptions and retired rewards " + upgraded, async () =>
            {
                foreach (var id in new[] { "JK67B", "JS19", "JK254", "JK275", "JK314", "U_CARD_008", "U_CARD_008B" })
                {
                    var card = await Card(player, id, upgraded, PileType.Draw);
                    Expect(!card.GetDescriptionForPile(PileType.Hand).Contains("ERROR"), id + " description");
                }
                foreach (var id in new[] { "JK78", "JA77", "JK184", "JK184B", "JK184C", "JK184D", "JA205", "U_CARD_008B" })
                    Expect(!CardCatalog.IsRewardCard(CardCatalog.Model(id)), id + " reward exclusion");
            });
            await Case("Updated arrow damage " + upgraded, async () =>
            {
                var foe = player.Creature.CombatState!.Enemies.First();
                int hp = foe.CurrentHp;
                var arrow = await Card(player, "JK67B", upgraded);
                await Play(arrow);
                await ArrowCombat.Accelerate(Context, ArrowCombat.Find(arrow)!, int.MaxValue);
                Expect(hp - foe.CurrentHp == 21, "Three hits of seven");
            });
            await Case("Learning cost and damage " + upgraded, async () =>
            {
                var foe = player.Creature.CombatState!.Enemies.First();
                int hp = foe.CurrentHp;
                await Play(await Card(player, "JS19", upgraded));
                Expect(hp - foe.CurrentHp == (upgraded ? 20 : 15), "Learning damage");
                Expect(player.PlayerCombatState!.Energy == 98, "Learning costs one");
            });
            await Case("Single area hit " + upgraded, async () =>
            {
                var foes = player.Creature.CombatState!.Enemies.ToArray();
                await Play(await Card(player, "JK275", upgraded));
                Expect(foes.All(foe => 100000 - foe.CurrentHp == (upgraded ? 26 : 20)), "One hit on each enemy");
            });
            await Case("Partner discount and hand snapshot " + upgraded, async () =>
            {
                await Play(await Card(player, "JK254", upgraded));
                var partner = await Card(player, "JK11B");
                var late = await Card(player, "JK11B", pile: PileType.Draw);
                var unrelated = await Card(player, "JB01");
                await Play(await Card(player, "JK11"));
                Expect(partner.Pile?.Type == PileType.Hand && CardActions.Cost(partner) == 0, "Partner discounted without autoplay");
                Expect(late.Pile?.Type == PileType.Hand && CardActions.Cost(late) == 1, "Partner entering during effect keeps cost");
                Expect(CardActions.Cost(unrelated) == 1, "Unrelated card keeps cost");
                await Play(partner);
                Expect(CardActions.Cost(partner) == 1, "Discount expires after play");
            });
            await Case("Bound depletion adds hits and survives saves " + upgraded, async () =>
            {
                var main = (CatalogCard)player.RunState.CreateCard(CardCatalog.Model("JK314"), player);
                if (upgraded) { main.UpgradeInternal(); main.FinalizeUpgradeInternal(); }
                int before = player.Deck.Cards.Count;
                await CardPileCmd.Add(main, PileType.Deck, skipVisuals: true);
                var members = player.Deck.Cards.Skip(before).OfType<CatalogCard>().Where(c => c.DesignId == "JK122B").ToArray();
                Expect(members.Length == 3, "Three bound desire cards");
                var combatCard = (CatalogCard)await Card(player, "JK314", upgraded);
                combatCard.DeckVersion = main;
                foreach (var member in members)
                {
                    var depletion = new ShineDepletionContext(member, member, player, Guid.NewGuid().ToString());
                    await member.OnShineDepleted(depletion);
                    await member.OnShineDepleted(depletion);
                }
                Expect(KnowledgeProgress.PermanentHits(main) == 3 && KnowledgeProgress.PermanentHits(combatCard) == 3, "Each bound depletion counts once");
                var restored = player.RunState.LoadCard(main.ToSerializable(), player);
                Expect(KnowledgeProgress.PermanentHits(restored) == 3, "Saved progress");
                int hp = player.Creature.CombatState!.Enemies.First().CurrentHp;
                DamageModifiers.Add(combatCard, 2);
                await Play(combatCard);
                Expect(hp - player.Creature.CombatState.Enemies.First().CurrentHp == (upgraded ? 14 : 10) * 5, "Five hits with per-hit combat bonus");
            });
            await Case("Stories stack and settle once " + upgraded, async () =>
            {
                int before = player.Deck.Cards.Count;
                await Play(await Card(player, "U_CARD_008", upgraded));
                await Play(await Card(player, "U_CARD_008", upgraded));
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "U_CARD_008");
                var description = power.SmartDescription;
                power.DynamicVars.AddTo(description);
                Expect(!description.GetFormattedText().Contains("ERROR"), "Story power description");
                await PowerCmd.Remove(power);
                await KnowledgeProgress.WonCombat();
                var stories = player.Deck.Cards.Skip(before).ToArray();
                Expect(stories.Length == 2 && stories.All(c => CardCatalog.DesignId(c) == "U_CARD_008B" && !c.IsUpgraded), "Two base stories added permanently");
                Expect(stories.All(c => Shine.GetState(c) is { Maximum: 3, Remaining: 3, Temporary: false }), "Independent permanent Shine budgets");
                await KnowledgeProgress.WonCombat();
                Expect(player.Deck.Cards.Count == before + 2, "No duplicate settlement");
            });
            await Case("Story draws and exhausts " + upgraded, async () =>
            {
                for (int i = 0; i < 4; i++) await Card(player, "JB01", pile: PileType.Draw);
                var story = await Card(player, "U_CARD_008B", upgraded);
                await Play(story);
                Expect(player.PlayerCombatState!.Hand.Cards.Count == (upgraded ? 3 : 2), "Story draw count");
                Expect(story.Pile?.Type == PileType.Exhaust && Shine.GetState(story)?.Remaining == 2, "Exhaust and one Shine use");
            });
        }
        File.WriteAllText(Path.ChangeExtension(ResultPath, ".completed"), "completed");
    }

    private static async Task Prepare(Player player)
    {
        await Reset(player);
        player.PlayerCombatState!.Energy = 8;
        foreach (var id in new[] { "U_CARD_008", "U_CARD_008B", "JK254", "JK11", "JK11B", "JK314", "JK275" })
            await Card(player, id);
        for (int i = 0; i < 8; i++) await Card(player, "JB01", pile: PileType.Draw);
    }
}
