using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class StardustInheritanceProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_stardust_inheritance";
    public override string Args => "check <phase> | prepare";
    public override string Description => "Verify Stardust damage investment through two generations.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player, args.ElementAtOrDefault(1) ?? "checks"),
            true, "Stardust fixture started");

    private static async Task Reset(Player player)
    {
        var target = player.Creature.CombatState!.HittableEnemies.First();
        foreach (var creature in new[] { player.Creature, target })
        {
            foreach (var power in creature.Powers.ToArray()) await PowerCmd.Remove(power);
            await TestGameApi.LoseBlock(new ThrowingPlayerChoiceContext(), creature, creature.Block, null);
        }
        foreach (var card in CardActions.All(player).ToArray())
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.RemoveFromCombat(card);
        }
        await CreatureCmd.SetMaxHp(target, 10000);
        await CreatureCmd.SetCurrentHp(target, 10000);
        player.PlayerCombatState!.Energy = 30;
    }

    private static async Task<CardModel> Add(Player player, string id, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player, CardPilePosition.Bottom);
        return card;
    }

    private static async Task Prepare(Player player)
    {
        await Reset(player);
        DamageModifiers.Add(await Add(player, "U_CARD_005"), damageBonus: 7);
        for (int i = 0; i < 6; i++) await Add(player, "JA177");
    }

    private static async Task Check(Player player, string phase)
    {
        var rows = new List<object>();
        void Verify(string name, bool pass, object actual) => rows.Add(new { name, pass, actual });
        var context = new ThrowingPlayerChoiceContext();
        try
        {
            foreach (var (upgraded, bonus, extraHits, strength) in new[]
                { (false, 0m, 0, 0), (false, 7m, 0, 0), (true, 7m, 1, 2) })
            {
                await Reset(player);
                var target = player.Creature.CombatState!.HittableEnemies.First();
                var parent = await Add(player, "U_CARD_005", upgraded);
                DamageModifiers.Add(parent, bonus, extraHits);
                if (strength > 0)
                    await PowerCmd.Apply<StrengthPower>(context, player.Creature, strength, player.Creature, null);
                string tag = $"upgraded={upgraded} bonus={bonus} hits={extraHits} strength={strength}";
                await TestGameApi.PaidPlay(context, parent, target);
                decimal hp = target.CurrentHp;
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(parent)!, 99);
                Verify(tag + " parent damage", hp - target.CurrentHp == ((upgraded ? 12 : 8) + bonus + strength) * (1 + extraHits),
                    hp - target.CurrentHp);
                var children = ArrowCombat.Deployed.ToArray();
                Verify(tag + " two base children retain target and countdown", children.Length == 2 &&
                    children.All(a => !a.Card.IsUpgraded && a.Target == target && a.Remaining == 3), children.Length);
                Verify(tag + " children inherit only damage bonus", children.All(a =>
                    DamageModifiers.CaptureDamageModifierSnapshot(a.Card) is var s && s.DamageBonus == bonus && s.ExtraHits == 0),
                    children.Select(a => DamageModifiers.CaptureDamageModifierSnapshot(a.Card)).Select(s => new { s.DamageBonus, s.ExtraHits }).ToArray());
                var left = children[0];
                var right = children[1];
                DamageModifiers.Add(left.Card, 3);
                Verify(tag + " sibling and parent investment remain independent",
                    DamageModifiers.CaptureDamageModifierSnapshot(right.Card).DamageBonus == bonus &&
                    DamageModifiers.CaptureDamageModifierSnapshot(parent).DamageBonus == bonus, bonus);
                hp = target.CurrentHp;
                await ArrowCombat.Accelerate(context, left, 99);
                Verify(tag + " child damage includes inherited and added bonus", hp - target.CurrentHp == 8 + bonus + 3 + strength,
                    hp - target.CurrentHp);
                var grandchildren = ArrowCombat.Deployed.Where(a => a != right).ToArray();
                Verify(tag + " grandchildren inherit child investment", grandchildren.Length == 2 &&
                    grandchildren.All(a => DamageModifiers.CaptureDamageModifierSnapshot(a.Card).DamageBonus == bonus + 3 &&
                        !a.Card.IsUpgraded && a.Target == target && a.Remaining == 3), grandchildren.Length);
            }
        }
        catch (Exception error) { rows.Add(new { name = "probe exception", pass = false, error = error.ToString() }); }
        var root = Environment.GetEnvironmentVariable("JUNNA_STARDUST_OUTPUT")
            ?? throw new InvalidOperationException("JUNNA_STARDUST_OUTPUT is required");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, phase + ".json"),
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
