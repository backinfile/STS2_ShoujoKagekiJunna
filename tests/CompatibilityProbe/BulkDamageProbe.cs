using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaCompatibilityProbe;

public sealed class BulkDamageCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_bulk_damage";
    public override string Args => "check | prepare";
    public override string Description => "Check status and curse exclusions from group damage investment.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Run(player, args.Contains("prepare")), true, "Bulk damage checks started");
    private static async Task Run(Player player, bool prepare)
    {
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/bulk-damage"));
        Directory.CreateDirectory(directory);
        var rows = new List<object>();
        void Save() => File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        void Expect(string name, object actual, object expected)
        { rows.Add(new { name, actual, expected, pass = Equals(actual, expected) }); Save(); }
        var statusModel = ModelDb.AllCards.First(card => card.Type == CardType.Status && card.Id.Entry == "BURN");
        var curseModel = ModelDb.AllCards.First(card => card.Type == CardType.Curse);
        // A supported adapter also covers damaging status/curse implementations supplied by other mods.
        DamageCapability.Register(statusModel.GetType(), _ => true);
        DamageCapability.Register(curseModel.GetType(), _ => true);
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.First();
        await CreatureCmd.SetMaxHp(target, 10000);
        await CreatureCmd.SetCurrentHp(target, 10000);
        foreach (string id in prepare ? new[] { "JK200B" } : new[] { "bulk", "JK200B", "JK131", "JG36" })
        {
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
            using var single = KnowledgeCombat.SingleCard();
            async Task<CardModel> Add(CardModel canonical, PileType pile)
            {
                var card = player.Creature.CombatState!.CreateCard(canonical, player);
                await CardPileCmd.Add(card, pile);
                return card;
            }
            var attack = await Add(CardCatalog.Model("JB01"), PileType.Hand);
            var status = await Add(statusModel, PileType.Hand);
            var curse = await Add(curseModel, PileType.Hand);
            var converted = await Add(statusModel, PileType.Hand);
            HandArrow.Adapt(converted);
            var drawAttack = await Add(CardCatalog.Model("JB01"), PileType.Draw);
            CardModel? source = id == "bulk" ? null : await Add(CardCatalog.Model(id), PileType.Hand);
            player.PlayerCombatState!.Energy = 20;
            if (prepare) return;
            if (source == null) DamageModifiers.AddMany([attack, status, curse, converted, attack], 3);
            else await CardActions.PaidPlay(context, source, source.TargetType == TargetType.AnyEnemy ? target : null);
            decimal expected = source is CatalogCard catalog ? catalog.Value("Bonus") : 3;
            decimal Bonus(CardModel card) => DamageModifiers.CaptureDamageModifierSnapshot(card).DamageBonus;
            Expect(id + " normal hand attack gains its bonus once", Bonus(attack), expected);
            Expect(id + " status is excluded", Bonus(status), 0m);
            Expect(id + " curse is excluded", Bonus(curse), 0m);
            Expect(id + " converted status arrow is excluded", Bonus(converted), 0m);
            Expect(id + " draw pile follows printed scope", Bonus(drawAttack), id is "JK131" or "JG36" ? expected : 0m);
        }
        Save();
    }
}
