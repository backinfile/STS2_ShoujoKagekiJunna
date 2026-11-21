using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
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

namespace JunnaIntegration;

public sealed class Workbench5462Command : AbstractConsoleCmd
{
    public override string CmdName => "junna_sync_5462";
    public override string Args => "check | prepare";
    public override string Description => "Targeted workbench r5462 balance checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Started");
    private static readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();

    private static async Task Reset(Player player, bool visuals = false)
    {
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: !visuals); }
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        foreach (var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, 100000); await CreatureCmd.SetCurrentHp(enemy, 100000);
            await TestGameApi.LoseBlock(Context, enemy, enemy.Block, null);
        }
        await TestGameApi.LoseBlock(Context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState.Energy = 20;
    }
    private static async Task<CardModel> Add(Player player, string id, bool upgraded = false, PileType pile = PileType.Hand)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, CardPilePosition.Bottom);
        return card;
    }
    private static async Task Play(CardModel card)
    {
        if (!await TestGameApi.PaidPlay(Context, card, card.Owner.Creature.CombatState!.HittableEnemies.Last()))
            throw new InvalidOperationException("Play rejected: " + CardCatalog.DesignId(card));
    }
    private static async Task Check(Player player)
    {
        int assertions = 0;
        var results = new List<object>();
        double speed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 6;
        void Equal<T>(T actual, T expected, string message)
        { assertions++; if (!Equals(actual, expected)) throw new InvalidOperationException($"{message}: {actual} != {expected}"); }
        async Task Case(string name, Func<Task> run)
        {
            try { await Reset(player); await run(); results.Add(new { name, pass = true }); }
            catch (Exception e) { results.Add(new { name, pass = false, error = e.ToString() }); }
        }
        try
        {
            foreach (bool up in new[] { false, true })
            {
                await Case("A cost and one base note " + up, async () =>
                {
                    var existing = new List<CardModel>();
                    for (int n = 0; n < 3; n++) existing.Add(await Add(player, "JB02", pile: PileType.Draw));
                    await Play(await Add(player, "A", up));
                    Equal(player.PlayerCombatState!.Energy, up ? 19 : 18, "Paid energy");
                    await player.Creature.GetPower<JunnaPowerA>()!.BeforeSideTurnEnd(Context, CombatSide.Player, [player.Creature]);
                    var draw = player.PlayerCombatState.DrawPile.Cards;
                    Equal(draw.Count, 4, "One generated note");
                    Equal(draw.Where(c => existing.Contains(c)).SequenceEqual(existing), true, "Original draw order preserved");
                    Equal(draw.Count(c => CardCatalog.DesignId(c) == "B" && !c.IsUpgraded), 1, "Base note version");
                });
                await Case("JA43 block and one acceleration " + up, async () =>
                {
                    var arrow = await Add(player, "U_CARD_025"); await Play(arrow);
                    await Play(await Add(player, "JA43", up));
                    Equal(player.Creature.Block, up ? 14m : 10m, "Block");
                    Equal(ArrowCombat.Find(arrow)?.Remaining, 4, "One acceleration");
                });
                await Case("JK122 damage " + up, async () =>
                {
                    var enemy = player.Creature.CombatState!.HittableEnemies.Last(); int hp = enemy.CurrentHp;
                    await Play(await Add(player, "JK122", up));
                    Equal(hp - enemy.CurrentHp, up ? 20 : 15, "Damage");
                });
                await Case("JK133 six Shine and delayed damage " + up, async () =>
                {
                    var card = await Add(player, "JK133", up);
                    Equal(Shine.GetState(card)?.Maximum, 6, "Shine budget");
                    var enemy = player.Creature.CombatState!.HittableEnemies.Last(); int hp = enemy.CurrentHp;
                    await Play(card);
                    Equal(Shine.GetState(card)?.Remaining, 6, "Deployment preserves Shine");
                    await ArrowCombat.Accelerate(Context, ArrowCombat.Find(card)!, 1);
                    Equal(hp - enemy.CurrentHp, up ? 12 : 9, "Completion damage");
                    Equal(Shine.GetState(card)?.Remaining, 5, "One completed play spends one Shine");
                });
                await Case("JA145 damage and one base generated card " + up, async () =>
                {
                    var enemy = player.Creature.CombatState!.HittableEnemies.Last(); int hp = enemy.CurrentHp;
                    await Play(await Add(player, "JA145", up));
                    Equal(hp - enemy.CurrentHp, up ? 12 : 8, "Damage");
                    Equal(player.PlayerCombatState!.Hand.Cards.Count, 1, "One generated card");
                    Equal(CardCatalog.DesignId(player.PlayerCombatState.Hand.Cards.Single()), "JA119", "Generated model");
                    Equal(player.PlayerCombatState.Hand.Cards.Single().IsUpgraded, false, "Generated base version");
                });
                await Case("JS35 uncommon rarity and block " + up, async () =>
                {
                    var card = await Add(player, "JS35", up);
                    Equal(card.Rarity, CardRarity.Uncommon, "Reward rarity");
                    await Play(card);
                    Equal(player.Creature.Block, up ? 15m : 11m, "Block retained");
                });
                await Case("JK274 block " + up, async () =>
                {
                    await Play(await Add(player, "JK274", up));
                    Equal(player.Creature.Block, up ? 14m : 11m, "Block");
                });
                await Case("U_CARD_008B three Shine, block and draw " + up, async () =>
                {
                    var drawn = await Add(player, "JB02", pile: PileType.Draw);
                    var card = await Add(player, "U_CARD_008B", up);
                    Equal(Shine.GetState(card)?.Maximum, 3, "Shine budget");
                    await Play(card);
                    Equal(player.Creature.Block, up ? 8m : 4m, "Block retained");
                    Equal(drawn.Pile?.Type, PileType.Hand, "One actual card drawn");
                    Equal(Shine.GetState(card)?.Remaining, 2, "One Shine spent");
                });
                await Case("U_CARD_016 cost and rotating single draws " + up, async () =>
                {
                    for (int n = 0; n < 8; n++) await Add(player, "JB02", pile: PileType.Draw);
                    await Play(await Add(player, "U_CARD_016", up));
                    Equal(player.PlayerCombatState!.Energy, up ? 20 : 19, "Paid energy");
                    foreach (string id in new[] { "JK200B", "JA81B", "JS35" })
                    {
                        int before = player.PlayerCombatState.DrawPile.Cards.Count;
                        await Play(await Add(player, id));
                        Equal(before - player.PlayerCombatState.DrawPile.Cards.Count, 1, "One draw for " + id);
                    }
                });
                await Case("JK400 zero-cost bonus " + up, async () =>
                {
                    await Play(await Add(player, "JK400", up));
                    var arrow = await Add(player, "JA81B"); await Play(arrow);
                    decimal bonus = up ? 3 : 2;
                    Equal(player.Creature.GetPower<JunnaPowerJK400>()!.Amount, bonus, "Power amount");
                    var free = await Add(player, "JB02"); await CardActions.FreePlay(Context, free, player.Creature);
                    var reduced = await Add(player, "JB02"); reduced.EnergyCost.SetThisCombat(0); await Play(reduced);
                });
            }
        }
        finally
        {
            Godot.Engine.TimeScale = speed;
            string output = Path.GetFullPath("../../../out/tmp/sync-workbench-20261118/targeted-results.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(new { revision = CardCatalog.Revision, completed = true, assertions, results }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static async Task Prepare(Player player)
    {
        await Reset(player, true);
        await CreatureCmd.SetMaxHp(player.Creature, 200); await CreatureCmd.SetCurrentHp(player.Creature, 200);
        foreach (string id in new[] { "A", "JA43", "JA145", "JK122", "JK133", "U_CARD_008B", "U_CARD_016", "JK400" })
            await Add(player, id, true);
        for (int n = 0; n < 8; n++) await Add(player, "JB02", pile: PileType.Draw);
    }
}
