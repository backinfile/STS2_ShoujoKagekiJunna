using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Models;

using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

public static class CardActions
{
    public static IEnumerable<CardModel> All(Player player) => player.PlayerCombatState!.AllCards.Where(c => c.Pile?.Type != PileType.Play)
        .Concat(ArrowCombat.Deployed.Where(a => a.Card.Owner == player).Select(a => a.Card)).Distinct();
    public static IEnumerable<CardModel> Available(Player player) => All(player).Where(c => c.Pile?.Type != PileType.Play && ArrowCombat.Find(c)?.Resolving != true);
    public static IEnumerable<CardModel> Named(Player player, string id) => Available(player).Where(c => CardCatalog.DesignId(c) == id);
    public static int Cost(CardModel card) => card.EnergyCost.CostsX
        ? Math.Max(0, card.Owner.PlayerCombatState?.Energy ?? 0) : Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.All));
    internal static CardPlay ResolveTarget(CardPlay play)
    {
        if (play.Card.TargetType != TargetType.AnyEnemy || play.Target is not { } original) return play;
        var target = ArrowCombat.ResolveEffectTarget(original, play.Card.Owner.Creature.CombatState!);
        return target != null && target != original ? GameApi.WithTarget(play, target) : play;
    }
    public static bool HasNumericCost(CardModel card) => !card.EnergyCost.CostsX &&
        card.EnergyCost.GetWithModifiers(CostModifiers.All) >= 0 && !card.Keywords.Contains(CardKeyword.Unplayable);

    public static async Task<CardModel[]> Choose(PlayerChoiceContext context, CardModel source,
        IEnumerable<CardModel> candidates, int count = 1, bool optional = false)
    {
        var cards = candidates.Where(c => c != source).Distinct().ToArray();
        if (cards.Length == 0) return [];
        return (await CardPicking.Pick(context, source.Owner, cards, new CardPickRequest
        {
            Prompt = CardChoices.Prompt(source, count), Minimum = optional ? 0 : Math.Max(0, count),
            Maximum = Math.Max(0, count), Source = source
        })).ToArray();
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
        foreach (var card in candidates)
        {
            ArrowCombat.Cancel(card);
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    public static Task FreePlay(PlayerChoiceContext context, CardModel card, Creature? target = null,
        bool keepTarget = false) => Play(context, card, target, keepTarget);

    private static async Task<bool> Play(PlayerChoiceContext context, CardModel card, Creature? target,
        bool keepTarget)
    {
        if (CombatManager.Instance.IsOverOrEnding || card.Owner.Creature.IsDead ||
            card.Keywords.Contains(CardKeyword.Unplayable) ||
            !Hook.ShouldPlay(card.CombatState!, card, out _, AutoPlayType.Default)) return false;
        if (card.TargetType == TargetType.AnyEnemy)
        {
            if (keepTarget)
                target = target == null ? null : ArrowCombat.ResolveEffectTarget(target, card.Owner.Creature.CombatState!);
            else
                target = await ChooseTarget(context, card);
            if (target == null) return false;
        }
        else target = CardTargets.First(card);
        if (!CardTargets.IsValid(card, target)) return false;
        ArrowCombat.Cancel(card);
        return await CardExecution.Play(context, card, new CardExecutionRequest { Target = target });
    }

    public static async Task PlayDrawPileArrows(PlayerChoiceContext context, Player player)
    {
        var draw = player.PlayerCombatState!.DrawPile;
        var arrows = draw.Cards.Where(CardCatalog.IsArrow).ToArray();
        foreach (var card in arrows)
        {
            if (CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead) break;
            if (card.Pile != draw || card.Keywords.Contains(CardKeyword.Unplayable) ||
                !Hook.ShouldPlay(card.CombatState!, card, out _, AutoPlayType.Default)) continue;
            var target = CardTargets.First(card);
            if (!CardTargets.IsValid(card, target)) continue;
            if (card.EnergyCost.CostsX) card.EnergyCost.CapturedXValue = 0;
            CostChanges.CaptureFreePayment(card);
            await CardExecution.Play(context, card, new CardExecutionRequest { Target = target, CaptureAvailableX = false });
        }
    }

    public static async Task PlayAllArrows(PlayerChoiceContext context, Player player)
    {
        // Snapshot actual cards once, including deployed arrows, before effects create more arrows.
        var arrows = Available(player).Where(card => card.Pile != null && CardCatalog.IsArrow(card)).ToArray();
        foreach (var card in arrows)
        {
            if (CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead) break;
            if (card.HasBeenRemovedFromState || card.Pile == null || card.Pile.Type == PileType.Play ||
                ArrowCombat.Find(card)?.Resolving == true || card.Keywords.Contains(CardKeyword.Unplayable) ||
                !Hook.ShouldPlay(card.CombatState!, card, out _, AutoPlayType.Default)) continue;
            var target = CardTargets.First(card);
            if (!CardTargets.IsValid(card, target)) continue;
            ArrowCombat.Cancel(card);
            if (card.EnergyCost.CostsX) card.EnergyCost.CapturedXValue = 0;
            CostChanges.CaptureFreePayment(card);
            await CardExecution.Play(context, card, new CardExecutionRequest { Target = target, CaptureAvailableX = false });
        }
    }

    public static async Task<Creature?> ChooseTarget(PlayerChoiceContext context, CardModel card)
    {
        var enemies = CardTargets.Enemies(card);
        if (enemies.Length <= 1) return enemies.FirstOrDefault();
        if (CardSelectCmd.Selector != null) return enemies[0];
        int selected = await PlayerChoices.ChooseIndex(context, card.Owner, enemies.Length, async () =>
        {
            var manager = NTargetManager.Instance;
            manager.StartTargeting(TargetType.AnyEnemy, new Vector2(960, 800), TargetMode.ClickMouseToTarget,
                () => CombatManager.Instance.IsOverOrEnding, n => n is NCreature creature && enemies.Contains(creature.Entity));
            var target = await manager.SelectionFinished() as NCreature;
            return target == null ? -1 : Array.IndexOf(enemies, target.Entity);
        });
        return selected >= 0 && CardTargets.IsValid(card, enemies[selected]) ? enemies[selected] : null;
    }

    public static async Task Generate(Player player, string id, int count, PileType pile,
        CardPilePosition position, bool upgraded = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var added = new List<CardPileAddResult>();
        for (int i = 0; i < count; i++)
        {
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            added.Add(await CardPileCmd.AddGeneratedCardToCombat(card, pile, player, position));
        }
        await PreviewAdded(added, pile);
    }

    internal static async Task PreviewAdded(IReadOnlyList<CardPileAddResult> results, PileType destination)
    {
        if (destination is not (PileType.Draw or PileType.Discard or PileType.Exhaust or PileType.Deck)) return;
        var added = results.Where(result => result.success && LocalContext.IsMine(result.cardAdded)).ToArray();
        if (added.Length == 0) return;
        CardCmd.PreviewCardPileAdd(added, style: added.Length > 5 ? CardPreviewStyle.MessyLayout : CardPreviewStyle.HorizontalLayout);
        await Cmd.Wait(1f, ignoreCombatEnd: true);
    }

    public static async Task Fly(PlayerChoiceContext context, Player player, int damage, Creature? target = null, bool upgraded = false)
    {
        if (CombatManager.Instance.IsOverOrEnding) return;
        using var single = KnowledgeCombat.SingleCard();
        var arrow = (CatalogCard)CardCatalog.Create("JA81B", player);
        if (upgraded) { arrow.UpgradeInternal(); arrow.FinalizeUpgradeInternal(); }
        arrow.SetGeneratedDamage(damage);
        await GeneratedArrowPresentation.Play(arrow, async () =>
        {
            // Generated arrows retain the native automatic-play target policy.
            if (target != null)
            {
                target = ArrowCombat.ResolveEffectTarget(target, player.Creature.CombatState!);
                if (target == null)
                {
                    await arrow.MoveToResultPileWithoutPlaying(context);
                    return;
                }
            }
            await CardCmd.AutoPlay(context, arrow, target);
        });
    }

}
