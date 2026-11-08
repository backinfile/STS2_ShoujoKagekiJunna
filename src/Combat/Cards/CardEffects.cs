using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Explicit implementations for the versioned catalog. Card text is never interpreted at runtime.</summary>
public static class CardEffects
{
    public static async Task Resolve(CatalogCard card, PlayerChoiceContext context, CardPlay play)
    {
        var player = card.Owner;
        var hand = player.PlayerCombatState!.Hand;
        int V(string name, int fallback = 0) => card.Value(name, fallback);
        Task Block(int? amount = null) => CreatureCmd.GainBlock(player.Creature, amount ?? V("Block"), ValueProp.Move, play);
        Task Draw(int count) => CardPileCmd.Draw(context, count, player);
        Task Energy(int amount) => PlayerCmd.GainEnergy(amount, player);
        Task<CardModel[]> Choose(IEnumerable<CardModel> cards, int count = 1, bool optional = false) => CardActions.Choose(context, card, cards, count, optional);
        IEnumerable<CardModel> All() => CardActions.All(player);
        IEnumerable<CardModel> Arrows() => All().Where(CardCatalog.IsArrow);
        async Task Improve(IEnumerable<CardModel> cards, int bonus, int extraHits = 0)
        {
            foreach (var chosen in cards.ToArray()) DamageModifiers.Add(chosen, bonus, extraHits);
            await Task.CompletedTask;
        }
        async Task Attack(int? damage = null, int? hits = null)
            => await DealDamage(card, context, play, damage ?? V("Damage"), hits ?? V("Repeat", 1));
        async Task ImproveSelected(IEnumerable<CardModel> cards, int bonus, int count = 1, bool optional = false)
            => await Improve(await Choose(cards.Where(DamageModifiers.CanDealDamage), count, optional), bonus);
        async Task RetainedEnergy(int amount)
        {
            int before = player.PlayerCombatState.Energy;
            await Energy(amount);
            await EnergyReserve.Add(context, card, Math.Max(0, player.PlayerCombatState.Energy - before));
        }

        switch (card.DesignId)
        {
            // Installed rules keep an independent snapshot and per-turn counters for every application.
            case "JA04": case "JK10": case "JA22": case "JK15": case "A": case "JK27":
            case "JA35": case "JK32": case "JK37C": case "JA45": case "JK64": case "JK67":
            case "JG31": case "JH21": case "JG34": case "JK75": case "JH23": case "JA70":
            case "JH59": case "JA144": case "JA118": case "JA105": case "JK127": case "JK139": case "JK81": case "JZ05": case "JZ06": case "JH30": case "JK46": case "JA87": case "JA96": case "JH31":
                await CatalogPower.Install(card, context, play);
                if (card.DesignId == "JA105") await ArrowCombat.ActivateHaste(context, player);
                if (card.DesignId == "JK32") SetNamedFree(player, "JK32B", "JK32C");
                break;
            case "JH19": case "JK80":
                await Block(); await CatalogPower.Install(card, context, play); break;
            case "JH24":
                await Block(); JunnaCombatHooks.AddMaxEnergy(player.PlayerCombatState, V("Count")); break;
            case "JK05":
                await CardActions.DrawMatching(context, player, CardCatalog.IsKnowledge, CardPile.MaxCardsInHand); break;
            case "JK10B": case "JK73":
                await Attack(); await Block(); break;
            case "JK184D": case "JS34": case "JA15": case "JF07": case "JK82B": case "JH27B": case "JK148C":
                await Draw(V("DrawCount")); break;
            case "JK11":
                await Block(); await CardActions.Recall(context, card, "JK11B"); break;
            case "JK11B":
                await Attack(); await CardActions.Recall(context, card, "JK11"); break;
            case "JS35": case "JK184C":
            case "JB06":
            case "JK15B":
                await Block(); break;
            case "JG16":
                foreach (var target in await Choose(hand.Cards.Where(DamageModifiers.CanDealDamage)))
                    DamageModifiers.TransferBonus(All(), target);
                break;
            case "JT02":
                foreach (var selected in await Choose(hand.Cards.Where(c => HandArrow.OriginalType(c) != CardType.Power)))
                    HandArrow.Adapt(selected);
                break;
            case "JT03":
                await Attack(hits: V("Repeat", 2) + (ArrowCombat.Find(card)?.AttacksWhileWaiting ?? 0)); break;
            case "B":
                await Draw(V("DrawCount")); await Energy(V("Energy")); break;
            case "JH01":
                if (hand.Cards.Count < 2) break;
                var swapped = await Choose(hand.Cards, 2);
                if (swapped.Length == 2)
                {
                    int first = CardActions.Cost(swapped[0]), second = CardActions.Cost(swapped[1]);
                    CostChanges.SetFixed(swapped[0], second); CostChanges.SetFixed(swapped[1], first);
                }
                break;
            case "JK32B":
                await Attack(); SetNamedFree(player, "JK32", "JK32C"); break;
            case "JK32C":
                await Block(); SetNamedFree(player, "JK32", "JK32B"); break;
            case "JK33":
                await Attack(); await Improve(CardActions.Named(player, "JK33B"), V("Bonus")); break;
            case "JK33B":
                await Attack(); await Improve(CardActions.Named(player, "JK33"), 0, V("Count")); break;
            case "JK37": case "JK43": case "JK44": case "JK45": case "JK46B": case "JK50":
            case "JA108B": case "JA38": case "JA41": case "JK67B": case "JK69B": case "JA59": case "JA64": case "JK75B":
            case "JZ03": case "JA72": case "JA77": case "JA81B":
            case "JK112B": case "JK122": case "JK130B": case "JK131B":
            case "JK184": case "JS19": case "JG38": case "JK148": case "JK148D":
                await Attack(); break;
            case "JK37B":
                await Attack();
                if (play.Target is { IsAlive: true } redirectedTarget) ArrowCombat.RetargetEnemies(player, redirectedTarget);
                break;
            case "JK43B":
                await Improve(await CardActions.DrawMatching(context, player, c => CardCatalog.DesignId(c) == "JK43", 1), V("Bonus"));
                break;
            case "JK44B": break; // Intentionally blank knowledge burden; its native Exhaust keyword still resolves.
            case "JK45B":
                await Improve(Arrows(), V("Bonus")); await ArrowCombat.AccelerateAll(context, player, V("Turns")); break;
            case "JK48":
                await Attack(V("Damage") + All().Count(CardCatalog.IsKnowledge)); break;
            case "JK48B":
                await Block(V("Block") + All().Count(CardCatalog.IsKnowledge)); break;
            case "JK49":
                await Attack(); break;
            case "JK49B":
                await Block(); break;
            case "JK50B":
                var eloquence = hand.Cards.Where(c => CardCatalog.DesignId(c) == "JK50").ToArray();
                foreach (var selected in eloquence)
                    await CardActions.FreePlay(context, selected);
                break;
            case "JK53":
                foreach (var selected in await Choose(hand.Cards.Where(CardCatalog.IsKnowledge)))
                    await KnowledgeCombat.DrawPartners(context, selected);
                break;
            case "JA43":
                await Block(); await ArrowCombat.AccelerateAll(context, player, V("Turns")); break;
            case "JA53":
                await Block(); await CardActions.DrawMatching(context, player, CardCatalog.IsArrow, V("Cards")); break;
            case "JA57":
                await Draw(V("DrawCount")); await Energy(V("Energy")); break;
            case "JG27":
                await ImproveSelected(hand.Cards, V("Bonus")); break;
            case "JX01":
                await Block(); await Improve(Arrows(), V("Bonus")); break;
            case "JX02":
                await Attack(); await Energy(ArrowCombat.Deployed.Count(a => a.Card.Owner == player)); break;
            case "JX07":
                await Block(); await RetainedEnergy(V("Energy")); break;
            case "JH11": case "JH17":
                await Block(); await Energy(V("Energy")); break;
            case "JH13":
                await Attack(hits: V("Repeat") + player.PlayerCombatState.Energy / V("Hits") * V("Hits4")); break;
            case "JF10": case "JA58":
                var debuffTargets = card.DesignId == "JA58" ? player.Creature.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray()
                    : play.Target?.IsAlive == true ? new[] { play.Target } : [];
                foreach (var enemy in debuffTargets)
                {
                    await PowerCmd.Apply<VulnerablePower>(context, enemy, V("Debuff"), player.Creature, card);
                    await PowerCmd.Apply<WeakPower>(context, enemy, V("Debuff"), player.Creature, card);
                }
                if (card.DesignId == "JF10")
                {
                    if (player.Creature.GetPower<VulnerablePower>() is { } vulnerable) await PowerCmd.Remove(vulnerable);
                    if (player.Creature.GetPower<WeakPower>() is { } weak) await PowerCmd.Remove(weak);
                }
                break;
            case "JH20":
                await RetainedEnergy(V("Energy")); break;
            case "JA80":
                await Energy(V("Energy")); break;
            case "JH22":
                var concentrated = await DealDamage(card, context, play, V("Damage"), 1);
                await Improve(ArrowCombat.Deployed.Where(a => a.Card.Owner == player).Select(a => a.Card), concentrated.Sum(r => r.TotalDamage));
                break;
            case "JA65":
                foreach (var selected in await Choose(player.PlayerCombatState.DrawPile.Cards.Where(CardCatalog.IsArrow)))
                    await CardActions.FreePlay(context, selected);
                break;
            case "JG33":
                await Attack(); DamageModifiers.Add(card, V("Bonus")); await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Random); break;
            case "JA66":
                foreach (var selected in await Choose(player.PlayerCombatState.DiscardPile.Cards, V("Cards"), true))
                    await CardPileCmd.Add(selected, PileType.Hand);
                break;
            case "JK73B":
                foreach (var selected in await Choose(CardActions.Named(player, "JK73").Where(c => player.PlayerCombatState.HasEnoughResourcesFor(c, out _))))
                    await CardActions.PaidPlay(context, selected);
                break;
            case "JA67":
                await Attack();
                if (ArrowCombat.Find(card)?.ExhaustAfterResolve != true) await CardActions.PaidPlay(context, card);
                break;
            case "JK74":
                await Attack(hits: hand.Cards.Any(c => CardCatalog.DesignId(c) == "JK74B") ? 2 : 1); break;
            case "JK74B":
                await Block(); if (hand.Cards.Any(c => CardCatalog.DesignId(c) == "JK74")) await Block(); break;
            case "JA71":
                await Block(); await ArrowCombat.AccelerateAll(context, player, int.MaxValue); break;
            case "JK78":
                await ImproveSelected(player.PlayerCombatState.DrawPile.Cards, V("Bonus"), V("Amount"), true); break;
            case "JK78D":
                await ImproveSelected(player.PlayerCombatState.DrawPile.Cards, V("Bonus"), V("Amount"), true); break;
            case "JK78E":
                await ImproveSelected(player.PlayerCombatState.DiscardPile.Cards, V("Bonus"), V("Amount"), true); break;
            case "JA73":
                await Attack(); if (ArrowCombat.Deployed.Any(a => a.Card.Owner == player)) await Block(); break;
            case "JH26":
                await Block(); await EnergyReserve.Add(context, card, V("Energy")); break;
            case "JH27":
                await Block(); break;
            case "JG36":
                await Attack(); await Improve(hand.Cards, V("Bonus")); break;
            case "JK81B":
                await Block(V("Block") + hand.Cards.Count(c => c != card) * V("Count")); break;
            case "JA75":
                await Improve(hand.Cards, V("Bonus")); break;
            case "JK82":
                await Energy(V("Energy")); break;
            case "JA76":
                var swept = await DealDamage(card, context, play, V("Damage"), 1);
                foreach (var group in swept.GroupBy(r => r.Receiver))
                    await CardActions.Fly(context, player, group.Sum(r => r.TotalDamage), group.Key);
                break;
            case "JH29":
                await Energy(hand.Cards.Where(c => c != card && CardActions.HasNumericCost(c)).Sum(CardActions.Cost)); break;
            case "JA81":
                var linked = await DealDamage(card, context, play, V("Damage"), 1);
                await CardActions.Fly(context, player, linked.Sum(r => r.TotalDamage), play.Target, card.IsUpgraded); break;
            case "JA82":
                int x = card.ResolveEnergyXValue();
                for (int i = 0; i < x; i++) await CardActions.Fly(context, player, V("Damage"), upgraded: card.IsUpgraded);
                break;
            case "JA84":
                await Block(); await HandArrow.Select(context, card, V("Cards")); break;
            case "JA88":
                await Attack();
                if (!CombatManager.Instance.IsOverOrEnding) await HandArrow.Select(context, card, CardPile.MaxCardsInHand, optional: true);
                break;
            case "JK84":
                await HandArrow.Select(context, card, V("Cards")); break;
            case "JG37":
                await Draw(V("DrawCount")); await HandArrow.Select(context, card, V("Cards")); break;
            case "JA94":
                await Block(); await ArrowCombat.AccelerateSelf(context, player, V("Turns")); break;
            case "JK112":
                await Block(); await KnowledgeProgress.Revise(context, card); break;
            case "JK148E": case "JK130":
                await Block(); break;
            case "JK122B":
                break;
            case "JK126":
                await Block(); await KnowledgeCombat.DrawPartners(context, card); break;
            case "JK131":
                await Draw(V("DrawCount")); break;
            case "JK133":
                await Attack(); await Draw(V("DrawCount")); break;
            case "JK137":
                await CardChoices.Notes(context, card); break;
            case "JK139B": case "JK148F":
                await Energy(V("Energy")); break;
            case "JK148B":
                foreach (var selected in await Choose(player.PlayerCombatState.DrawPile.Cards.Where(CardCatalog.IsKnowledge), V("Cards")))
                    await CardPileCmd.Add(selected, PileType.Hand);
                break;
            case "JA108":
                await CardActions.PlayDrawPileArrows(context, player); break;
            case "JA104":
                await Block(); await CardChoices.AccelerateTargets(context, card); break;
            case "JF13":
                await Block(); await Draw(V("DrawCount")); break;
            case "JA145":
                await Attack(); await CardActions.Generate(player, "JA119", V("Cards"), PileType.Hand); break;
            case "JK184B":
                await ArrowCombat.AccelerateAll(context, player, V("Turns")); break;
            case "JH60":
                await CardChoices.EnergyTiming(context, card); break;
            case "JA119":
                if (play.Target is { } selectedTarget)
                    await ArrowCombat.AccelerateTarget(context, player, selectedTarget, V("Turns"));
                break;
            case "JK84B":
                await Attack(); await ArrowCombat.AccelerateAll(context, player, V("Turns")); break;
            default: throw new InvalidOperationException($"Missing card effect: {card.DesignId}");
        }
    }

    public static void SetNamedFree(MegaCrit.Sts2.Core.Entities.Players.Player player, params string[] ids)
    {
        foreach (var c in CardActions.All(player).Where(c => ids.Contains(CardCatalog.DesignId(c))))
        { c.EnergyCost.SetThisCombat(0); c.InvokeEnergyCostChanged(); }
    }

    public static async Task<DamageResult[]> DealDamage(CatalogCard card, PlayerChoiceContext context, CardPlay play, int damage, int hits)
    {
        if (hits <= 0 || (card.TargetType == TargetType.AnyEnemy && play.Target?.IsAlive != true)) return [];
        var attack = DamageCmd.Attack(damage).FromCard(card, play).WithHitCount(hits).WithHitFx(VfxCmd.slashPath);
        if (card.TargetType == TargetType.AllEnemies || card.DesignId == "JG34") attack.TargetingAllOpponents(card.Owner.Creature.CombatState!);
        else attack.Targeting(play.Target!);
        await attack.Execute(context);
        return attack.Results.SelectMany(r => r).ToArray();
    }
}
