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

internal interface ICombatOpeningCard
{
    Task PlayAtCombatOpening(PlayerChoiceContext context);
}

/// <summary>Permanent card progress and run rewards use BaseLib's saved fields.</summary>
public static partial class KnowledgeProgress
{
    private static readonly SavedSpireField<Player, int> OpeningInsights = new(() => 0, "JunnaOpeningInsights");
    private static readonly HashSet<Player> Opened = [];
    private static readonly Dictionary<Player, int> StoryRewards = [];
    private static readonly List<(Player Player, int Count)> VictoryKnowledgeUpgrades = [];
    public static void Initialize() { _ = LegacyDepletedPartners; }
    public static async Task Depleted(CatalogCard card, ShineDepletionContext context)
    {
        if (context.PermanentCard == null) return;
        await card.OnPermanentShineDepleted(context);
    }

    public static void Acquired(CardModel card)
    {
        if (card is JunnaCard && card.Pile?.Type == PileType.Deck && Shine.GetState(card) is { Temporary: true } state)
            Shine.SetState(card, state.Remaining, state.Maximum);
    }
    public static void RegisterStory(Player player) => StoryRewards[player] = checked(StoryRewards.GetValueOrDefault(player) + 1);
    internal static async Task UpgradeRandomKnowledge(Player player, int count)
    {
        // The native upgrade command skips effects while the combat is ending.
        if (CombatManager.Instance.IsEnding)
        {
            VictoryKnowledgeUpgrades.Add((player, count));
            return;
        }

        var candidates = player.Deck.Cards.Where(card => card.IsUpgradable && CardCatalog.IsKnowledge(card)).ToList();
        var rng = player.RunState.Rng.CombatCardSelection;
        var selected = new List<CardModel>();
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            var card = rng.NextItem(candidates)!;
            selected.Add(card);
            candidates.Remove(card);
        }
        if (selected.Count > 0)
            await CardUpgrades.Upgrade(selected, CardPreviewStyle.HorizontalLayout);
    }

    public static async Task WonCombat()
    {
        var upgrades = VictoryKnowledgeUpgrades.ToArray();
        VictoryKnowledgeUpgrades.Clear();
        foreach (var (player, count) in upgrades)
            await UpgradeRandomKnowledge(player, count);

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
        VictoryKnowledgeUpgrades.Clear();
    }
    public static async Task OpenCombat(Player player, PlayerChoiceContext context)
    {
        if (player.PlayerCombatState!.TurnNumber != 1 || !Opened.Add(player)) return;
        using var single = KnowledgeCombat.SingleCard();
        foreach (var card in player.PlayerCombatState.AllCards.OfType<ICombatOpeningCard>().ToArray())
            await card.PlayAtCombatOpening(context);
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
}
