using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

/// <summary>Exercise every published card face through payment, play, deferred resolution and destination.</summary>
public static class CardCompletionProbe
{
    private sealed class Selector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult<IEnumerable<CardModel>>(options.Take(maxSelect).ToArray());
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }

    public static async Task Run(Player player, bool edges = false, string? only = null)
    {
        string report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", only != null ? "card-recheck-results.json" : edges ? "card-edge-results.json" : "card-face-results.json"));
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var target = state.GetOpponentsOf(player.Creature).First();
        using var selector = CardSelectCmd.UseSelector(new Selector());
        void Save() => File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        string current = "";
        void Check(string label, object actual, object expected)
        {
            rows.Add(new { name = current + ": " + label, actual, expected, pass = Equals(actual, expected) }); Save();
        }
        async Task Reset()
        {
            await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var c in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
            { ArrowCombat.Cancel(c); await CardPileCmd.RemoveFromCombat(c, skipVisuals: true); }
            foreach (var enemy in state.GetOpponentsOf(player.Creature))
            {
                foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
                await CreatureCmd.SetMaxHp(enemy, 1000000); await CreatureCmd.SetCurrentHp(enemy, 1000000);
                await TestGameApi.LoseBlock(context, enemy, enemy.Block, null);
            }
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState.Energy = 10;
            player.PlayerCombatState.IncrementTurnNumber();
        }
        async Task<CardModel> Card(string id, PileType pile = PileType.Hand, bool up = false)
        {
            using var single = KnowledgeCombat.SingleCard();
            var c = CardCatalog.Create(id, player);
            if (up) { c.UpgradeInternal(); c.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(c, pile, skipVisuals: true);
            return c;
        }
        Task<bool> Play(CardModel c) => CardActions.PaidPlay(context, c, target);
        async Task Finish(CardModel c)
        { if (ArrowCombat.Find(c) is { } arrow) await ArrowCombat.Accelerate(context, arrow, 100); }
        async Task Scenario(string name, Func<Task> action)
        {
            if (only != null && !name.StartsWith(only, StringComparison.Ordinal)) return;
            current = name;
            try { await Reset(); await action(); }
            catch (Exception ex) { rows.Add(new { name, pass = false, error = ex.ToString() }); Save(); }
        }
        if (!edges)
        {
            foreach (var definition in CardCatalog.Cards)
            foreach (bool up in new[] { false, true })
            await Scenario(definition.Id + (up ? "+" : ""), async () =>
            {
                string id = definition.Id;
                int N(string name, int fallback = 0) => definition.Values.FirstOrDefault(v => v.Name == name) is { } v ? (up ? v.Upgraded : v.Base) : fallback;
                int baseDamage = N("Damage"), baseBlock = N("Block");
                CardModel? selected = null;
                CardModel? deployed = null;
                var helpers = new List<CardModel>();
                // Build the card's actual target/partner situation, rather than letting selection instructions no-op.
                string? named = id switch
                {
                    "JK11" => "JK11B", "JK11B" => "JK11", "JK32" => "JK32B", "JK32B" => "JK32C", "JK32C" => "JK32",
                    "JK33" => "JK33B", "JK33B" => "JK33", "JK43B" => "JK43", "JK50B" => "JK50", "JK73B" => "JK73",
                    "JK74" => "JK74B", "JK74B" => "JK74", _ => null
                };
                if (named != null)
                {
                    PileType pile = id is "JK11" or "JK11B" ? PileType.Exhaust : id == "JK43B" ? PileType.Draw : PileType.Hand;
                    selected = await Card(named, pile); helpers.Add(selected);
                }
                if (id is "JG03" or "JG09" or "JG16" or "JG27" or "JG36" or "JA75" or "JH01" or "JH29" or "JK81B")
                { selected = await Card("JB01"); helpers.Add(selected); helpers.Add(await Card("JH27")); }
                if (id == "JG16") DamageModifiers.Add(helpers[0], 3);
                if (id == "JG16") { var donor = await Card("JA38", PileType.Discard); DamageModifiers.Add(donor, 4); helpers.Add(donor); }
                if (id == "JT02") { selected = await Card("JA38"); helpers.Add(selected); }
                if (id is "JK05" or "JK53" or "JK48" or "JK48B")
                {
                    selected = await Card("JK11"); helpers.Add(selected);
                    helpers.Add(await Card("JK11B", PileType.Draw)); helpers.Add(await Card("JK15B", PileType.Draw));
                }
                if (id is "JA65" or "JA53") { selected = await Card("JA38", PileType.Draw); helpers.Add(selected); }
                if (id == "JA66") { selected = await Card("JB01", PileType.Discard); helpers.Add(selected); helpers.Add(await Card("JB02", PileType.Discard)); }
                if (id is "JK78" or "JK78D") { selected = await Card("JB01", PileType.Draw); helpers.Add(selected); helpers.Add(await Card("JB01", PileType.Discard)); }
                if (id == "JK78E") { selected = await Card("JB01", PileType.Discard); helpers.Add(selected); }
                if (id == "JH31") for (int i = 0; i < 4; i++) helpers.Add(await Card("JA38", PileType.Draw));
                if (id is "JA15" or "JF07" or "JK82B" or "JA57" or "B")
                    for (int i = 0; i < 5; i++) helpers.Add(await Card("JB01", PileType.Draw));
                if (id is "JA43" or "JA71" or "JK45B" or "JH22" or "JX01" or "JX02" or "JA73")
                {
                    deployed = await Card("JB03"); deployed.DynamicVars.Damage.BaseValue = 0;
                    await Play(deployed); helpers.Add(deployed);
                }
                if (id == "JF10")
                {
                    await PowerCmd.Apply<WeakPower>(context, player.Creature, 1, target, null);
                    await PowerCmd.Apply<VulnerablePower>(context, player.Creature, 1, target, null);
                }
                var card = await Card(id, up: up);
                if (id == "JA81B") { ((CatalogCard)card).SetGeneratedDamage(13); baseDamage = 13; }
                if (definition.CostsX) player.PlayerCombatState!.Energy = 3;
                if (id == "JA67") player.PlayerCombatState!.Energy = 2;
                int energyBefore = player.PlayerCombatState!.Energy;
                int hp = target.CurrentHp;
                int expectedDamage = baseDamage * N("Repeat", 1);
                int expectedBlock = baseBlock;
                if (definition.Type == "能力") expectedBlock = 0;
                if (id is "JG34" or "JH30" or "JA82") expectedDamage = 0;
                if (id == "JK48") expectedDamage += player.Deck.Cards.Count(CardCatalog.IsKnowledge);
                if (id == "JK48B") expectedBlock += player.Deck.Cards.Count(CardCatalog.IsKnowledge);
                if (id == "JH13") expectedDamage *= 1 + (energyBefore - (up ? definition.UpgradedCost : definition.Cost)) / 2;
                if (id == "JK74") expectedDamage *= 2;
                if (id == "JK74B") expectedBlock *= 2;
                if (id == "JK81B") expectedBlock += helpers.Count * N("N1");
                if (id == "JK50B") expectedDamage = 40;
                Check("printed cost", card.EnergyCost.GetWithModifiers(CostModifiers.None), up ? definition.UpgradedCost : definition.Cost);
                Check("expected arrow type", CardCatalog.IsArrow(card), definition.Type is "箭矢" or "星矢");
                Check("paid play", await Play(card), true);
                if ((definition.Type is "箭矢" or "星矢") && Math.Max(0, N("Turns") - (id == "JA59" ? 3 : 0)) > 0)
                    Check("waiting deployment exists", ArrowCombat.Find(card) != null, true);
                if ((definition.Type is "箭矢" or "星矢") && ArrowCombat.Find(card) is { } waiting)
                {
                    Check("initial countdown", waiting.Remaining, Math.Max(0, N("Turns") - (id == "JA59" ? 3 : 0)));
                    Check("damage waits for completion", hp - target.CurrentHp, 0);
                    await Finish(card);
                }
                Check("damage", hp - target.CurrentHp, expectedDamage);
                Check("block", player.Creature.Block, expectedBlock);
                if (definition.Type == "能力")
                    Check("installed ability", player.Creature.Powers.Any(p => p is CatalogPower power && power.DesignId == id || id == "JG23" && p is JunnaEndurancePower), true);
                else
                    Check("destination", card.Pile?.Type ?? PileType.None, (up ? definition.UpgradedExhaust : definition.Exhaust) ? PileType.Exhaust : id == "JA67" ? ArrowPile.PileId : id == "JG33" ? PileType.Draw : PileType.Discard);
                switch (id)
                {
                    case "JG03": Check("selected extra hit", DamageModifiers.CaptureDamageModifierSnapshot(selected!).ExtraHits, 1); break;
                    case "JG09": case "JG27": case "JG36": case "JK78": case "JK78D": case "JK78E": case "JA75":
                        Check("selected bonus", DamageModifiers.CaptureDamageModifierSnapshot(selected!).DamageBonus, (decimal)N("Bonus")); break;
                    case "JG16": Check("transfer total", DamageModifiers.CaptureDamageModifierSnapshot(selected!).DamageBonus, 7m); break;
                    case "JT02": Check("converted type", selected!.Type, ArrowPile.ArrowType); Check("converted cost", CardActions.Cost(selected!), 0); Check("adapted waiting turns", HandArrow.Turns(selected!), 5); break;
                    case "JK33": Check("partner bonus", DamageModifiers.CaptureDamageModifierSnapshot(selected!).DamageBonus, 6m); break;
                    case "JK33B": Check("partner extra hit", DamageModifiers.CaptureDamageModifierSnapshot(selected!).ExtraHits, 1); break;
                    case "JK32": case "JK32B": case "JK32C": Check("partner free", CardActions.Cost(selected!), 0); break;
                    case "JK11": case "JK11B": case "JK43B": case "JA53": case "JA66":
                        Check("selected card in hand", selected!.Pile!.Type, PileType.Hand); break;
                    case "JK53": Check("linked card drawn", helpers[1].Pile!.Type, PileType.Hand); break;
                    case "JK05": Check("knowledge drawn", player.PlayerCombatState.Hand.Cards.Count, 3); break;
                    case "JA15": case "JF07": case "JK82B": case "JA57": case "B": case "JH31":
                        Check("draw quantity", player.PlayerCombatState.Hand.Cards.Count, N("N0")); break;
                    case "JX02": Check("energy from deployment", player.PlayerCombatState.Energy, energyBefore - definition.Cost + 1); break;
                    case "JH20": case "JA80": case "JK82": Check("energy gain", player.PlayerCombatState.Energy, energyBefore - definition.Cost + N("N0")); break;
                    case "JX07": case "JH11": case "JH17": Check("energy gain", player.PlayerCombatState.Energy, energyBefore - definition.Cost + N("N1")); break;
                    case "JH29": Check("energy total", player.PlayerCombatState.Energy, energyBefore - (up ? definition.UpgradedCost : definition.Cost) + 4); break;
                    case "JH01": Check("first swapped cost", CardActions.Cost(helpers[0]), 3); Check("second swapped cost", CardActions.Cost(helpers[1]), 1); break;
                    case "JA65":
                        Check("selected arrow deployed", ArrowCombat.Find(selected!) != null, true);
                        Check("selected arrow plays free", player.PlayerCombatState.Energy, energyBefore - (up ? definition.UpgradedCost : definition.Cost)); break;
                    case "JK73B": Check("selected arrow deployed", ArrowCombat.Find(selected!) != null, true); break;
                    case "JA81": case "JA76": case "JA82":
                        var flying = ArrowCombat.Deployed.Where(a => CardCatalog.DesignId(a.Card) == "JA81B").ToArray();
                        Check("flying count", flying.Length, id == "JA82" ? 3 : id == "JA76" ? state.GetOpponentsOf(player.Creature).Count() : 1);
                        Check("flying upgrade", flying.All(a => a.Card.IsUpgraded == (id != "JA76" && up) && a.Remaining == (id != "JA76" && up ? 1 : 2)), true);
                        Check("flying damage", flying.All(a => a.Card.DynamicVars.Damage.IntValue == baseDamage), true); break;
                    case "JA43": case "JK45B": Check("accelerated countdown", ArrowCombat.Find(deployed!)!.Remaining, 2); break;
                    case "JA71": Check("all arrows completed", ArrowCombat.Deployed.Count, 0); break;
                    case "JH22": Check("damage invested into arrow", DamageModifiers.CaptureDamageModifierSnapshot(deployed!).DamageBonus, (decimal)baseDamage); break;
                    case "JX01": Check("arrow bonus", DamageModifiers.CaptureDamageModifierSnapshot(deployed!).DamageBonus, (decimal)N("Bonus")); break;
                    case "JF10": case "JA58":
                        Check("weak", target.GetPower<WeakPower>()!.Amount, N("N0"));
                        Check("vulnerable", target.GetPower<VulnerablePower>()!.Amount, N("N0"));
                        if (id == "JF10") Check("self debuffs cleared", player.Creature.Powers.Count, 0); break;
                    case "JA16": case "JA17": Check("next turn energy", player.Creature.GetPower<EnergyNextTurnPower>()!.Amount, id == "JA16" ? 1 : 2); break;
                    case "JK49": case "JK49B":
                        Check("independent knowledge identity", CardCatalog.DesignId(card)!, id);
                        Check("independent knowledge cost", CardActions.Cost(card), id == "JK49" ? 2 : 0); break;
                }
                rows.Add(new { name = current + ": complete face", pass = true }); Save();
            });
        }
        else
        {
            await Scenario("card text", async () =>
            {
                var descriptions = new List<object>();
                foreach (var definition in CardCatalog.Cards)
                {
                    var card = CardCatalog.Create(definition.Id, player);
                    string normal = card.GetDescriptionForPile(PileType.Hand);
                    string preview = card.GetDescriptionForUpgradePreview();
                    card.UpgradeInternal(); card.FinalizeUpgradeInternal();
                    string upgraded = card.GetDescriptionForPile(PileType.Hand);
                    foreach (string text in new[] { normal, preview, upgraded })
                        if (text.Contains("ERROR") || text.Contains('{') || text.Contains('}'))
                            throw new Exception(definition.Id + " localization: " + text);
                    descriptions.Add(new { definition.Id, definition.Name, normal, preview, upgraded });
                }
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(report)!, "card-descriptions.json"), JsonSerializer.Serialize(descriptions));
                Check("base, upgrade preview and upgraded descriptions", descriptions.Count, 117);
                await Task.CompletedTask;
            });
            foreach (bool up in new[] { false, true })
            foreach (bool blockDraw in new[] { false, true })
            await Scenario("independent cost reduction" + (up ? "+" : "") + (blockDraw ? " blocked draw" : " no match"), async () =>
            {
                var hand = await Card("JK43");
                var discard = await Card("JK43", PileType.Discard);
                var exhausted = await Card("JK43", PileType.Exhaust);
                CardModel? draw = blockDraw ? await Card("JK43", PileType.Draw) : null;
                if (blockDraw) await PowerCmd.Apply<NoDrawPower>(context, player.Creature, 1, player.Creature, null);
                Check("paid play", await Play(await Card("JK43B", up: up)), true);
                Check("hand cost", CardActions.Cost(hand), up ? 2 : 3);
                Check("discard cost", CardActions.Cost(discard), up ? 2 : 3);
                Check("exhaust cost", CardActions.Cost(exhausted), up ? 2 : 3);
                if (draw != null)
                {
                    Check("blocked card stays in draw", draw.Pile!.Type, PileType.Draw);
                    Check("draw pile cost", CardActions.Cost(draw), up ? 2 : 3);
                }
            });
            await Scenario("attack event classification", async () =>
            {
                await Play(await Card("JZ05"));
                await TestGameApi.Damage(context, target, 2, ValueProp.Unpowered, player.Creature);
                Check("non-attack damage keeps readiness", ArrowCombat.Deployed.Count, 0);
                var foreignSkill = state.CreateCard(ModelDb.Card<ExternalDirectCard>(), player);
                await CardPileCmd.Add(foreignSkill, PileType.Hand);
                await Play(foreignSkill);
                Check("external skill attack damage triggers", ArrowCombat.Deployed.Count, 1);
                Check("external attack damage captured", ArrowCombat.Deployed.Single().Card.DynamicVars.Damage.IntValue, 4);
            });
            await Scenario("independent knowledge replay and identity", async () =>
            {
                var card = await Card("JK49", up: true);
                card.BaseReplayCount = 1; DamageModifiers.Add(card, 3, 1); card.EnergyCost.AddThisCombat(-1);
                int hp = target.CurrentHp; await Play(card);
                Check("replay retains attacking form until complete", hp - target.CurrentHp, 124);
                Check("same instance survives", ReferenceEquals(CardActions.All(player).Single(), card), true);
                Check("attack identity retained", CardCatalog.DesignId(card)!, "JK49");
                Check("investment retained", DamageModifiers.CaptureDamageModifierSnapshot(card).DamageBonus, 3m);
                var defense = await Card("JK49B", up: true);
                defense.BaseReplayCount = 1;
                await Play(defense);
                Check("independent block replay", player.Creature.Block, 12);
                Check("defense identity retained", CardCatalog.DesignId(defense)!, "JK49B");
                Check("attack form restored", card.Type, CardType.Attack);
                Check("attack target restored", card.TargetType, TargetType.AnyEnemy);
                Check("cost modifier retained", CardActions.Cost(card), 1);
                Check("restored form accepts investment", DamageModifiers.Add(card, 1), true);
                var clone = card.CreateCloneForPlayer(player);
                Check("clone keeps knowledge identity", CardCatalog.DesignId(clone)!, "JK49");
            });
            await Scenario("exhaust precedes arrow return", async () =>
            {
                await Play(await Card("JA22")); var arrow = await Card("JA81B"); await Play(arrow); await Finish(arrow);
                Check("exhausted token", arrow.Pile!.Type, PileType.Exhaust);
            });
            await Scenario("fixed cost aggregation", async () =>
            {
                await Card("JA59"); await Card("JH27"); await Play(await Card("JH29"));
                Check("X excluded from total", player.PlayerCombatState!.Energy, 12);
                await Play(await Card("JZ06")); player.PlayerCombatState.Energy = 10;
                await player.Creature.Powers.OfType<CatalogPower>().Single().AfterPlayerTurnStart(context, player);
                Check("X excluded from maximum", player.PlayerCombatState.Energy, 13);
            });
            await Scenario("energy setting has no gain event", async () =>
            {
                await Play(await Card("JH30")); int hp = target.CurrentHp;
                await PlayerCmd.SetEnergy(30, player); await PlayerCmd.SetEnergy(1, player);
                Check("energy set bypasses event", hp - target.CurrentHp, 0);
                await PlayerCmd.GainEnergy(1, player); await PlayerCmd.LoseEnergy(1, player);
                Check("actual gains and spending remain events", hp - target.CurrentHp, 2);
            });
            await Scenario("energy damage keeps investment snapshot", async () =>
            {
                var star = await Card("JH30"); DamageModifiers.Add(star, 2, 1); await Play(star);
                int hp = target.CurrentHp; await PlayerCmd.GainEnergy(3, player);
                Check("amount with bonus and extra hit", hp - target.CurrentHp, 10);
                DamageModifiers.Add(star, 100);
                hp = target.CurrentHp; await PlayerCmd.GainEnergy(2, player);
                Check("later investment preserves installed snapshot", hp - target.CurrentHp, 8);
            });
            await Scenario("ambush free play preserves card", async () =>
            {
                await Play(await Card("JH30")); await Play(await Card("JA45"));
                var ambush = await Card("JA65"); await Play(ambush);
                var selected = await Card("JK37", PileType.Draw, up: true);
                CostChanges.SetFixed(selected, 4); DamageModifiers.Add(selected, 2, 1);
                player.PlayerCombatState!.Energy = 0; int hp = target.CurrentHp;
                await Finish(ambush);
                Check("zero energy still deploys", ArrowCombat.Find(selected) != null, true);
                Check("no energy consumed", player.PlayerCombatState.Energy, 0);
                Check("no energy damage event", hp - target.CurrentHp, 0);
                Check("modified cost preserved", CardActions.Cost(selected), 4);
                Check("upgrade preserved", selected.IsUpgraded, true);
                Check("free play counts toward arrow trigger", ArrowCombat.Find(selected)!.Remaining, selected.DynamicVars["Turns"].IntValue - 1);
                await Finish(selected);
                Check("damage investment preserved", hp - target.CurrentHp, ((int)selected.DynamicVars.Damage.BaseValue + 2) * 2);
            });
            await Scenario("ambush X free play", async () =>
            {
                var ambush = await Card("JA65"); await Play(ambush);
                var selected = await Card("JA59", PileType.Draw);
                player.PlayerCombatState!.Energy = 3; await Finish(ambush);
                Check("native free X captures available energy", selected.EnergyCost.CapturedXValue, 3);
                Check("X does not consume energy", player.PlayerCombatState.Energy, 3);
                Check("X cost preserved", selected.EnergyCost.CostsX, true);
                Check("X adjusts initial countdown", ArrowCombat.Find(selected)!.Remaining, selected.DynamicVars["Turns"].IntValue - 3);
            });
            foreach (bool up in new[] { false, true })
            await Scenario("argument free play" + (up ? "+" : ""), async () =>
            {
                await Play(await Card("JK15"));
                var selected = await Card("JK50", up: up);
                var argument = await Card("JK50B", up: up);
                player.PlayerCombatState!.Energy = 2;
                int hp = target.CurrentHp;
                Check("argument paid normally", await Play(argument), true);
                Check("only argument energy consumed", player.PlayerCombatState.Energy, 0);
                Check("target damage resolves without payable energy", hp - target.CurrentHp, up ? 52 : 40);
                Check("target printed cost preserved", CardActions.Cost(selected), 4);
                Check("target upgrade preserved", selected.IsUpgraded, up);
                Check("both knowledge plays count", player.PlayerCombatState.DrawPile.Cards.Count, 2);
            });
            await Scenario("ambush respects play restriction", async () =>
            {
                var ambush = await Card("JA65"); await Play(ambush);
                var selected = await Card("JA38", PileType.Draw); selected.AddKeyword(CardKeyword.Unplayable);
                await Finish(ambush);
                Check("unplayable arrow stays in draw pile", selected.Pile!.Type, PileType.Draw);
                Check("unplayable arrow is not deployed", ArrowCombat.Find(selected) == null, true);
            });
            await Scenario("attack history before installing trigger", async () =>
            {
                await Play(await Card("JB01")); await Play(await Card("JZ05")); await Play(await Card("JB01"));
                Check("installation keeps first attack history", ArrowCombat.Deployed.Count, 0);
                player.PlayerCombatState!.IncrementTurnNumber(); await Play(await Card("JB01"));
                Check("next turn resets history", ArrowCombat.Deployed.Count, 1);
            });
            await Scenario("upgraded trigger first two damage events", async () =>
            {
                await Play(await Card("JZ05", up: true)); await Play(await Card("JH10"));
                Check("two events from multihit attack", ArrowCombat.Deployed.Count, 2);
                Check("both tokens upgraded", ArrowCombat.Deployed.All(a => a.Remaining == 1 && a.Card.IsUpgraded), true);
            });
            await Scenario("temporary replay and native replay event counts", async () =>
            {
                await Play(await Card("JH23", up: true)); var attack = await Card("JK50"); attack.BaseReplayCount = 1;
                await Play(attack); Check("native replay counts one play", player.Creature.Block, 9);
                await Play(await Card("JH19")); player.PlayerCombatState!.Energy = 10;
                var arrow = await Card("JA38"); await Play(arrow);
                Check("paid arrow replay one deployment", ArrowCombat.Deployed.Count, 1);
                Check("paid arrow replay two executions", ArrowCombat.Find(arrow)!.PlayCount, 2);
                int hp = target.CurrentHp; await Finish(arrow); Check("paid arrow replay damage", hp - target.CurrentHp, 24);
            });
            foreach (string id in "JA04 JA22 JK10 JK15 A JK27 JA35 JK32 JK37C JA45 JK64 JK67 JG31 JH19 JH21 JG34 JK75 JH23 JA70 JH24 JK80 JK81 JZ05 JZ06 JH30 JK46 JG23".Split(' '))
            foreach (bool up in new[] { false, true })
            await Scenario("event " + id + (up ? "+" : ""), async () =>
            {
                int maxEnergyBefore = player.PlayerCombatState!.MaxEnergy, permanentMaxEnergy = player.MaxEnergy;
                player.PlayerCombatState.Energy = id == "JG34" ? 2 : 50;
                var source = await Card(id, up: up);
                if (id == "JG34") DamageModifiers.Add(source, 2, 1);
                await Play(source);
                var power = player.Creature.Powers.OfType<CatalogPower>().SingleOrDefault();
                async Task Start() { player.PlayerCombatState.IncrementTurnNumber(); await power!.AfterPlayerTurnStart(context, player); }
                Task End() => power!.BeforeSideTurnEnd(context, player.Creature.Side, [player.Creature]);
                switch (id)
                {
                    case "JA04":
                        var fast1 = await Card("JA38"); await Play(fast1);
                        var fast2 = await Card("JA38"); await Play(fast2);
                        Check("first completes", fast1.Pile!.Type, PileType.Discard);
                        Check("second charge", fast2.Pile!.Type, up ? PileType.Discard : ArrowPile.PileId); break;
                    case "JA22":
                        var returning = await Card("JA38"); await Play(returning); await Finish(returning);
                        Check("arrow returns", returning.Pile!.Type, PileType.Hand); break;
                    case "JK10":
                        for (int i = 0; i < 3; i++) await Card("JB01", PileType.Draw);
                        int before = player.PlayerCombatState.Energy;
                        for (int i = 0; i < 3; i++) await Play(await Card("JK50"));
                        Check("draw cap", player.PlayerCombatState.Hand.Cards.Count, up ? 2 : 1);
                        Check("energy cap", player.PlayerCombatState.Energy, before - 12 + (up ? 2 : 1)); break;
                    case "JK15":
                        for (int i = 0; i < 3; i++) await Play(await Card("JK15B"));
                        Check("copy cap", player.PlayerCombatState.DrawPile.Cards.Count, up ? 3 : 2);
                        player.PlayerCombatState.IncrementTurnNumber();
                        for (int i = 0; i < 3; i++) await Play(await Card("JK15B"));
                        Check("next turn restores trigger allowance", player.PlayerCombatState.DrawPile.Cards.Count, up ? 6 : 4); break;
                    case "A": await End(); Check("end generation", player.PlayerCombatState.DrawPile.Cards.Count, up ? 2 : 1); break;
                    case "JK27":
                        await Card("JK11", PileType.Draw); await Card("JK11B", PileType.Draw);
                        await CardPileCmd.Draw(context, player);
                        Check("linked draw", player.PlayerCombatState.Hand.Cards.Count, 2); break;
                    case "JA35":
                        var longArrow = await Card("JK37"); await Play(longArrow); await Start();
                        Check("start acceleration", ArrowCombat.Find(longArrow)!.Remaining, up ? 8 : 9);
                        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).AfterPlayerTurnStartLate(context, player);
                        Check("natural countdown additional", ArrowCombat.Find(longArrow)!.Remaining, up ? 7 : 8); break;
                    case "JK32":
                        for (int i = 0; i < 3; i++) await Card("JB01", PileType.Draw);
                        await Start(); Check("start draw", player.PlayerCombatState.Hand.Cards.Count, up ? 2 : 1); break;
                    case "JK37C": case "JK64":
                        var accelerated = await Card("JK37"); await Play(accelerated);
                        int remaining = ArrowCombat.Find(accelerated)!.Remaining;
                        await Play(await Card("JK15B"));
                        Check("knowledge/zero play acceleration", ArrowCombat.Find(accelerated)!.Remaining, remaining - 1);
                        if (id == "JK64")
                        {
                            await power!.AfterSideTurnEnd(context, player.Creature.Side, [player.Creature]);
                            Check("turn listener expires", CatalogPower.Has(player, id), false);
                        }
                        break;
                    case "JA45":
                        var arrows = new List<CardModel>();
                        for (int i = 0; i < 3; i++) { var arrow = await Card("JA38"); arrows.Add(arrow); await Play(arrow); }
                        Check("first accelerated", ArrowCombat.Find(arrows[0])!.Remaining, 1);
                        Check("third allowance", ArrowCombat.Find(arrows[2])!.Remaining, up ? 1 : 2); break;
                    case "JK67":
                        var handArrow = await Card("JA38"); var exhaustedArrow = await Card("JA38", PileType.Exhaust);
                        await End();
                        Check("hand arrow investment", DamageModifiers.CaptureDamageModifierSnapshot(handArrow).DamageBonus, up ? 3m : 2m);
                        Check("exhaust arrow investment", DamageModifiers.CaptureDamageModifierSnapshot(exhaustedArrow).DamageBonus, up ? 3m : 2m); break;
                    case "JG31":
                        var practiced = await Card("JB01"); int hp = target.CurrentHp;
                        await Play(practiced); Check("first damage before gain", hp - target.CurrentHp, 6);
                        await CardPileCmd.Add(practiced, PileType.Hand); hp = target.CurrentHp; await Play(practiced);
                        Check("next damage uses gain", hp - target.CurrentHp, up ? 9 : 8);
                        await power!.AfterSideTurnEnd(context, player.Creature.Side, [player.Creature]);
                        Check("turn listener expires", CatalogPower.Has(player, id), false); break;
                    case "JH19":
                        player.PlayerCombatState.Energy = 2; int replayHp = target.CurrentHp;
                        await Play(await Card("JB01"));
                        Check("native replay damage", replayHp - target.CurrentHp, 12);
                        Check("paid once for native replay", player.PlayerCombatState.Energy, 1);
                        Check("readiness consumed", CatalogPower.Has(player, id), false); break;
                    case "JH21":
                        player.PlayerCombatState.Energy = 0; await End(); Check("empty energy gives no block", player.Creature.Block, 0);
                        player.PlayerCombatState.Energy = 1; await End(); Check("leftover energy block", player.Creature.Block, up ? 7 : 5); break;
                    case "JG34":
                        int chorusHp = target.CurrentHp; await Start(); Check("captured X and investment", chorusHp - target.CurrentHp, up ? 18 : 15); break;
                    case "JK75":
                        var randomTarget = await Card("JB01"); await Play(await Card("JK15B"));
                        Check("random eligible hand investment", DamageModifiers.CaptureDamageModifierSnapshot(randomTarget).DamageBonus, up ? 2m : 1m); break;
                    case "JH23":
                        await Play(await Card("JK50")); await Play(await Card("JK50"));
                        Check("one expensive trigger", player.Creature.Block, up ? 9 : 6); break;
                    case "JA70":
                        await Play(await Card("JA38")); await Play(await Card("JA38"));
                        Check("two arrow play blocks", player.Creature.Block, up ? 6 : 4); break;
                    case "JH24":
                        Check("combat maximum increases immediately", player.PlayerCombatState.MaxEnergy, maxEnergyBefore + 1);
                        Check("permanent maximum unchanged", player.MaxEnergy, permanentMaxEnergy);
                        Check("no installed buff", CatalogPower.Has(player, id), false);
                        Check("block preserved", player.Creature.Block, up ? 32 : 24);
                        Check("no immediate energy grant", player.PlayerCombatState.Energy, 46);
                        player.PlayerCombatState.IncrementTurnNumber(); player.PlayerCombatState.ResetEnergy();
                        Check("next turn refresh uses increased maximum", player.PlayerCombatState.Energy, maxEnergyBefore + 1);
                        break;
                    case "JK80":
                        await Play(await Card("JK15B")); await Play(await Card("JK15B"));
                        Check("only next knowledge block", player.Creature.Block, up ? 30 : 26); break;
                    case "JK81":
                        var expensive = await Card("JK50"); var cheap = await Card("JB01");
                        await power!.BeforeFlush(context, player);
                        Check("cost selects retained cards", expensive.ShouldRetainThisTurn && !cheap.ShouldRetainThisTurn, true); break;
                    case "JZ05":
                        await Play(await Card("JH10"));
                        Check("first damage events", ArrowCombat.Deployed.Count, up ? 2 : 1); break;
                    case "JZ06":
                        await Card("JA59"); await Card("JH27"); int starting = player.PlayerCombatState.Energy;
                        await Start(); Check("numeric maximum energy", player.PlayerCombatState.Energy, starting + 3); break;
                    case "JH30":
                        Check("description formats equal damage", source.GetDescriptionForPile(PileType.Hand).Contains("等量伤害"), true);
                        int starHp = target.CurrentHp; await PlayerCmd.GainEnergy(0, player);
                        Check("zero energy change skips damage", starHp - target.CurrentHp, 0);
                        await PlayerCmd.GainEnergy(3, player); await PlayerCmd.LoseEnergy(2, player);
                        Check("damage scales with actual energy change", starHp - target.CurrentHp, 5);
                        var costly = await Card("JB02"); CostChanges.SetFixed(costly, 4); await Play(costly);
                        Check("payment damage uses spent amount", starHp - target.CurrentHp, 9);
                        player.PlayerCombatState.Energy = 1; await PlayerCmd.LoseEnergy(9, player);
                        Check("energy loss damage uses actual loss", starHp - target.CurrentHp, 10); break;
                    case "JK46":
                        await Start(); var generated = player.PlayerCombatState.Hand.Cards.Single();
                        Check("generated knowledge", CardCatalog.DesignId(generated)!, "JK46B");
                        Check("generated upgrade", generated.IsUpgraded, up); break;
                    case "JG23":
                        var trained = await Card("JB01"); DamageModifiers.Add(trained, 12); int trainedHp = target.CurrentHp; await Play(trained);
                        Check("bonus thresholds add hits", trainedHp - target.CurrentHp, 54); break;
                }
            });
        }
        rows.Add(new { name = "complete", pass = true }); Save();
    }
}
