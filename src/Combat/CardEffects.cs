using MegaCrit.Sts2.Core.Commands;
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
        int N(int index) => card.Number(index);
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
            case "JK81": case "JZ05": case "JZ06": case "JH30": case "JK46":
                await CatalogPower.Install(card, context, play);
                if (card.DesignId == "JK32") SetNamedFree(player, "JK32B", "JK32C");
                break;
            case "JH19": case "JH24": case "JK80":
                await Block(); await CatalogPower.Install(card, context, play); break;
            case "JK05":
                await CardActions.DrawMatching(context, player, CardCatalog.IsKnowledge, CardPile.MaxCardsInHand); break;
            case "JK10B": case "JK73":
                await Attack(); await Block(); break;
            case "JA15": case "JF07": case "JK82B":
                await Draw(N(0)); break;
            case "JK11":
                await Block(); await CardActions.Recall(context, card, "JK11B"); break;
            case "JK11B":
                await Attack(); await CardActions.Recall(context, card, "JK11"); break;
            case "JK15B":
                await Block(); break;
            case "JG16":
                foreach (var target in await Choose(hand.Cards.Where(DamageModifiers.CanDealDamage)))
                    DamageModifiers.TransferBonus(All(), target);
                break;
            case "JT02":
                foreach (var selected in await Choose(hand.Cards.Where(CardCatalog.IsArrow)))
                    ArrowConversion.Convert(selected);
                break;
            case "JT03":
                await Attack(hits: V("Repeat", 2) + (ArrowCombat.Find(card)?.AttacksWhileWaiting ?? 0)); break;
            case "B":
                await Draw(N(0)); await Energy(N(1)); break;
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
                await Attack(); await Improve(CardActions.Named(player, "JK33"), 0, N(1)); break;
            case "JK37": case "JK43": case "JK44": case "JK45": case "JK46B": case "JK50":
            case "JA38": case "JA41": case "JK67B": case "JK69B": case "JA59": case "JA64":
            case "JZ03": case "JA72": case "JA77": case "JA81B":
                await Attack(); break;
            case "JK37B":
                await Attack(); await CardActions.DrawMatching(context, player, CardCatalog.IsArrow, N(1)); break;
            case "JK43B":
                await CardActions.DrawMatching(context, player, c => CardCatalog.DesignId(c) == "JK43", 1);
                foreach (var c in CardActions.Named(player, "JK43"))
                { c.EnergyCost.AddThisCombat(-N(0)); c.InvokeEnergyCostChanged(); }
                break;
            case "JK44B": break; // Intentionally blank knowledge burden; its native Exhaust keyword still resolves.
            case "JK45B":
                await Improve(Arrows(), V("Bonus")); await ArrowCombat.AccelerateAll(context, player, N(2)); break;
            case "JK48":
                await Attack(V("Damage") + All().Count(CardCatalog.IsKnowledge)); break;
            case "JK48B":
                await Block(V("Block") + All().Count(CardCatalog.IsKnowledge)); break;
            case "JK49":
                await Attack(); CardForms.AfterPlay(card, "JK49B"); break;
            case "JK49B":
                await Block(); CardForms.AfterPlay(card, "JK49"); break;
            case "JK50B":
                foreach (var selected in await Choose(hand.Cards.Where(c => CardCatalog.DesignId(c) == "JK50")))
                    await CardActions.FreePlay(context, selected);
                break;
            case "JK53":
                foreach (var selected in await Choose(hand.Cards.Where(CardCatalog.IsKnowledge)))
                    await KnowledgeCombat.DrawPartners(context, selected);
                break;
            case "JA43":
                await Block(); await ArrowCombat.AccelerateAll(context, player, N(1)); break;
            case "JA53":
                await Block(); await CardActions.DrawMatching(context, player, CardCatalog.IsArrow, N(1)); break;
            case "JA57":
                await Draw(N(0)); await Energy(N(1)); break;
            case "JG27":
                await ImproveSelected(CardActions.Available(player), V("Bonus")); break;
            case "JX01":
                await Block(); await Improve(Arrows(), V("Bonus")); break;
            case "JX02":
                await Attack(); await Energy(ArrowCombat.Deployed.Count(a => a.Card.Owner == player)); break;
            case "JX07":
                await Block(); await RetainedEnergy(N(1)); break;
            case "JH11": case "JH17":
                await Block(); await Energy(N(1)); break;
            case "JH13":
                await Attack(hits: 1 + player.PlayerCombatState.Energy / N(1)); break;
            case "JF10": case "JA58":
                if (play.Target?.IsAlive == true)
                {
                    await PowerCmd.Apply<VulnerablePower>(context, play.Target, N(0), player.Creature, card);
                    await PowerCmd.Apply<WeakPower>(context, play.Target, N(0), player.Creature, card);
                }
                if (card.DesignId == "JF10")
                {
                    if (player.Creature.GetPower<VulnerablePower>() is { } vulnerable) await PowerCmd.Remove(vulnerable);
                    if (player.Creature.GetPower<WeakPower>() is { } weak) await PowerCmd.Remove(weak);
                }
                break;
            case "JH20": case "JA80":
                await RetainedEnergy(N(0)); break;
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
                foreach (var selected in await Choose(player.PlayerCombatState.DiscardPile.Cards, N(0), true))
                    await CardPileCmd.Add(selected, PileType.Hand);
                break;
            case "JK73B":
                foreach (var selected in await Choose(CardActions.Named(player, "JK73").Where(c => player.PlayerCombatState.HasEnoughResourcesFor(c, out _))))
                    await CardActions.PaidPlay(context, selected);
                break;
            case "JA67":
                await Attack(); await CardActions.PaidPlay(context, card); break;
            case "JK74":
                await Attack(hits: hand.Cards.Any(c => CardCatalog.DesignId(c) == "JK74B") ? 2 : 1); break;
            case "JK74B":
                await Block(); if (hand.Cards.Any(c => CardCatalog.DesignId(c) == "JK74")) await Block(); break;
            case "JA71":
                await Block(); await ArrowCombat.AccelerateAll(context, player, int.MaxValue); break;
            case "JK78":
                await Improve(await CardChoices.ChooseTrainingPile(context, card), V("Bonus")); break;
            case "JK78D":
                await ImproveSelected(player.PlayerCombatState.DrawPile.Cards, V("Bonus"), N(0), true); break;
            case "JK78E":
                await ImproveSelected(player.PlayerCombatState.DiscardPile.Cards, V("Bonus"), N(0), true); break;
            case "JA73":
                await Attack(); if (ArrowCombat.Deployed.Any(a => a.Card.Owner == player)) await Block(); break;
            case "JH26":
                await Block(); await EnergyReserve.Add(context, card, N(1)); break;
            case "JH27":
                await Block(); break;
            case "JG36":
                await Attack(); await ImproveSelected(hand.Cards, V("Bonus")); break;
            case "JK81B":
                await Block(V("Block") + hand.Cards.Count(c => c != card) * N(1)); break;
            case "JA75":
                await Improve(hand.Cards, V("Bonus")); break;
            case "JK82":
                await Energy(N(0)); break;
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
            case "JH31":
                await CardActions.DrawMatching(context, player, c => CardActions.HasNumericCost(c) && CardActions.Cost(c) == 0, N(0)); break;
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
        if (card.TargetType == TargetType.AllEnemies) attack.TargetingAllOpponents(card.Owner.Creature.CombatState!);
        else attack.Targeting(play.Target!);
        await attack.Execute(context);
        return attack.Results.SelectMany(r => r).ToArray();
    }
}
