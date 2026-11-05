using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static class CardActions
{
    public static IEnumerable<CardModel> All(Player player) => player.PlayerCombatState!.AllCards.Where(c => c.Pile?.Type != PileType.Play)
        .Concat(ArrowCombat.Deployed.Where(a => a.Card.Owner == player).Select(a => a.Card)).Distinct();
    public static IEnumerable<CardModel> Available(Player player) => All(player).Where(c => c.Pile?.Type != PileType.Play && ArrowCombat.Find(c)?.Resolving != true);
    public static IEnumerable<CardModel> Named(Player player, string id) => Available(player).Where(c => CardCatalog.DesignId(c) == id);
    public static int Cost(CardModel card) => card.EnergyCost.CostsX
        ? Math.Max(0, card.Owner.PlayerCombatState?.Energy ?? 0) : Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.All));
    public static bool HasNumericCost(CardModel card) => !card.EnergyCost.CostsX &&
        card.EnergyCost.GetWithModifiers(CostModifiers.All) >= 0 && !card.Keywords.Contains(CardKeyword.Unplayable);

    public static async Task<CardModel[]> Choose(PlayerChoiceContext context, CardModel source,
        IEnumerable<CardModel> candidates, int count = 1, bool optional = false)
    {
        var cards = candidates.Where(c => c != source).Distinct().ToArray();
        if (cards.Length == 0) return [];
        int maximum = Math.Min(cards.Length, count);
        var prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "JUNNA.SELECT_DAMAGE_CARD"), optional ? 0 : maximum, maximum);
        var chosen = cards.All(c => c.Pile?.Type == PileType.Hand)
            ? await CardSelectCmd.FromHand(context, source.Owner, prefs, cards.Contains, source)
            : await CardSelectCmd.FromSimpleGrid(context, cards, source.Owner, prefs);
        return chosen.Where(cards.Contains).Distinct().ToArray();
    }

    public static async Task<CardModel[]> DrawMatching(PlayerChoiceContext context, Player player,
        Func<CardModel, bool> predicate, int count)
    {
        var drawn = new List<CardModel>();
        for (int i = 0; i < count; i++)
        {
            var card = player.PlayerCombatState!.DrawPile.Cards.FirstOrDefault(predicate);
            if (card == null || player.PlayerCombatState.Hand.Cards.Count >= CardPile.MaxCardsInHand || CombatManager.Instance.IsOverOrEnding) break;
            // Move the chosen actual card to the top, then use the normal draw pipeline. No reshuffle is requested.
            if (!Hook.ShouldDraw(player.Creature.CombatState!, player, false, out _)) break;
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, skipVisuals: true);
            var result = await CardPileCmd.Draw(context, player);
            if (result == null) break;
            drawn.Add(result);
        }
        return drawn.ToArray();
    }

    public static async Task Recall(PlayerChoiceContext context, CardModel source, string id)
    {
        var candidates = Named(source.Owner, id).Where(c => c != source && c.Pile?.Type != PileType.Hand).ToArray();
        if (candidates.Length == 0) return;
        var chosen = candidates.Length == 1 ? candidates : await Choose(context, source, candidates);
        foreach (var card in chosen)
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    public static async Task<bool> PaidPlay(PlayerChoiceContext context, CardModel card, Creature? target = null,
        bool restartArrow = true)
    {
        if (CombatManager.Instance.IsOverOrEnding || card.Owner.Creature.IsDead ||
            card.Keywords.Contains(CardKeyword.Unplayable) || !card.Owner.PlayerCombatState!.HasEnoughResourcesFor(card, out _) ||
            !Hook.ShouldPlay(card.CombatState!, card, out _, AutoPlayType.Default)) return false;
        if (card.TargetType == TargetType.AnyEnemy)
        {
            if (target?.IsAlive != true) target = await ChooseTarget(context, card);
            if (target == null) return false;
        }
        else target = null;
        if (!card.Owner.PlayerCombatState.HasEnoughResourcesFor(card, out _)) return false;
        int energyValue = Cost(card), starValue = Math.Max(0, card.GetStarCostWithModifiers());
        var (energySpent, starsSpent) = await card.SpendResources();
        if (restartArrow) ArrowCombat.Cancel(card);
        await card.OnPlayWrapper(context, target, true, new ResourceInfo
        {
            EnergySpent = energySpent, EnergyValue = energyValue, StarsSpent = starsSpent, StarValue = starValue
        });
        return true;
    }

    public static async Task FreePlay(PlayerChoiceContext context, CardModel card)
    {
        if (CombatManager.Instance.IsOverOrEnding || card.Owner.Creature.IsDead ||
            card.Keywords.Contains(CardKeyword.Unplayable) ||
            !Hook.ShouldPlay(card.CombatState!, card, out _, AutoPlayType.Default)) return;
        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy)
        {
            target = await ChooseTarget(context, card);
            if (target == null) return;
        }
        await CardCmd.AutoPlay(context, card, target);
    }

    public static async Task<Creature?> ChooseTarget(PlayerChoiceContext context, CardModel card)
    {
        var enemies = card.Owner.Creature.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
        if (enemies.Length <= 1) return enemies.FirstOrDefault();
        if (CardSelectCmd.Selector != null) return enemies[0];
        var player = card.Owner;
        var sync = RunManager.Instance.PlayerChoiceSynchronizer;
        uint choiceId = sync.ReserveChoiceId(player);
        await context.SignalPlayerChoiceBegun(player, PlayerChoiceOptions.None);
        try
        {
            int selected;
            if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Replay)
            {
                var manager = NTargetManager.Instance;
                manager.StartTargeting(TargetType.AnyEnemy, new Vector2(960, 800), TargetMode.ClickMouseToTarget,
                    () => CombatManager.Instance.IsOverOrEnding, n => n is NCreature creature && enemies.Contains(creature.Entity));
                var target = await manager.SelectionFinished() as NCreature;
                selected = target == null ? -1 : Array.IndexOf(enemies, target.Entity);
                sync.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(selected));
            }
            else selected = (await sync.WaitForRemoteChoice(player, choiceId)).AsIndex();
            return selected >= 0 && selected < enemies.Length && enemies[selected].IsAlive ? enemies[selected] : null;
        }
        finally { await context.SignalPlayerChoiceEnded(); }
    }

    public static async Task Generate(Player player, string id, int count, PileType pile, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        for (int i = 0; i < count; i++)
        {
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.AddGeneratedCardToCombat(card, pile, player);
        }
    }

    public static async Task Fly(PlayerChoiceContext context, Player player, int damage, Creature? target = null, bool upgraded = false)
    {
        if (CombatManager.Instance.IsOverOrEnding) return;
        using var single = KnowledgeCombat.SingleCard();
        var arrow = (CatalogCard)CardCatalog.Create("JA81B", player);
        if (upgraded) { arrow.UpgradeInternal(); arrow.FinalizeUpgradeInternal(); }
        arrow.SetGeneratedDamage(damage);
        await CardPileCmd.AddGeneratedCardToCombat(arrow, PileType.Play, player);
        // Generated arrows use the native automatic-play target policy, independently per card.
        // An effect may supply a target; otherwise the combat target RNG selects one.
        if (target != null)
        {
            target = ArrowCombat.ResolveEffectTarget(target, player.Creature.CombatState!);
            if (target == null)
            {
                await CardPileCmd.Add(arrow, PileType.Discard);
                return;
            }
        }
        await CardCmd.AutoPlay(context, arrow, target);
    }

}
