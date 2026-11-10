using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaCompatibilityProbe;

public sealed class LatestWorkbenchCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_latest_workbench";
    public override string Args => "check | prepare";
    public override string Description => "Check the synchronized workbench changes and prepare their showcase.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Run(player, args.Contains("prepare")), true, "Workbench checks started");

    private static async Task Run(Player player, bool prepare)
    {
        string directory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/card-description-pool-20261110"));
        Directory.CreateDirectory(directory);
        var checks = new List<object>();
        void Save() => File.WriteAllText(Path.Combine(directory, "sync-results.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        void Check(string name, bool pass) { checks.Add(new { name, pass }); Save(); }
        var context = new ThrowingPlayerChoiceContext();
        var enemies = player.Creature.CombatState!.HittableEnemies.ToArray();
        async Task Clear()
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: !prepare); }
            foreach (var enemy in enemies)
            {
                await CreatureCmd.SetMaxHp(enemy, 100000);
                await CreatureCmd.SetCurrentHp(enemy, 100000);
            }
            player.PlayerCombatState!.Energy = 30;
        }
        async Task<CardModel> Add(string id, PileType pile = PileType.Hand, bool upgraded = false)
        {
            using var single = KnowledgeCombat.SingleCard();
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(card, pile, skipVisuals: !prepare);
            return card;
        }
        Task<bool> Play(CardModel card) => CardActions.PaidPlay(context, card, enemies[0]);
        try
        {
            await Clear();
            if (prepare)
            {
                await Add("JK254", upgraded: true);
                await Add("JK11B");
                await Add("JK11", PileType.Draw);
                await Add("JK184");
                await Add("JA82");
                player.PlayerCombatState!.Energy = 10;
                return;
            }
            Check("r4616 catalog includes 108 cards and 22 independent lists", CardCatalog.Revision == 4616 && CardCatalog.Cards.Count == 108 && CardCatalog.Groups.Count == 22);
            foreach (var definition in CardCatalog.Cards)
            {
                foreach (bool upgraded in new[] { false, true })
                {
                    var model = CardCatalog.Create(definition.Id, player);
                    if (upgraded) { model.UpgradeInternal(); model.FinalizeUpgradeInternal(); }
                    bool values = definition.Values.Where(value => model.DynamicVars.ContainsKey(value.Name))
                        .All(value => model.DynamicVars[value.Name].BaseValue == (upgraded ? value.Upgraded : value.Base));
                    // 更近一步 chooses its target after play through the existing choice UI.
                    var target = definition.Id == "JA119" ? TargetType.None : definition.TargetType;
                    Check(definition.Id + " actual model and upgrade " + upgraded,
                        model.Type == definition.CardType && model.TargetType == target && values &&
                        (definition.CostsX ? model.EnergyCost.CostsX : model.EnergyCost.GetWithModifiers(CostModifiers.None) ==
                            (upgraded ? definition.UpgradedCost : definition.Cost)));
                }
            }
            foreach (bool upgraded in new[] { false, true })
            {
                await Clear();
                var defend = await Add("JK274", upgraded: upgraded);
                int block = player.Creature.Block;
                await Play(defend);
                Check("审辩 block " + upgraded, player.Creature.Block == block + (upgraded ? 16 : 12));
                var attack = await Add("JK275", upgraded: upgraded);
                var hp = enemies.Select(enemy => enemy.CurrentHp).ToArray();
                await Play(attack);
                Check("证伪 damages every enemy " + upgraded, enemies.Select((enemy, index) => hp[index] - enemy.CurrentHp).All(damage => damage == (upgraded ? 24 : 18)));
                var speech = await Add("JA35", upgraded: upgraded);
                Check("娓娓道来 ethereal keyword " + upgraded, speech.Keywords.Contains(CardKeyword.Ethereal) == !upgraded);
                await Play(await Add("JA17", upgraded: upgraded));
                Check("破围 reserves two energy " + upgraded, player.Creature.Powers.OfType<EnergyNextTurnPower>().Sum(power => power.Amount) == 2);
            }
            await Clear();
            var targets = new[] { await Add("JB01"), await Add("JB01", PileType.Draw), await Add("JB01", PileType.Discard) };
            var practice = await Add("JA75");
            await Play(practice);
            var deployment = ArrowCombat.Find(practice)!;
            await ArrowCombat.Accelerate(context, deployment, int.MaxValue);
            Check("射箭练习 invests across hand draw and discard", targets.All(card => DamageModifiers.CaptureDamageModifierSnapshot(card).DamageBonus == 3));
            await Clear();
            var waiting = await Add("JA38");
            await Play(waiting);
            var speechPowerCard = await Add("JA35");
            await Play(speechPowerCard);
            var speechPower = player.Creature.Powers.OfType<CatalogPower>().Single(power => power.DesignId == "JA35");
            int turns = ArrowCombat.Find(waiting)!.Remaining;
            await speechPower.BeforeSideTurnEnd(context, CombatSide.Player, [player.Creature]);
            Check("娓娓道来 does not accelerate at turn end", ArrowCombat.Find(waiting)!.Remaining == turns);
            await speechPower.AfterPlayerTurnStart(context, player);
            Check("娓娓道来 accelerates at turn start", ArrowCombat.Find(waiting)!.Remaining == turns - 1);
            foreach (bool upgraded in new[] { false, true })
            {
                await Clear();
                var fusion = await Add("JK254", upgraded: upgraded);
                var argument = await Add("JK11B");
                var recalled = await Add("JK11", PileType.Draw);
                await Play(fusion);
                var fusionPower = player.Creature.Powers.OfType<CatalogPower>().Single(power => power.DesignId == "JK254");
                string description = ((StringVar)fusionPower.DynamicVars["Effects"]).StringValue;
                Check("融会贯通 power description " + upgraded, !description.Contains("ERROR") &&
                    description.Contains(upgraded ? "下次打出耗能变为0" : "也打出"));
                await Play(argument);
                Check("recalled 立论 remains in hand " + upgraded, recalled.Pile?.Type == PileType.Hand);
                Check("融会贯通 leaves a newly recalled partner at its original cost " + upgraded, CardActions.Cost(recalled) == 1);
                if (upgraded)
                {
                    int energy = player.PlayerCombatState!.Energy;
                    await Play(recalled);
                    Check("newly recalled partner pays its original cost", player.PlayerCombatState.Energy == energy - 1);
                    Check("newly recalled partner retains its cost after use", CardActions.Cost(recalled) == 1);
                }
            }
            int count = player.Deck.Cards.Count;
            foreach (string id in new[] { "JK122", "JK274", "JK275" })
                await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model(id), player), PileType.Deck, skipVisuals: true);
            Check("three sources grant exactly 1 1 and 2 shared companions", player.Deck.Cards.Count == count + 7);
            Check("shared companion has no reverse association list", KnowledgeCombat.Group(CardCatalog.Model("JK122B")) == null);
            var source = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
            var offers = new[] { "JK122", "JK274", "JK275" }.Select(id => player.RunState.CreateCard(CardCatalog.Model(id), player)).ToArray();
            count = player.Deck.Cards.Count;
            await DynamicKnowledge.Prepare(context, source, offers.Prepend(source));
            await CardPileCmd.Add(source, PileType.Deck, skipVisuals: true);
            Check("兼收并蓄 takes every other reward once without recursive grants", player.Deck.Cards.Count == count + 4);
            Check("兼收并蓄 preserves each acquired source list", offers.All(card => !DynamicKnowledge.IsLinked(card) && KnowledgeCombat.Group(card) != null));
            Check("associated pool entries remain independently obtainable", offers.All(card => DynamicKnowledge.IsRewardEntrance(player, card)));
            await Clear();
            var singleKnowledge = CardCatalog.Create("JK184", player);
            await CardPileCmd.AddGeneratedCardToCombat(singleKnowledge, PileType.Hand, player);
            Check("single combat generation does not grant six extra companions", player.PlayerCombatState!.Hand.Cards.Count == 1);
            var singing = player.RunState.CreateCard(CardCatalog.Model("JK184"), player);
            count = player.Deck.Cards.Count;
            await CardPileCmd.Add(singing, PileType.Deck, skipVisuals: true);
            Check("acquiring singing still grants all six companions", player.Deck.Cards.Count == count + 7);
            Check("all descriptions omit added parentheses", CardCatalog.Cards.All(definition =>
                !player.RunState.CreateCard(CardCatalog.Model(definition.Id), player).GetDescriptionForPile(PileType.Deck).Contains('（')));
        }
        catch (Exception error) { checks.Add(new { name = "probe exception", pass = false, error = error.ToString() }); Save(); }
    }
}
