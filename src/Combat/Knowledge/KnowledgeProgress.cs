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
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Permanent card progress and run rewards use BaseLib's saved fields.</summary>
public static partial class KnowledgeProgress
{
    private static readonly SavedSpireField<Player, int> OpeningInsights = new(() => 0, "JunnaOpeningInsights");
    private static readonly HashSet<Player> Opened = [];
    private static readonly Dictionary<Player, int> StoryRewards = [];
    public static void Initialize() { _ = LegacyDepletedPartners; }
    public static async Task Depleted(CatalogCard card, ShineDepletionContext context)
    {
        if (context.PermanentCard == null) return;
        ResearchDepleted(card, context.PermanentCard);
        if (card.DesignId == "JK122B") context.Player.PlayerCombatState?.RecalculateCardValues();
        if (card.DesignId == "JK139B")
        {
            OpeningInsights.Set(context.Player, checked(OpeningInsights.Get(context.Player) + 1));
        }
        else if (card.DesignId == "JS19")
        {
            await OfferKnowledgeCopy(context.Player, card.IsUpgraded ? 2 : 1);
        }
    }

    public static void Acquired(CardModel card)
    {
        if (card is JunnaCard && card.Pile?.Type == PileType.Deck && Shine.GetState(card) is { Temporary: true } state)
            Shine.SetState(card, state.Remaining, state.Maximum);
    }
    public static void RegisterStory(Player player) => StoryRewards[player] = checked(StoryRewards.GetValueOrDefault(player) + 1);
    public static async Task WonCombat()
    {
        var stories = StoryRewards.ToArray();
        StoryRewards.Clear();
        using (KnowledgeCombat.SingleCard())
            foreach (var (player, count) in stories)
            {
                var added = new List<CardPileAddResult>();
                for (int i = 0; i < count; i++)
                    added.Add(await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model("U_CARD_008B"), player), PileType.Deck));
                CardCmd.PreviewCardPileAdd(added, style: added.Count > 5 ? CardPreviewStyle.MessyLayout : CardPreviewStyle.HorizontalLayout);
            }
    }
    public static void ResetCombat()
    {
        Opened.Clear();
        StoryRewards.Clear();
    }
    public static async Task OpenCombat(Player player, PlayerChoiceContext context)
    {
        if (player.PlayerCombatState!.TurnNumber != 1 || !Opened.Add(player)) return;
        using var single = KnowledgeCombat.SingleCard();
        foreach (var arrow in player.PlayerCombatState.AllCards.Where(c => CardCatalog.DesignId(c) == "JK37").ToArray())
        {
            var target = player.Creature.CombatState!.HittableEnemies.FirstOrDefault(c => c.IsAlive && arrow.IsValidTarget(c));
            if (target != null) await CardCmd.AutoPlay(context, arrow, target);
        }
        for (int i = 0; i < OpeningInsights.Get(player); i++)
        {
            var insight = CardCatalog.Create("JK139", player);
            await CardPileCmd.AddGeneratedCardToCombat(insight, PileType.Play, player);
            await CardActions.FreePlay(context, insight);
        }
    }

    public static async Task CopyDeckCard(Player player)
    {
        var candidates = player.Deck.Cards.Where(CardCatalog.IsKnowledge).Where(c => Shine.GetState(c) is not { Remaining: <= 0 }).ToArray();
        if (candidates.Length == 0) return;
        var prompt = new LocString("cards", "JUNNA_JK127.selectionScreenPrompt");
        var selected = await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), candidates, player,
            new CardSelectorPrefs(prompt, 0, 1));
        await CopySingleCards(player, selected);
    }
    internal static void CopyResearch(CardModel source, CardModel copy)
    {
        if (source is CatalogCard original && copy is CatalogCard clone && ResearchRules.NeedsLink(original.DesignId))
            CopyGrowth(original, clone);
    }
}
