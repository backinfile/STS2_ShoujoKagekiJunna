using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Permanent card progress and run rewards use BaseLib's saved fields.</summary>
public static class KnowledgeProgress
{
    private static readonly SavedSpireField<CatalogCard, int> ExtraHits = new(() => 0, "JunnaPermanentHits");
    private static readonly SavedSpireField<Player, int> OpeningInsights = new(() => 0, "JunnaOpeningInsights");
    private static readonly HashSet<Player> Opened = [];
    private static readonly Dictionary<Player, int> CopyRewards = [];
    public static void Initialize() { }
    public static int PermanentHits(CardModel card) => card is CatalogCard catalog ? ExtraHits.Get(catalog) : 0;

    public static async Task Revise(PlayerChoiceContext context, CatalogCard source)
    {
        var player = source.Owner;
        var permanent = source.DeckVersion ?? source;
        bool permanentSource = Shine.GetState(source) is not { Temporary: true } && !source.IsDupe && player.Deck.Cards.Contains(permanent);
        var candidates = permanentSource ? player.Deck.Cards : CardActions.Available(player);
        var targets = candidates.Where(c => CardCatalog.DesignId(c) == "JK112B").ToArray();
        var chosen = targets.Length <= 1 ? targets : await CardActions.Choose(context, source, targets);
        foreach (var target in chosen.OfType<CatalogCard>())
        {
            if (!permanentSource)
            {
                DamageModifiers.Add(target, extraHits: 1);
                continue;
            }
            ExtraHits.Set(target, checked(ExtraHits.Get(target) + 1));
            foreach (var combatCard in player.PlayerCombatState!.AllCards.OfType<CatalogCard>().Where(c => c.DeckVersion == target))
                ExtraHits.Set(combatCard, ExtraHits.Get(target));
        }
        player.PlayerCombatState?.RecalculateCardValues();
    }

    public static async Task Depleted(CatalogCard card, ShineDepletionContext context)
    {
        if (context.PermanentCard == null) return;
        if (card.DesignId == "JK139B")
        {
            OpeningInsights.Set(context.Player, checked(OpeningInsights.Get(context.Player) + 1));
        }
        else if (card.DesignId == "JS19")
        {
            var choices = context.Player.Deck.Cards.Where(c => c != context.PermanentCard && c.IsRemovable).ToArray();
            if (choices.Length == 0) return;
            var prefs = new CardSelectorPrefs(CardChoices.Prompt(card), 0, 1);
            var selected = await CardSelectCmd.FromSimpleGrid(context.ChoiceContext ?? new BlockingPlayerChoiceContext(), choices, context.Player, prefs);
            foreach (var target in selected.Where(context.Player.Deck.Cards.Contains).ToArray())
                await CardPileCmd.RemoveFromDeck(target);
        }
    }

    public static void Acquired(CardModel card)
    {
        if (card is JunnaCard && card.Pile?.Type == PileType.Deck && Shine.GetState(card) is { Temporary: true } state)
            Shine.SetState(card, state.Remaining, state.Maximum);
    }
    public static void RegisterCopy(Player player) => CopyRewards[player] = CopyRewards.GetValueOrDefault(player) + 1;
    public static async Task WonCombat()
    {
        var rewards = CopyRewards.ToArray();
        CopyRewards.Clear();
        foreach (var (player, count) in rewards)
            for (int i = 0; i < count; i++) await CopyDeckCard(player);
    }
    public static void ResetCombat()
    {
        Opened.Clear();
        CopyRewards.Clear();
    }
    public static async Task OpenCombat(Player player, PlayerChoiceContext context)
    {
        if (player.PlayerCombatState!.TurnNumber != 1 || !Opened.Add(player)) return;
        using var single = KnowledgeCombat.SingleCard();
        for (int i = 0; i < OpeningInsights.Get(player); i++)
        {
            var insight = CardCatalog.Create("JK139", player);
            await CardPileCmd.AddGeneratedCardToCombat(insight, PileType.Play, player);
            await CardActions.FreePlay(context, insight);
        }
    }

    public static async Task CopyDeckCard(Player player)
    {
        var candidates = player.Deck.Cards.Where(c => Shine.GetState(c) is not { Remaining: <= 0 }).ToArray();
        if (candidates.Length == 0) return;
        var prompt = new LocString("cards", "JUNNA_JK127.selectionScreenPrompt");
        var selected = await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), candidates, player,
            new CardSelectorPrefs(prompt, 0, 1));
        using var single = KnowledgeCombat.SingleCard();
        foreach (var original in selected.Where(player.Deck.Cards.Contains))
        {
            // Transfer serialization keeps permanent modifications and the current Shine budget.
            // The new physical card receives an independent depletion lifecycle from Core.
            var budget = Shine.GetState(original);
            var copy = player.RunState.LoadCard(original.ToSerializable(), player);
            if (budget != null) Shine.SetState(copy, budget.Remaining, budget.Maximum);
            await CardPileCmd.Add(copy, PileType.Deck);
        }
    }

    [HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
    private static class CopyProgress
    {
        private static void Postfix(AbstractModel __instance, AbstractModel __result)
        {
            if (__instance is CatalogCard source && __result is CatalogCard clone)
                ExtraHits.Set(clone, ExtraHits.Get(source));
        }
    }
}
