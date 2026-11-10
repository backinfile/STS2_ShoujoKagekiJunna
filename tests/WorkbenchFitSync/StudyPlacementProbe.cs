using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class StudyPlacementCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_study_placement";
    public override string Args => "check | related | prepare";
    public override string Description => "Checks end-turn insight insertion positions.";
    public override bool IsNetworked => false;
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly List<(CardModel Card, CardPilePosition Position)> Generated = [];
    private static readonly string ResultPath = Path.GetFullPath("../../../out/tmp/study-placement/results.json", Environment.CurrentDirectory);

    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() switch
        {
            "prepare" => Prepare(player),
            "related" => CheckRelated(player),
            _ => Check(player)
        }, true, "Started");

    private static void Capture(CardModel card, PileType newPileType, CardPilePosition position)
    {
        if (newPileType == PileType.Draw) Generated.Add((card, position));
    }

    private static async Task Reset(Player player)
    {
        foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        player.PlayerCombatState.Energy = 9;
    }

    private static async Task<CardModel> Card(Player player, string id, PileType pile, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, skipVisuals: false);
        return card;
    }

    private static async Task Check(Player player)
    {
        const string patchId = "JunnaWorkbenchFitSync.StudyPlacement";
        var patch = new Harmony(patchId);
        patch.Patch(AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat)),
            prefix: new HarmonyMethod(typeof(StudyPlacementCommand), nameof(Capture)));
        var rows = new List<object>();
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
        try
        {
            foreach (var (name, upgraded, stacked, existing) in new[]
            {
                ("base", false, false, 8), ("upgraded", true, false, 8),
                ("stacked", true, true, 8), ("empty draw pile", false, false, 0)
            })
            {
                try
                {
                    await Reset(player);
                    for (int i = 0; i < existing; i++) await Card(player, "JB01", PileType.Draw);
                    var original = player.PlayerCombatState!.DrawPile.Cards.ToArray();
                    var source = await Card(player, "A", PileType.Hand, upgraded);
                    await CardActions.PaidPlay(Context, source);
                    if (stacked) await CardActions.PaidPlay(Context, await Card(player, "A", PileType.Hand));
                    Generated.Clear();
                    var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "A");
                    await power.BeforeSideTurnEnd(Context, CombatSide.Player, [player.Creature]);
                    int expected = (upgraded ? 2 : 1) + (stacked ? 1 : 0);
                    var draw = player.PlayerCombatState.DrawPile.Cards.ToArray();
                    bool pass = Generated.Count == expected && Generated.All(c => c.Position == CardPilePosition.Random)
                        && draw.Length == existing + expected && Generated.All(c => !c.Card.IsUpgraded && c.Card.Pile?.Type == PileType.Draw)
                        && draw.Where(c => CardCatalog.DesignId(c) != "B").SequenceEqual(original);
                    rows.Add(new { name, pass, expected, positions = Generated.Select(c => c.Position.ToString()).ToArray(),
                        indices = Generated.Select(c => Array.IndexOf(draw, c.Card)).ToArray(), drawCount = draw.Length });
                }
                catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
                File.WriteAllText(ResultPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally
        {
            patch.UnpatchAll(patchId);
            File.WriteAllText(Path.ChangeExtension(ResultPath, ".completed"), "completed");
        }
    }

    private static async Task CheckRelated(Player player)
    {
        const string patchId = "JunnaWorkbenchFitSync.RelatedPlacement";
        var patch = new Harmony(patchId);
        patch.Patch(AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat)),
            prefix: new HarmonyMethod(typeof(StudyPlacementCommand), nameof(Capture)));
        string path = Path.Combine(Path.GetDirectoryName(ResultPath)!, "related-results.json");
        var rows = new List<object>();
        async Task Case(string name, Func<Task<bool>> action)
        {
            try
            {
                await Reset(player);
                for (int i = 0; i < 8; i++) await Card(player, "JB01", PileType.Draw);
                var original = player.PlayerCombatState!.DrawPile.Cards.ToArray();
                Generated.Clear();
                bool pass = await action();
                var draw = player.PlayerCombatState.DrawPile.Cards.ToArray();
                pass &= draw.Where(original.Contains).SequenceEqual(original);
                rows.Add(new { name, pass, cards = Generated.Select(c => CardCatalog.DesignId(c.Card)).ToArray(),
                    positions = Generated.Select(c => c.Position.ToString()).ToArray(),
                    indices = Generated.Select(c => Array.IndexOf(draw, c.Card)).ToArray() });
            }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        bool RandomCards(string id, int count) => Generated.Count == count
            && Generated.All(c => c.Position == CardPilePosition.Random && CardCatalog.DesignId(c.Card) == id && !c.Card.IsUpgraded);
        try
        {
            foreach (bool alreadyUpgraded in new[] { false, true })
                await Case("combat learning upgrade " + alreadyUpgraded, async () =>
                {
                    var source = await Card(player, "JK82", PileType.Hand, alreadyUpgraded);
                    await ShoujoKageki.Core.V1.CardUpgrades.Upgrade(source);
                    return source.CurrentUpgradeLevel == (alreadyUpgraded ? 2 : 1) && RandomCards("JK82B", 1)
                        && player.PlayerCombatState!.DrawPile.Cards.Count == 9;
                });
            await Case("knowledge companions into draw pile", async () =>
            {
                var source = await Card(player, "A", PileType.Draw);
                var task = (Task<IReadOnlyList<CardModel>>)AccessTools.Method(typeof(KnowledgeCombat), "GrantPartners")!
                    .Invoke(null, [source, PileType.Draw, null])!;
                var gained = await task;
                return gained.Count == 3 && gained.Contains(source) && RandomCards("B", 2)
                    && player.PlayerCombatState!.DrawPile.Cards.Count == 11;
            });
            await Case("retired research copy", async () =>
            {
                await CardActions.PaidPlay(Context, await Card(player, "JK15", PileType.Hand));
                var source = await Card(player, "JK49B", PileType.Hand, upgraded: true);
                await CardActions.PaidPlay(Context, source);
                return Generated.Count == 1 && Generated[0].Position == CardPilePosition.Random
                    && Generated[0].Card != source && Generated[0].Card.Id == source.Id && Generated[0].Card.IsUpgraded;
            });
            await Case("permanent learning upgrade stays in deck", async () =>
            {
                using var single = KnowledgeCombat.SingleCard();
                var source = player.RunState.CreateCard(CardCatalog.Model("JK82"), player);
                await CardPileCmd.Add(source, PileType.Deck, skipVisuals: true);
                int before = player.Deck.Cards.Count;
                await ShoujoKageki.Core.V1.CardUpgrades.Upgrade(source);
                return Generated.Count == 0 && player.Deck.Cards.Count == before + 1
                    && CardCatalog.DesignId(player.Deck.Cards.Last()) == "JK82B";
            });
            await Case("aimed shot still generates in hand", async () =>
            {
                await CardActions.PaidPlay(Context, await Card(player, "JA145", PileType.Hand));
                return Generated.Count == 0 && player.PlayerCombatState!.Hand.Cards.Count == 1
                    && CardCatalog.DesignId(player.PlayerCombatState.Hand.Cards.Single()) == "JA119";
            });
            await Case("targeted draw keeps selected actual card", async () =>
            {
                var arrow = await Card(player, "JA38", PileType.Draw);
                await CardActions.PaidPlay(Context, await Card(player, "JA53", PileType.Hand));
                return Generated.Count == 0 && arrow.Pile?.Type == PileType.Hand
                    && player.PlayerCombatState!.DrawPile.Cards.Count == 8;
            });
        }
        finally
        {
            patch.UnpatchAll(patchId);
            File.WriteAllText(Path.ChangeExtension(path, ".completed"), "completed");
        }
    }

    private static async Task Prepare(Player player)
    {
        await Reset(player);
        await Card(player, "A", PileType.Hand, upgraded: true);
        for (int i = 0; i < 8; i++) await Card(player, "JB01", PileType.Draw);
        foreach (var enemy in player.Creature.CombatState!.Enemies)
        {
            await CreatureCmd.SetMaxHp(enemy, 1000);
            await CreatureCmd.SetCurrentHp(enemy, 1000);
        }
    }
}
