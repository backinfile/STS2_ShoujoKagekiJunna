using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public static class CardPoolProbe
{
    private sealed class Selector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult<IEnumerable<CardModel>>(options.Take(maxSelect).ToArray());
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    public static async Task Run(Player player, bool events = false, string? only = null)
    {
        string report = "D:/Github/STS2_ShoujoKagekiJunna/artifacts/dev-game/" + (only != null ? "card-targeted-results.json" : events ? "card-event-results.json" : "card-pool-results.json");
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var target = state.GetOpponentsOf(player.Creature).First();
        using var selector = CardSelectCmd.UseSelector(new Selector());
        void Save() => File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        void Check(string name, object actual, object expected)
        { rows.Add(new { name, actual, expected, pass = Equals(actual, expected) }); Save(); }
        async Task Reset()
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).ToArray())
            {
                ArrowCombat.Cancel(card);
                await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            }
            foreach (var enemy in state.GetOpponentsOf(player.Creature))
            {
                foreach (var power in enemy.Powers.Where(p => p.Type == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff).ToArray()) await PowerCmd.Remove(power);
                await CreatureCmd.SetMaxHp(enemy, 100000);
                await CreatureCmd.SetCurrentHp(enemy, 100000);
                await CreatureCmd.LoseBlock(context, enemy, enemy.Block, null);
            }
            await CreatureCmd.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState!.Energy = 99;
        }
        async Task<CardModel> Card(string id, PileType pile = PileType.Hand, bool upgraded = false)
        {
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            using var single = KnowledgeCombat.SingleCard();
            await CardPileCmd.Add(card, pile, skipVisuals: true);
            return card;
        }
        Task<bool> Play(CardModel card) => CardActions.PaidPlay(context, card, target);
        async Task Scenario(string name, Func<Task> run)
        {
            if (only != null && only != name) return;
            try { await Reset(); await run(); }
            catch (Exception ex) { rows.Add(new { name, pass = false, error = ex.ToString() }); Save(); }
        }
        if (!events)
        {
        await Scenario("catalog", async () =>
        {
            Check("active card definitions", CardCatalog.Cards.Count, 115);
            var descriptions = new List<object>();
            foreach (var definition in CardCatalog.Cards)
            {
                var card = CardCatalog.Create(definition.Id, player);
                string normal = card.GetDescriptionForPile(PileType.Hand);
                bool capable = DamageModifiers.CanDealDamage(card);
                if (capable != definition.DamageCapable) throw new Exception(definition.Id + " damage eligibility");
                card.UpgradeInternal(); card.FinalizeUpgradeInternal();
                string upgraded = card.GetDescriptionForPile(PileType.Hand);
                if (normal.Contains("ERROR") || upgraded.Contains("ERROR") || normal.Contains("IfUpgraded")) throw new Exception(definition.Id + " localization: " + normal);
                _ = card.HoverTips.ToArray();
                descriptions.Add(new { definition.Id, definition.Name, normal, upgraded, capable });
            }
            File.WriteAllText("D:/Github/STS2_ShoujoKagekiJunna/artifacts/dev-game/card-descriptions.json", JsonSerializer.Serialize(descriptions));
            Check("base and upgraded descriptions rendered", descriptions.Count, 115);
            await Task.CompletedTask;
        });
        await Scenario("arrow immediate and extra damage", async () =>
        {
            await Play(await Card("JA04"));
            var arrow = await Card("JB03"); DamageModifiers.Add(arrow, 2, 1);
            int hp = target.CurrentHp;
            await Play(arrow);
            Check("immediate arrow resolves bonus and extra hit", hp - target.CurrentHp, 54);
            Check("immediate arrow leaves deployment", ArrowCombat.Deployed.Count, 0);
            Check("next-arrow rule consumed", CatalogPower.Has(player, "JA04"), false);
            Check("completed arrow discarded", arrow.Pile!.Type, PileType.Discard);
        });
        await Scenario("zero countdown and return", async () =>
        {
            player.PlayerCombatState!.Energy = 4;
            var arrow = await Card("JA59"); int hp = target.CurrentHp;
            await Play(arrow);
            Check("X arrow spends all energy", player.PlayerCombatState.Energy, 0);
            Check("initial zero completes once", hp - target.CurrentHp, 24);
            player.PlayerCombatState.Energy = 10;
            await Play(await Card("JA22"));
            var second = await Card("JA38"); await Play(second);
            await ArrowCombat.AccelerateAll(context, player, 20);
            Check("completion return rule", second.Pile!.Type, PileType.Hand);
        });
        await Scenario("waiting history", async () =>
        {
            var arrow = await Card("JT03"); await Play(arrow);
            await Play(await Card("JB01")); await Play(await Card("JA38"));
            Check("waiting counts attack and other arrow", ArrowCombat.Find(arrow)!.AttacksWhileWaiting, 2);
            int hp = target.CurrentHp; await ArrowCombat.Accelerate(context, ArrowCombat.Find(arrow)!, 3);
            Check("waiting attack count becomes hits", hp - target.CurrentHp, 20);
        });
        await Scenario("arrow replay redeploy", async () =>
        {
            player.PlayerCombatState!.Energy = 4;
            var arrow = await Card("JA67"); await Play(arrow);
            await ArrowCombat.Accelerate(context, ArrowCombat.Find(arrow)!, 2);
            Check("redeploy pays second cost", player.PlayerCombatState.Energy, 0);
            Check("redeploy keeps one fresh instance", ArrowCombat.Deployed.Count, 1);
            Check("redeploy resets countdown", ArrowCombat.Find(arrow)!.Remaining, 2);
            await ArrowCombat.Accelerate(context, ArrowCombat.Find(arrow)!, 2);
            Check("unaffordable redeploy ends normally", arrow.Pile!.Type, PileType.Discard);
        });
        await Scenario("selection and paid play", async () =>
        {
            player.PlayerCombatState!.Energy = 6;
            var attack = await Card("JK50"); var argument = await Card("JK50B");
            int hp = target.CurrentHp; await Play(argument);
            Check("effect plays selected card for its real cost", player.PlayerCombatState.Energy, 0);
            Check("selected card resolves", hp - target.CurrentHp, 40);
        });
        await Scenario("next non-power play", async () =>
        {
            player.PlayerCombatState!.Energy = 10;
            await Play(await Card("JH19")); await Play(await Card("JH23"));
            Check("next non-power rule skips power", CatalogPower.Has(player, "JH19"), true);
            int hp = target.CurrentHp; await Play(await Card("JB01"));
            Check("next card resolves twice", hp - target.CurrentHp, 12);
            Check("next card pays twice", player.PlayerCombatState.Energy, 6);
            Check("next non-power rule consumed", CatalogPower.Has(player, "JH19"), false);
        });
        await Scenario("knowledge bundles and upgrades", async () =>
        {
            int initial = player.Deck.Cards.Count;
            var acquired = player.RunState.CreateCard(CardCatalog.Model("A"), player);
            await CardPileCmd.Add(acquired, PileType.Deck, skipVisuals: true);
            Check("knowledge acquisition honors configured multiplicity", player.Deck.Cards.Count - initial, 3);
            int before = CardActions.All(player).Count();
            await CardActions.Generate(player, "A", 1, PileType.Hand);
            Check("single generated knowledge stays single", CardActions.All(player).Count() - before, 1);
            var study = await Card("JK82");
            CardCmd.Upgrade(study, CardPreviewStyle.None); await KnowledgeCombat.PendingUpgrades;
            CardCmd.Upgrade(study, CardPreviewStyle.None); await KnowledgeCombat.PendingUpgrades;
            Check("repeat upgrades keep zero cost", CardActions.Cost(study), 0);
            Check("repeat upgrades increase energy", ((CatalogCard)study).Number(0), 4);
            Check("each upgrade creates one new knowledge", CardActions.All(player).Count(c => CardCatalog.DesignId(c) == "JK82B"), 2);
        });
        await Scenario("linked draw", async () =>
        {
            var main = await Card("JK11"); var partner = await Card("JK11B", PileType.Draw); var unrelated = await Card("JB01", PileType.Draw);
            await KnowledgeCombat.DrawPartners(context, main);
            Check("linked draw chooses actual partner", partner.Pile!.Type, PileType.Hand);
            Check("linked draw leaves unrelated card", unrelated.Pile!.Type, PileType.Draw);
        });
        await Scenario("energy reserve", async () =>
        {
            player.PlayerCombatState!.Energy = 1;
            await Play(await Card("JH20", upgraded: true));
            Check("retained gain applied", player.PlayerCombatState.Energy, 3);
            player.PlayerCombatState.ResetEnergy();
            Check("limited energy adds to refresh", player.PlayerCombatState.Energy, player.PlayerCombatState.MaxEnergy + 2);
            await player.Creature.GetPower<EnergyReserve>()!.AfterEnergyReset(player);
            Check("reserve expires after refresh", player.Creature.GetPower<EnergyReserve>() == null, true);
        });
        await Scenario("X cost exchange", async () =>
        {
            player.PlayerCombatState!.Energy = 6;
            var x = await Card("JA59"); var strike = await Card("JB01");
            await Play(await Card("JH01"));
            Check("X becomes fixed numeric cost", x.EnergyCost.CostsX, false);
            Check("X receives other cost", CardActions.Cost(x), 1);
            Check("other card receives available energy after exchange payment", CardActions.Cost(strike), 5);
            await Play(x);
            Check("converted X payload uses paid energy", ArrowCombat.Find(x)!.Remaining, 3);
            Check("fixed X pays fixed amount", player.PlayerCombatState.Energy, 4);
        });
        await Scenario("persistent damage snapshots", async () =>
        {
            var power = await Card("JH30"); DamageModifiers.Add(power, 2, 1); await Play(power);
            int hp = target.CurrentHp; await PlayerCmd.GainEnergy(1, player);
            Check("persistent damage keeps bonus and extra hit", hp - target.CurrentHp, 6);
            DamageModifiers.Add(power, 100);
            hp = target.CurrentHp; await PlayerCmd.GainEnergy(1, player);
            Check("persistent damage snapshot is independent of later card changes", hp - target.CurrentHp, 6);
            hp = target.CurrentHp; await Play(await Card("JB02"));
            Check("spending energy also triggers persistent damage", hp - target.CurrentHp, 6);
        });
        await Scenario("per-turn cap and reset", async () =>
        {
            await Play(await Card("JH23"));
            await Play(await Card("JK10B")); await Play(await Card("JK10B"));
            Check("first expensive card trigger capped", player.Creature.Block, 26);
            player.PlayerCombatState!.IncrementTurnNumber();
            await Play(await Card("JK10B"));
            Check("cap resets with player turn number", player.Creature.Block, 42);
        });
        await Scenario("transform and investment", async () =>
        {
            var card = await Card("JK49"); DamageModifiers.Add(card, 3, 1);
            await Play(card);
            var transformed = CardActions.All(player).Single(c => CardCatalog.DesignId(c) == "JK49B");
            Check("transformed card keeps investment", DamageModifiers.CaptureDamageModifierSnapshot(transformed).DamageBonus, 3m);
            Check("transform keeps single card", CardActions.All(player).Count(), 1);
        });
        await Scenario("conditional cost and conversion", async () =>
        {
            var words = await Card("JK45B"); var arrow = await Card("JK45");
            await Play(arrow); Check("deployed partner makes card free", CardActions.Cost(words), 0);
            await ArrowCombat.AccelerateAll(context, player, 4);
            Check("conditional cost restores after completion", CardActions.Cost(words), 2);
            var full = await Card("JB03"); ArrowConversion.Convert(full);
            Check("converted arrow uses internal attack type", full.Type, CardType.Attack);
            Check("converted arrow costs one more", CardActions.Cost(full), 2);
            int hp = target.CurrentHp; await Play(full);
            Check("converted arrow resolves immediately", hp - target.CurrentHp, 25);
        });
        }
        else
        {
            await Scenario("start turn damage and energy", async () =>
            {
                player.PlayerCombatState!.Energy = 2;
                var card = await Card("JG34"); DamageModifiers.Add(card, 2, 1); await Play(card);
                var power = player.Creature.Powers.OfType<CatalogPower>().Single();
                int hp = target.CurrentHp; await power.AfterPlayerTurnStart(context, player);
                Check("X persistent attack counts captured X plus one extra hit", hp - target.CurrentHp, 15);
            });
            await Scenario("knowledge copy and draw hooks", async () =>
            {
                await Play(await Card("JK15")); var source = await Card("JK33"); DamageModifiers.Add(source, 2); await Play(source);
                var copy = player.PlayerCombatState!.DrawPile.Cards.Single(c => CardCatalog.DesignId(c) == "JK33");
                Check("knowledge copy inherits investment", DamageModifiers.CaptureDamageModifierSnapshot(copy).DamageBonus, 2m);
                Check("knowledge copy creates only selected member", CardActions.All(player).Count(c => CardCatalog.IsKnowledge(c)), 2);
                await Play(await Card("JK27"));
                var partner = await Card("JK33B", PileType.Draw); await CardPileCmd.Draw(context, player);
                Check("drawing knowledge pulls linked actual card", partner.Pile!.Type, PileType.Hand);
                Check("linked draw recursion terminates", player.PlayerCombatState.DrawPile.Cards.Count, 0);
            });
            await Scenario("discovery groups", async () =>
            {
                var selected = CardCatalog.Create("JK11B", player);
                var discovered = await CardSelectCmd.FromChooseACardScreen(context, [selected], player);
                await CardPileCmd.AddGeneratedCardToCombat(discovered!, PileType.Hand, player);
                Check("discovery through companion grants whole group", player.PlayerCombatState!.Hand.Cards.Count, 2);
                Check("discovery includes main", player.PlayerCombatState.Hand.Cards.Any(c => CardCatalog.DesignId(c) == "JK11"), true);
            });
            await Scenario("end turn retention generation and gain", async () =>
            {
                await Play(await Card("JK81")); await Play(await Card("A", upgraded: true)); await Play(await Card("JK67"));
                var costly = await Card("JK10B"); var cheap = await Card("JB01"); var arrow = await Card("JA38"); await Play(arrow);
                foreach (var power in player.Creature.Powers.OfType<CatalogPower>().ToArray())
                {
                    await power.BeforeFlush(context, player);
                    await power.BeforeSideTurnEnd(context, player.Creature.Side, [player.Creature]);
                }
                Check("high-cost retain condition", costly.ShouldRetainThisTurn && !cheap.ShouldRetainThisTurn, true);
                Check("end turn generates upgraded quantity", player.PlayerCombatState!.DrawPile.Cards.Count(c => CardCatalog.DesignId(c) == "B"), 2);
                Check("end turn arrow investment", DamageModifiers.CaptureDamageModifierSnapshot(arrow).DamageBonus, 2m);
            });
            await Scenario("flying arrows", async () =>
            {
                var power = await Card("JZ05"); await Play(power);
                await Play(await Card("JB01"));
                var flight = ArrowCombat.Deployed.Single();
                Check("attack triggers one flying arrow", CardCatalog.DesignId(flight.Card)!, "JA81B");
                Check("flying arrow records actual damage", flight.Card.DynamicVars.Damage.IntValue, 6);
                await Play(await Card("JB01")); Check("flying arrow trigger capped", ArrowCombat.Deployed.Count, 1);
                int hp = target.CurrentHp; await ArrowCombat.AccelerateAll(context, player, 2);
                Check("flying payload damage", hp - target.CurrentHp, 6);
                Check("flying arrow exhausts through normal command", flight.Card.Pile!.Type, PileType.Exhaust);
            });
            await Scenario("turn snapshot and dedup", async () =>
            {
                var hooks = ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>());
                var arrow = await Card("JA38"); await Play(arrow);
                player.PlayerCombatState!.IncrementTurnNumber();
                await hooks.AfterPlayerTurnStartLate(context, player); await hooks.AfterPlayerTurnStartLate(context, player);
                Check("natural countdown once per player turn", ArrowCombat.Find(arrow)!.Remaining, 1);
                var summon = await Card("JA65"); await Play(summon); await Card("JA38", PileType.Draw);
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(summon)!, 2);
                var newborn = ArrowCombat.Deployed.Single(a => a.Card != arrow);
                await hooks.AfterPlayerTurnStartLate(context, player);
                Check("new deployment does not tick on same turn", newborn.Remaining, 2);
            });
            await Scenario("stacked next arrow charges", async () =>
            {
                await Play(await Card("JA04")); await Play(await Card("JA04"));
                await Play(await Card("JA38"));
                Check("stacked next-arrow charges preserve second charge", CatalogPower.Has(player, "JA04"), true);
                await Play(await Card("JA38"));
                Check("stacked next-arrow charges consume on successive arrows", CatalogPower.Has(player, "JA04"), false);
            });
            await Scenario("fixed X copy and energy command", async () =>
            {
                var card = await Card("JA59"); CostChanges.SetFixed(card, 2);
                var copy = card.CreateCloneForPlayer(player);
                Check("fixed X clone preserves numeric cost", CardActions.Cost(copy), 2);
                Check("fixed X clone preserves converted type", copy.EnergyCost.CostsX, false);
                var star = await Card("JH30"); DamageModifiers.Add(star, 2, 1); await Play(star);
                int hp = target.CurrentHp; await PlayerCmd.LoseEnergy(1, player);
                Check("explicit energy loss triggers once", hp - target.CurrentHp, 6);
                hp = target.CurrentHp; await PlayerCmd.GainEnergy(1, player);
                Check("persistent damage after unregistered snapshot", hp - target.CurrentHp, 6);
            });
            await Scenario("converted clones", async () =>
            {
                foreach (var id in new[] { "JB03", "JA41" })
                {
                    var original = await Card(id); ArrowConversion.Convert(original);
                    var clone = original.CreateClone();
                    Check(id + " ordinary clone keeps converted type", clone.Type, CardType.Attack);
                    Check(id + " converted text removes wait", clone.GetDescriptionForPile(PileType.Hand).Contains("回合"), false);
                }
            });
        }
        rows.Add(new { name = "complete", pass = true }); Save();
    }
}
