using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class StarterRelicProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_relics";
    public override string Args => "check | prepare";
    public override string Description => "Check starter gem healing through native deck commands.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Starter relic checks started");

    private static async Task Prepare(Player player)
    {
        await CreatureCmd.SetCurrentHp(player.Creature, 40);
        var cards = new[] { "JK11", "JA105", "JG03" }
            .Select(id => player.RunState.CreateCard(CardCatalog.Model(id), player)).ToArray();
        var reward = new CardReward(cards, CardCreationSource.Other, player,
            new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform));
        _ = reward.SelectUnsynchronized();
    }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Expect(string name, bool pass) => rows.Add(new { name, pass });
        var originalCards = player.Deck.Cards.ToHashSet();
        var originalGems = player.Relics.OfType<JunnaGem>().ToArray();
        int originalHp = player.Creature.CurrentHp;
        double speed = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 15;
        try
        {
            Expect("workbench r4456", CardCatalog.Revision == 4456);
            Expect("107 cards and 20 knowledge groups", CardCatalog.Cards.Count == 107 && CardCatalog.Groups.Count == 20);
            Expect("all active model rarities match workbench", CardCatalog.Cards.All(definition =>
                CardCatalog.Model(definition.Id).Rarity == definition.CardRarity));
            Expect("updated rarity", CardCatalog.Model("JG03").Rarity == CardRarity.Uncommon
                && CardCatalog.Model("JK48").Rarity == CardRarity.Rare && CardCatalog.Model("JK48B").Rarity == CardRarity.Rare);
            Expect("updated thesis values", CardCatalog.Get("JK11").Values.First().Base == 7
                && CardCatalog.Get("JK11").Values.First().Upgraded == 10
                && CardCatalog.Get("JK11B").Values.First().Base == 10
                && CardCatalog.Get("JK11B").Values.First().Upgraded == 13);
            foreach (var gem in originalGems) await RelicCmd.Remove(gem);
            foreach (var (canonical, amount) in new (RelicModel, int)[]
                { (ModelDb.Relic<JunnaGreenGem>(), 1), (ModelDb.Relic<JunnaBurningGem>(), 2) })
            {
                var gem = await RelicCmd.Obtain(canonical.ToMutable(), player);
                string label = gem.Id.Entry;
                string description = gem.DynamicDescription.GetFormattedText();
                Expect(label + " description excludes notes", description.Contains($"回复{amount}点生命") && !description.Contains("按张数"));
                Expect(label + " no generated-card tooltip", !gem.HoverTips.Any(tip => tip is MegaCrit.Sts2.Core.HoverTips.CardHoverTip));
                await CreatureCmd.SetCurrentHp(player.Creature, 20);
                var single = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
                await CardPileCmd.Add(single, PileType.Deck, skipVisuals: true);
                Expect(label + " single gain", player.Creature.CurrentHp == 20 + amount);
                await CardPileCmd.RemoveFromDeck(single, showPreview: false);
                Expect(label + " single loss", player.Creature.CurrentHp == 20 + amount * 2);
                await CreatureCmd.SetCurrentHp(player.Creature, 20);
                var batch = Enumerable.Range(0, 3).Select(_ => player.RunState.CreateCard(CardCatalog.Model("JB02"), player)).ToArray();
                await CardPileCmd.Add(batch, player.Deck, skipVisuals: true);
                Expect(label + " three-card gain", player.Creature.CurrentHp == 20 + amount * 3);
                await CardPileCmd.RemoveFromDeck(batch, showPreview: false);
                Expect(label + " three-card loss", player.Creature.CurrentHp == 20 + amount * 6);
                await CreatureCmd.SetCurrentHp(player.Creature, 20);
                int before = player.Deck.Cards.Count;
                var knowledge = player.RunState.CreateCard(CardCatalog.Model("JK11"), player);
                await CardPileCmd.Add(knowledge, PileType.Deck, skipVisuals: true);
                Expect(label + " knowledge group gains two physical cards", player.Deck.Cards.Count == before + 2
                    && player.Creature.CurrentHp == 20 + amount * 2);
                var gained = player.Deck.Cards.Where(card => !originalCards.Contains(card)).ToArray();
                await CardPileCmd.RemoveFromDeck(gained, showPreview: false);
                Expect(label + " knowledge batch loss", player.Creature.CurrentHp == 20 + amount * 4);
                await CreatureCmd.SetCurrentHp(player.Creature, 20);
                var temporary = CardCatalog.Create("JB01", player);
                await CardPileCmd.AddGeneratedCardToCombat(temporary, PileType.Hand, player);
                await CardPileCmd.Add(temporary, PileType.Discard, skipVisuals: true);
                await CardPileCmd.RemoveFromCombat(temporary, skipVisuals: true);
                Expect(label + " combat generation, movement and removal", player.Creature.CurrentHp == 20);
                var transformed = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
                await CardPileCmd.Add(transformed, PileType.Deck, skipVisuals: true);
                await CreatureCmd.SetCurrentHp(player.Creature, 20);
                await CardCmd.Transform(transformed, player.RunState.CreateCard(CardCatalog.Model("JB02"), player));
                Expect(label + " transformation counts old and new cards", player.Creature.CurrentHp == 20 + amount * 2);
                await CreatureCmd.SetCurrentHp(player.Creature, player.Creature.MaxHp - 1);
                var capped = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
                await CardPileCmd.Add(capped, PileType.Deck, skipVisuals: true);
                Expect(label + " healing capped at max hp", player.Creature.CurrentHp == player.Creature.MaxHp);
                await RelicCmd.Remove(gem);
                await CardPileCmd.RemoveFromDeck(player.Deck.Cards.Where(card => !originalCards.Contains(card)).ToArray(), showPreview: false);
            }
            var touch = (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>().ToMutable();
            Expect("native starter upgrade replacement", touch.GetUpgradedStarterRelic(ModelDb.Relic<JunnaGreenGem>()).Id == ModelDb.Relic<JunnaBurningGem>().Id);
        }
        catch (Exception error) { rows.Add(new { name = "exception", pass = false, error = error.ToString() }); }
        finally
        {
            foreach (var gem in player.Relics.OfType<JunnaGem>().ToArray()) await RelicCmd.Remove(gem);
            await CardPileCmd.RemoveFromDeck(player.Deck.Cards.Where(card => !originalCards.Contains(card)).ToArray(), showPreview: false);
            foreach (var gem in originalGems) await RelicCmd.Obtain(((RelicModel)gem.CanonicalInstance).ToMutable(), player);
            await CreatureCmd.SetCurrentHp(player.Creature, originalHp);
            Godot.Engine.TimeScale = speed;
            string directory = Environment.GetEnvironmentVariable("JUNNA_TEST_OUTPUT") ?? Path.Combine(Environment.CurrentDirectory, "out/tmp/relic-health");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "relic-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
