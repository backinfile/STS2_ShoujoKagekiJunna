using System.Reflection;
using System.Text.Json;
using Godot;
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

namespace EndlessLearningAnimation;

public static class AcquisitionFlow
{
    private sealed class Observation(string name, Player player, int generated, bool deck = false)
    {
        internal readonly string Name = name;
        internal readonly Player Player = player;
        internal readonly int Expected = generated;
        internal readonly CardPile Pile = deck ? player.Deck : player.PlayerCombatState!.DrawPile;
        internal readonly CardModel[] Original = (deck ? player.Deck : player.PlayerCombatState!.DrawPile).Cards.ToArray();
        internal readonly List<(CardModel Card, CardPilePosition Position)> Requests = [];
        internal readonly List<Task> Previews = [];
        internal ulong? LastPreviewFrame;
        internal int PreviewCount;
        internal bool? Immediate;
    }
    private static Observation? Active;
    private static readonly List<object> Results = [];
    private static string Output => Path.Combine(System.Environment.GetEnvironmentVariable("JUNNA_ENDLESS_OUTPUT")!, "gain-flow.json");
    private static void Save() => File.WriteAllText(Output, JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat))]
    private static class Generate
    {
        private static void Prefix(CardModel card, PileType newPileType, CardPilePosition position)
        {
            if (Active is { } test && card.Owner == test.Player && newPileType == PileType.Draw)
                test.Requests.Add((card, position));
        }
    }
    [HarmonyPatch(typeof(CardCmd), "PreviewInternal")]
    private static class Preview
    {
        private static void Postfix(CardModel card, bool isAddingCardsToPile, TaskCompletionSource? __result)
        {
            if (Active is not { } test || !isAddingCardsToPile || card.Owner != test.Player || __result == null) return;
            test.LastPreviewFrame = Engine.GetProcessFrames(); test.PreviewCount++; test.Previews.Add(__result.Task);
        }
    }
    private static void Finish(bool upgraded = false)
    {
        var test = Active!;
        var added = test.Pile.Cards.Where(card => !test.Original.Contains(card)).ToArray();
        bool sameFrame = test.LastPreviewFrame == Engine.GetProcessFrames();
        bool pending = test.Previews.Any(task => !task.IsCompleted);
        bool model = added.Length == test.Expected && added.All(card => CardCatalog.DesignId(card) == "B" && card.IsUpgraded == upgraded);
        bool order = test.Pile.Cards.Where(test.Original.Contains).SequenceEqual(test.Original);
        bool positions = test.Pile.Type != PileType.Draw || test.Requests.Count == test.Expected
            && test.Requests.All(request => request.Position == CardPilePosition.Random);
        Results.Add(new { name = test.Name, pass = sameFrame && pending && model && order && positions && test.Immediate != false,
            sameFrame, pending, model, order, positions, immediate = test.Immediate, previews = test.PreviewCount,
            expected = test.Expected, actual = added.Length, requests = test.Requests.Select(r => r.Position.ToString()).ToArray(),
            endFrame = Engine.GetProcessFrames(), previewFrame = test.LastPreviewFrame });
        Active = null; Save();
    }
    private static async Task Reset(Player player, int existing = 8)
    {
        Active = null; Probe.Player = null; Probe.Source = null;
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            if (card.Pile != null) { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
        for (int i = 0; i < existing; i++) await Add(player, "JB01", PileType.Draw);
        player.PlayerCombatState.Energy = 9;
    }
    private static async Task<CardModel> Add(Player player, string id, PileType pile, bool upgraded = false)
    {
        var card = (pile == PileType.Deck ? player.RunState.CreateCard(CardCatalog.Model(id), player)
            : player.Creature.CombatState!.CreateCard(CardCatalog.Model(id), player));
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        using (KnowledgeCombat.SingleCard()) await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, skipVisuals: pile != PileType.Hand);
        return card;
    }
    public static async Task Check(Player player)
    {
        Results.Clear(); Save();
        async Task Case(string name, Func<Task> run)
        {
            try { await run(); }
            catch (Exception error) { Active = null; Results.Add(new { name, pass = false, error = error.Message }); Save(); }
        }
        await Case("shared preview returns immediately", async () =>
        {
            await Reset(player);
            CardPileAddResult result;
            using (KnowledgeCombat.SingleCard()) result = await CardPileCmd.AddGeneratedCardToCombat(
                player.Creature.CombatState!.CreateCard(CardCatalog.Model("B"), player), PileType.Draw, player, CardPilePosition.Random);
            Active = new("shared preview returns immediately", player, 0);
            var method = typeof(CardActions).GetMethod("PreviewAdded", BindingFlags.Static | BindingFlags.NonPublic)!;
            object[] args = method.GetParameters().Length == 3 ? [new CardPileAddResult[] { result }, PileType.Draw, false] : [new CardPileAddResult[] { result }, PileType.Draw];
            var task = (Task)method.Invoke(null, args)!; Active.Immediate = task.IsCompletedSuccessfully;
            await task; Finish();
        });
        await Case("generated insight", async () =>
        {
            await Reset(player); Active = new("generated insight", player, 1);
            await CardActions.Generate(player, "B", 1, PileType.Draw, CardPilePosition.Random); Finish();
        });
        foreach (var pile in new[] { PileType.Draw, PileType.Deck })
            await Case("knowledge partners " + pile, async () =>
            {
                await Reset(player); var source = await Add(player, "A", pile);
                Active = new("knowledge partners " + pile, player, 2, pile == PileType.Deck);
                await (Task<IReadOnlyList<CardModel>>)AccessTools.Method(typeof(KnowledgeCombat), "GrantPartners")
                    .Invoke(null, [source, pile, null])!; Finish();
            });
        await Case("permanent single copy", async () =>
        {
            await Reset(player); var source = await Add(player, "B", PileType.Deck, true);
            Active = new("permanent single copy", player, 1, true);
            await KnowledgeProgress.CopySingleCards(player, [source]); Finish(upgraded: true);
        });
        foreach (var (name, upgraded, stacked, existing) in new[] {
            ("study base", false, false, 8), ("study upgraded", true, false, 8),
            ("study stacked", true, true, 8), ("study empty draw", false, false, 0) })
            await Case(name, async () =>
            {
                await Reset(player, existing);
                await CardActions.FreePlay(new ThrowingPlayerChoiceContext(), await Add(player, "A", PileType.Hand, upgraded));
                if (stacked) await CardActions.FreePlay(new ThrowingPlayerChoiceContext(), await Add(player, "A", PileType.Hand));
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == "A");
                Active = new(name, player, (upgraded ? 2 : 1) + (stacked ? 1 : 0));
                await power.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [player.Creature]); Finish();
            });
        File.WriteAllText(Path.ChangeExtension(Output, ".completed"), "completed");
    }
    public static async Task PrepareStudy(Player player)
    {
        await Reset(player);
        await CreatureCmd.SetMaxHp(player.Creature, 1000); await CreatureCmd.SetCurrentHp(player.Creature, 1000);
        foreach (var enemy in player.Creature.CombatState!.Enemies.Where(c => c.IsAlive))
        { await CreatureCmd.SetMaxHp(enemy, 1000); await CreatureCmd.SetCurrentHp(enemy, 1000); }
        await Add(player, "A", PileType.Hand);
    }
}
public sealed class AcquisitionFlowCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_gain_flow";
    public override string Args => "check | prepare";
    public override string Description => "Check that acquisition previews do not delay subsequent logic.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(args.FirstOrDefault() == "prepare" ? AcquisitionFlow.PrepareStudy(player)
            : AcquisitionFlow.Check(player), true, "Acquisition flow check started");
}
