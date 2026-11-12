using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class PileAnimationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_pile_animation";
    public override string Args => "check";
    public override string Description => "Checks real generated/copy entry points and native visual completion.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static HashSet<CardModel>? _before;
    private static string _id = "";
    private static int _previews;
    private static readonly List<(CardModel Card, CardPilePosition Position)> Generated = [];
    private static readonly List<NCardFlyVfx> Flies = [];
    private static bool Matches(CardModel card) => _before != null && !_before.Contains(card) && CardCatalog.DesignId(card) == _id;
    private static void Preview(CardModel card, bool isAddingCardsToPile, TaskCompletionSource? __result)
    { if (Matches(card) && isAddingCardsToPile && __result != null) _previews++; }
    private static void Fly(NCard card, bool isAddingToPile, NCardFlyVfx? __result)
    { if (card.Model is { } model && Matches(model) && isAddingToPile && __result != null) Flies.Add(__result); }
    private static void Generate(CardModel card, CardPilePosition position)
    { if (Matches(card)) Generated.Add((card, position)); }
    private sealed class Selector(CardModel selected, bool skip = false) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult<IEnumerable<CardModel>>(skip ? [] : options.Where(card => card == selected).Take(maxSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static async Task<CardModel> Card(Player player, string id, PileType pile, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = pile == PileType.Deck ? player.RunState.CreateCard(CardCatalog.Model(id), player) : CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, CardPilePosition.Bottom);
        return card;
    }
    private static async Task Check(Player player)
    {
        const string patchId = "Junna.PileAnimation.Observe";
        var patch = new Harmony(patchId);
        patch.Patch(AccessTools.Method(typeof(CardCmd), "PreviewInternal"), postfix: new HarmonyMethod(typeof(PileAnimationCommand), nameof(Preview)));
        patch.Patch(AccessTools.Method(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create), [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)]),
            postfix: new HarmonyMethod(typeof(PileAnimationCommand), nameof(Fly)));
        patch.Patch(AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat)), prefix: new HarmonyMethod(typeof(PileAnimationCommand), nameof(Generate)));
        string output = Path.GetFullPath("../../../out/tmp/study-animation/related.json", Environment.CurrentDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var rows = new List<object>();
        async Task Reset()
        {
            _before = null;
            foreach (var arrow in ArrowCombat.Deployed.ToArray()) ArrowCombat.Cancel(arrow.Card);
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
                if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: card.Pile.Type != PileType.Hand);
            player.PlayerCombatState.Energy = 99;
            for (int i = 0; i < 8; i++) await Card(player, "JB01", PileType.Draw);
        }
        async Task Case(string name, string id, int expected, PileType pile, bool upgraded,
            Func<Task<Func<Task>>> prepare, bool visual = true, int? shine = null)
        {
            try
            {
                await Reset();
                var action = await prepare();
                var original = player.PlayerCombatState!.DrawPile.Cards.ToArray();
                _before = CardActions.All(player).Concat(player.Deck.Cards).ToHashSet();
                _id = id; _previews = 0; Generated.Clear(); Flies.Clear();
                await action();
                long deadline = Environment.TickCount64 + 6000;
                while (_previews > 0 && (Flies.Count < expected || Flies.Any(fly => fly.SwooshAwayCompletion?.Task.IsCompletedSuccessfully != true))
                    && Environment.TickCount64 < deadline) await Task.Delay(50);
                var gained = CardActions.All(player).Concat(player.Deck.Cards).Distinct().Where(Matches).ToArray();
                var draw = player.PlayerCombatState.DrawPile.Cards.ToArray();
                var checks = new Dictionary<string, bool>
                {
                    ["count_version_destination"] = gained.Length == expected && gained.All(card => card.IsUpgraded == upgraded && card.Pile?.Type == pile),
                    ["original_order"] = draw.Where(original.Contains).SequenceEqual(original),
                    ["random_position"] = pile != PileType.Draw || Generated.Count == expected && Generated.All(card => card.Position == CardPilePosition.Random),
                    ["previews"] = _previews == (visual ? expected : 0),
                    ["flies"] = Flies.Count == (visual ? expected : 0),
                    ["completed_flies"] = Flies.Count(fly => fly.SwooshAwayCompletion?.Task.IsCompletedSuccessfully == true) == (visual ? expected : 0),
                    ["shine"] = shine == null || gained.All(card => Shine.GetState(card)?.Remaining == shine)
                };
                rows.Add(new { name, pass = checks.Values.All(pass => pass), expected, previews = _previews, flies = Flies.Count, originalCount = original.Length, drawCount = draw.Length, originalIndices = original.Select(card => Array.IndexOf(draw, card)).ToArray(), checks });
            }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            finally { _before = null; }
            File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            foreach (bool upgraded in new[] { false, true })
                await Case("combat learning upgrade " + upgraded, "JK82B", 1, PileType.Draw, false, async () =>
                {
                    var source = await Card(player, "JK82", PileType.Hand, upgraded);
                    return () => CardUpgrades.Upgrade(source);
                });
            await Case("permanent learning upgrade", "JK82B", 1, PileType.Deck, false, async () =>
            {
                var source = await Card(player, "JK82", PileType.Deck);
                return () => CardUpgrades.Upgrade(source);
            });
            await Case("research upgraded copy", "JK49B", 1, PileType.Draw, true, async () =>
            {
                await CardActions.PaidPlay(Context, await Card(player, "JK15", PileType.Hand));
                var source = await Card(player, "JK49B", PileType.Hand, true);
                return async () => { await CardActions.PaidPlay(Context, source); };
            });
            foreach (var pile in new[] { PileType.Draw, PileType.Discard, PileType.Deck })
                await Case("knowledge companions " + pile, "B", 2, pile, false, async () =>
                {
                    var source = await Card(player, "A", pile);
                    return async () => { await (Task<IReadOnlyList<CardModel>>)AccessTools.Method(typeof(KnowledgeCombat), "GrantPartners")!.Invoke(null, [source, pile, null])!; };
                });
            await Case("single permanent copy keeps upgrade and shine", "JK137", 1, PileType.Deck, true, async () =>
            {
                var source = await Card(player, "JK137", PileType.Deck, true);
                Shine.SetState(source, 2, 6);
                return () => KnowledgeProgress.CopySingleCards(player, [source, source]);
            }, shine: 2);
            await Case("selected permanent copy", "JK137", 1, PileType.Deck, true, async () =>
            {
                var source = await Card(player, "JK137", PileType.Deck, true);
                return async () => { using var selection = CardSelectCmd.UseSelector(new Selector(source)); await KnowledgeProgress.CopyDeckCard(player); };
            });
            await Case("skipped permanent copy", "JK137", 0, PileType.Deck, false, async () =>
            {
                var source = await Card(player, "JK137", PileType.Deck);
                return async () => { using var selection = CardSelectCmd.UseSelector(new Selector(source, true)); await KnowledgeProgress.CopyDeckCard(player); };
            });
            await Case("hand generation uses native hand animation", "JA119", 1, PileType.Hand, false, () =>
                Task.FromResult<Func<Task>>(() => CardActions.Generate(player, "JA119", 1, PileType.Hand, CardPilePosition.Bottom)), visual: false);
        }
        finally
        {
            _before = null;
            patch.UnpatchAll(patchId);
            File.WriteAllText(Path.ChangeExtension(output, ".completed"), "completed");
        }
    }
}