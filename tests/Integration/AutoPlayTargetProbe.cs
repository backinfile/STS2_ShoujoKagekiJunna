using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class AutoPlayTargetProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_auto_target";
    public override string Args => "check | prepare | inspect";
    public override string Description => "Check automatic targets without manual enemy selection.";
    public override bool IsNetworked => false;
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
    private static readonly List<(CardModel Card, Creature? Target)> Plays = [];
    private static bool RejectManualTarget(PlayerChoiceContext context, CardModel card, ref Task<Creature?> __result)
    {
        __result = Task.FromException<Creature?>(new InvalidOperationException("Automatic play requested manual enemy selection"));
        return false;
    }
    private static void Observe(CardModel card, Creature? target) => Plays.Add((card, target));
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "inspect") { Save(player, new { plays = ArrowCombat.Deployed.Select(a => new { id = CardCatalog.DesignId(a.Card), target = a.Play.Target?.CombatId, a.Remaining }) }); return new(true, "Saved"); }
        return new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    }
    private static string DirectoryPath => Path.GetFullPath("../../../out/tmp/auto-target");
    private static void Save(Player player, object value)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "live.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
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
        }
        player.PlayerCombatState.Energy = 10;
        Plays.Clear();
    }
    private static async Task<CardModel> Add(Player player, string id, PileType pile = PileType.Hand, bool upgraded = false, bool visuals = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, skipVisuals: !visuals);
        return card;
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player, visuals: true);
        await Add(player, "JA65", visuals: true);
        await Add(player, "JB03", PileType.Draw);
        for (int i = 0; i < 5; i++)
        {
            var filler = player.Creature.CombatState!.CreateCard<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(player);
            await CardPileCmd.Add(filler, PileType.Draw, CardPilePosition.Top, skipVisuals: true);
        }
        Save(player, new { prepared = true });
    }
    private static async Task Check(Player player)
    {
        if (player.Creature.CombatState!.HittableEnemies.Count() < 2) throw new InvalidOperationException("Two enemies required");
        var results = new List<object>();
        var harmony = new Harmony("Junna.AutoPlayTargetProbe");
        // Fail immediately at the former prompt boundary, even when a card selector is installed.
        var chooseTarget = AccessTools.Method(typeof(CardActions), "ChooseTarget");
        if (chooseTarget != null) harmony.Patch(chooseTarget, prefix: new HarmonyMethod(typeof(AutoPlayTargetProbe), nameof(RejectManualTarget)));
        var before = AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardAutoPlayed));
        harmony.Patch(before, prefix: new HarmonyMethod(typeof(AutoPlayTargetProbe), nameof(Observe)));
        double speed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 20;
        void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Played(CardModel card) => Expect(Plays.Any(p => p.Card == card && p.Target is { IsAlive: true } && card.IsValidTarget(p.Target)), "Native automatic play must receive a living valid target");
        async Task Case(string name, Func<Task> action)
        {
            try { await Reset(player); await action(); results.Add(new { name, pass = true }); }
            catch (Exception error) { results.Add(new { name, pass = false, error = error.ToString() }); }
        }
        try
        {
            foreach (bool up in new[] { false, true })
                await Case("伏击 resolves selected arrow automatically " + up, async () =>
                {
                    var arrow = await Add(player, "JB03", PileType.Draw);
                    var source = await Add(player, "JA65", upgraded: up);
                    await TestGameApi.PaidPlay(Context, source);
                    using (TestGameApi.UseSelector(new FirstCardSelector()))
                        await ArrowCombat.Accelerate(Context, ArrowCombat.Find(source)!, int.MaxValue);
                    Played(arrow);
                    Expect(ArrowCombat.Find(arrow)?.Remaining == 3, "Selected actual arrow must deploy");
                    Expect(player.PlayerCombatState!.Energy == (up ? 10 : 9), "Only Ambush costs energy");
                });
            await Case("FreePlay works without a card selector", async () =>
            {
                var arrow = await Add(player, "JB03");
                await CardActions.FreePlay(Context, arrow);
                Played(arrow);
                Expect(player.PlayerCombatState!.Energy == 10, "Automatic play is free");
            });
            await Case("Knowledge partners automatically target", async () =>
            {
                var partner = await Add(player, "JK50");
                var source = await Add(player, "JK50B");
                await TestGameApi.PaidPlay(Context, source);
                Played(partner);
            });
            await Case("Captured knowledge partners automatically target", async () =>
            {
                var partner = await Add(player, "JK32B");
                var source = await Add(player, "JK32");
                await KnowledgeCombat.PlayPartners(Context, source);
                Played(partner);
            });
            await Case("Explicit target remains attached to replay", async () =>
            {
                var arrow = await Add(player, "JB03");
                var target = player.Creature.CombatState!.HittableEnemies.Last();
                await CardActions.FreePlay(Context, arrow, target, keepTarget: true);
                Expect(ArrowCombat.Find(arrow)?.Play.Target == target, "Preserve explicit target");
            });
            await Case("Self target stays automatic", async () =>
            {
                var card = await Add(player, "JB02");
                await CardActions.FreePlay(Context, card);
                Expect(card.Pile?.Type == PileType.Discard, "Defend resolves without an enemy");
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Godot.Engine.TimeScale = speed;
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, "results.json"), JsonSerializer.Serialize(new { completed = true, results }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private sealed class FirstCardSelector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) => Task.FromResult(options.Take(maxSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult> options, IReadOnlyList<MegaCrit.Sts2.Core.Entities.CardRewardAlternatives.CardRewardAlternative> alternatives) => throw new NotSupportedException();
    }
}
