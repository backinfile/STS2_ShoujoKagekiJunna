using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;
using ShoujoKageki.Core.V1;

namespace JunnaIntegration;

public sealed class EffectReplayProbe : AbstractConsoleCmd
{
    private sealed class Selector : MegaCrit.Sts2.Core.TestSupport.ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult<IEnumerable<CardModel>>(options.Take(maxSelect).ToArray());
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static NCard? _rewardCard;
    private static string Report(string file) => Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory,
        "../../../out/tmp/scroll-box-stacks-20261107/", file));
    public override string CmdName => "junna_effect_replay";
    public override string Args => "check | prepare | reward-show | reward-check";
    public override string Description => "Check free effect plays and knowledge acquisition animation.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        switch (args.FirstOrDefault())
        {
            case "shine": return new(CheckShine(player), true, "Shine timing checks started");
            case "sync": return new(CheckSync(player), true, "Workbench effect checks started");
            case "reward-show":
                var cards = new[] { "A", "JK32", "JB01" }.Select(id => player.RunState.CreateCard(CardCatalog.Model(id), player)).ToArray();
                var reward = new CardReward(cards, CardCreationSource.Other, player,
                    new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform));
                _ = reward.SelectUnsynchronized();
                _rewardCard = Descendants(MegaCrit.Sts2.Core.Nodes.NGame.Instance!).OfType<NGridCardHolder>()
                    .First(holder => holder.CardModel == cards[0]).CardNode;
                return new(true, "Reward prepared");
            case "reward-check": return new(CheckReward(), true, "Watching native reward animation");
            default: return player.PlayerCombatState == null ? new(false, "Combat required")
                : new(Check(player, args.Contains("prepare")), true, "Effect play checks started");
        }
    }
    private static async Task CheckSync(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
        var target = player.Creature.CombatState!.HittableEnemies.First(c => c.IsAlive);
        var source = CardCatalog.Create("JA96", player);
        using (KnowledgeCombat.SingleCard()) await CardPileCmd.Add(source, PileType.Hand, skipVisuals: true);
        player.PlayerCombatState!.Energy = 9;
        await CardActions.PaidPlay(context, source);
        var arrows = new[] { CardCatalog.Create("JB03", player), CardCatalog.Create("JA80", player), CardCatalog.Create("JA77", player) };
        foreach (var arrow in arrows)
        {
            var play = new CardPlay { Card = arrow,
#if STS2_BETA
            Player = player,
#endif
 Target = arrow.TargetType == TargetType.Self ? player.Creature : target,
                ResultPile = PileType.Discard, Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            ArrowCombat.Deploy(arrow, play, 3, (_, _) => Task.CompletedTask);
            await CardPileCmd.AddGeneratedCardToCombat(arrow, ArrowPile.PileId, player);
        }
        await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
        await TestGameApi.Damage(context, [player.Creature], 1, ValueProp.Move, target, null, null);
        var rows = new[] {
            new { name = "updated damage reaction advances single enemy arrow", pass = ArrowCombat.Find(arrows[0])?.Remaining == 2 },
            new { name = "updated damage reaction preserves self arrow", pass = ArrowCombat.Find(arrows[1])?.Remaining == 3 },
            new { name = "updated damage reaction advances all-enemy arrow", pass = ArrowCombat.Find(arrows[2])?.Remaining == 2 }
        };
        File.WriteAllText(Report("workbench-sync-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task CheckShine(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        var rows = new List<object>();
        void Check(string name, object actual, object expected)
        { rows.Add(new { name, actual, expected, pass = Equals(actual, expected) }); Save(); }
        void Save() => File.WriteAllText(Report("shine-timing-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var id in new[] { "JK148F", "JK133", "JK148D" })
        {
            bool adapted = id == "JK148D";
            var permanent = player.RunState.CreateCard(CardCatalog.Model(id), player);
            using (KnowledgeCombat.SingleCard()) await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
            int remaining = Shine.GetState(permanent)!.Maximum;
            Shine.SetState(permanent, remaining, remaining);
            var card = player.Creature.CombatState!.CloneCard(permanent);
            card.DeckVersion = permanent;
            using (KnowledgeCombat.SingleCard()) await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            if (adapted) HandArrow.Adapt(card);
            player.PlayerCombatState!.Energy = 9;
            var target = player.Creature.CombatState.HittableEnemies.First(c => c.IsAlive);
            await CreatureCmd.SetMaxHp(target, 10000);
            await CreatureCmd.SetCurrentHp(target, 10000);
            await CardActions.PaidPlay(context, card, target);
            Check(id + " deployment preserves combat shine", Shine.GetState(card)!.Remaining, remaining);
            Check(id + " deployment preserves permanent shine", Shine.GetState(permanent)!.Remaining, remaining);
            var arrow = ArrowCombat.Find(card)!;
            await ArrowCombat.Accelerate(context, arrow, 99);
            Check(id + " completed resolution consumes once", Shine.GetState(card)!.Remaining, remaining - 1);
            Check(id + " completed resolution updates permanent shine", Shine.GetState(permanent)!.Remaining, remaining - 1);
        }
    }
    private static async Task CheckReward()
    {
        var source = _rewardCard!;
        for (int frame = 0; frame < 1800; frame++)
        {
            await source.ToSignal(source.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(source) || !source.IsInsideTree()) break;
            if (source.Body.Scale.X >= .35f) continue;
            var stack = source.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack");
            int visible = stack?.GetChildren().OfType<NCard>().Count(card => card.IsVisibleInTree()) ?? 0;
            File.WriteAllText(Report("reward-flight-results.json"), JsonSerializer.Serialize(new[] {
                new { name = "native flight shrinks the selected card", pass = true, actual = source.Body.Scale.X },
                new { name = "native flight leaves no full-size companion cards", pass = visible == 0, actual = (float)visible }
            }, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        File.WriteAllText(Report("reward-flight-results.json"), "[{\"name\":\"native flight observed\",\"pass\":false}]");
    }
    private static async Task Check(Player player, bool prepare)
    {
        using var selector = CardSelectCmd.UseSelector(new Selector());
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.First(c => c.IsAlive);
        await CreatureCmd.SetMaxHp(target, 10000);
        await CreatureCmd.SetCurrentHp(target, 10000);
        foreach (var card in CardActions.All(player).ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        var rows = new List<object>();
        void CheckValue(string name, object? actual, object? expected)
        { rows.Add(new { name, actual, expected, pass = Equals(actual, expected) }); Save(); }
        void Save() => File.WriteAllText(Report("free-play-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        async Task<CardModel> Card(string id, PileType pile)
        {
            var card = CardCatalog.Create(id, player);
            using var single = KnowledgeCombat.SingleCard();
            await CardPileCmd.Add(card, pile, skipVisuals: true);
            return card;
        }
        Task<bool> Play(CardModel card) => CardActions.PaidPlay(context, card, target);
        try
        {
            var chain = await Card("JA67", PileType.Hand);
            player.PlayerCombatState!.Energy = 2;
            await Play(chain);
            if (prepare)
            {
                await Card("JA71", PileType.Hand);
                player.PlayerCombatState.Energy = 1;
                return;
            }
            CheckValue("manual chain arrow pays its cost", player.PlayerCombatState.Energy, 0);
            var old = ArrowCombat.Find(chain)!;
            int hp = target.CurrentHp;
            await ArrowCombat.Accelerate(context, old, 2);
            CheckValue("chain arrow deals damage at zero energy", hp - target.CurrentHp, (int)chain.DynamicVars.Damage.BaseValue);
            CheckValue("chain arrow redeploys at zero energy", ArrowCombat.Find(chain)?.Remaining, 2);
            CheckValue("chain arrow replaces its old deployment", ArrowCombat.Find(chain) is { } fresh && fresh != old, true);
            CheckValue("chain replay keeps zero energy", player.PlayerCombatState.Energy, 0);
            if (ArrowCombat.Find(chain) is { } next)
            {
                player.PlayerCombatState.Energy = 4;
                await ArrowCombat.Accelerate(context, next, 2);
                CheckValue("chain replay preserves available energy", player.PlayerCombatState.Energy, 4);
                CheckValue("chain replay keeps one deployment", ArrowCombat.Deployed.Count(a => a.Card == chain), 1);
                for (int turn = 0; turn < 2; turn++)
                {
                    player.PlayerCombatState.IncrementTurnNumber();
                    await (Task)AccessTools.Method(typeof(ArrowCombat), "Advance").Invoke(null, [context, player])!;
                }
                CheckValue("turn-start replay keeps its full new countdown", ArrowCombat.Find(chain)?.Remaining, 2);
                await (Task)AccessTools.Method(typeof(ArrowCombat), "Advance").Invoke(null, [context, player])!;
                CheckValue("same turn cannot tick the new deployment again", ArrowCombat.Find(chain)?.Remaining, 2);
            }
            ArrowCombat.Cancel(chain);
            await CardPileCmd.RemoveFromCombat(chain, skipVisuals: true);
            player.PlayerCombatState.Energy = 4;
            await Play(await Card("JH19", PileType.Hand));
            hp = target.CurrentHp;
            await Play(await Card("JB01", PileType.Hand));
            CheckValue("explicit paid replay still spends extra energy", player.PlayerCombatState.Energy, 1);
            CheckValue("explicit paid replay still resolves twice", hp - target.CurrentHp, 12);
            await CreatureCmd.Kill(target);
            target = player.Creature.CombatState!.HittableEnemies.Last(c => c.IsAlive);
            await CreatureCmd.SetMaxHp(target, 1000);
            await CreatureCmd.SetCurrentHp(target, 1000);
            var aimed = await Card("JA67", PileType.Hand);
            player.PlayerCombatState.Energy = 2;
            await Play(aimed);
            await ArrowCombat.Accelerate(context, ArrowCombat.Find(aimed)!, 2);
            CheckValue("replay keeps a non-first original target", ArrowCombat.Find(aimed)?.Target == target, true);
            await CreatureCmd.Kill(target);
            var replacement = ArrowCombat.Find(aimed)?.Target;
            CheckValue("death uses normal arrow retargeting", replacement?.IsAlive == true && replacement != target, true);
            if (replacement != null && ArrowCombat.Find(aimed) is { } retargeted)
            {
                await CreatureCmd.SetMaxHp(replacement, 1000);
                await CreatureCmd.SetCurrentHp(replacement, 1000);
                await ArrowCombat.Accelerate(context, retargeted, 2);
                CheckValue("replay keeps the automatic replacement target", ArrowCombat.Find(aimed)?.Target == replacement, true);
                CheckValue("retargeted replay remains free", player.PlayerCombatState.Energy, 0);
            }
        }
        catch (Exception ex) { rows.Add(new { name = "effect play checks", pass = false, error = ex.ToString() }); Save(); }
    }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
}
