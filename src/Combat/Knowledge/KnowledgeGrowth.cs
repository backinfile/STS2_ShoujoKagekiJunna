using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKagekiJunna.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

public static partial class KnowledgeProgress
{
    // Historical run/replay properties still need a registered ID. Current hits use Core's archive.
    private static readonly SavedSpireField<CatalogCard, int> LegacyDepletedPartners = new(() => 0, "JunnaDepletedPartners");
    private static readonly SavedSpireField<CatalogCard, int> ResearchHits = new(() => 0, "JunnaPermanentHits");
    private static readonly SavedSpireField<CatalogCard, string> ResearchLinks = new(() => null, "JunnaResearchGroup");
    private static readonly SavedSpireField<CatalogCard, bool> ResearchCompleted = new(() => false, "JunnaResearchCompleted");
    private static readonly SavedSpireField<Player, int> ResearchSequence = new(() => 0, "JunnaResearchSequence");
    internal static string? ResearchGroup(CardModel card) => card is CatalogCard catalog ? ResearchLinks.Get(catalog) : null;

    public static int DepletedPartners(CardModel card)
    {
        if (card is not CatalogCard catalog
            || ResearchRules.ForMain(catalog.DesignId) is not { ShowDepletedPartners: true } rule) return 0;
        if (rule.Counter == ResearchCounter.ExtraHits) return ResearchHits.Get(catalog);
        if (!catalog.IsMutable || catalog.Owner is not { } owner) return 0;
        var memberIds = rule.Members.Select(id => CardCatalog.Model(id).Id.ToString()).ToHashSet(StringComparer.Ordinal);
        return Shine.GetDepletedCards(owner).Count(entry => memberIds.Contains(entry.ModelId));
    }

    internal static void BindResearch(IEnumerable<CardModel> acquired)
    {
        var cards = acquired.OfType<CatalogCard>().ToArray();
        if (!cards.Any(card => ResearchRules.ForMain(card.DesignId) is { Counter: ResearchCounter.ExtraHits })) return;
        var owner = cards[0].Owner;
        int sequence = checked(ResearchSequence.Get(owner) + 1);
        ResearchSequence.Set(owner, sequence);
        string link = owner.NetId + "/" + sequence;
        foreach (var card in cards.Where(card => ResearchRules.NeedsLink(card.DesignId)))
            ResearchLinks.Set(card, link);
    }

    private static void ResearchDepleted(CatalogCard source, CardModel permanent)
    {
        if (ResearchRules.ForPartner(source.DesignId) is not { Counter: ResearchCounter.ExtraHits } rule
            || permanent is not CatalogCard member || ResearchCompleted.Get(member)) return;
        string? link = ResearchLinks.Get(member);
        if (string.IsNullOrEmpty(link)) return;
        ResearchCompleted.Set(member, true);
        foreach (var paper in source.Owner.Deck.Cards.OfType<CatalogCard>()
                     .Where(c => c.DesignId == rule.MainCardId && ResearchLinks.Get(c) == link))
        {
            int hits = rule.Advance(ResearchHits.Get(paper));
            ResearchHits.Set(paper, hits);
            if (source.Owner.PlayerCombatState is { } combat)
                foreach (var copy in combat.AllCards.OfType<CatalogCard>().Where(c => c.DeckVersion == paper))
                    ResearchHits.Set(copy, hits);
        }
        source.Owner.PlayerCombatState?.RecalculateCardValues();
    }

    private static void CopyGrowth(CatalogCard source, CatalogCard clone)
    {
        if (ResearchRules.ForMain(source.DesignId)?.Counter == ResearchCounter.ExtraHits)
            ResearchHits.Set(clone, ResearchHits.Get(source));
        ResearchLinks.Set(clone, ResearchLinks.Get(source));
        ResearchCompleted.Set(clone, ResearchCompleted.Get(source));
    }

    private static async Task OfferKnowledgeCopy(Player player, int maximum)
    {
        var reward = new KnowledgeCopyReward(player, maximum);
        if (player.RunState.CurrentRoom is CombatRoom room)
        {
            room.AddExtraReward(player, reward);
            await PowerCmd.Apply<JunnaKnowledgeCopyRewardPower>(new ThrowingPlayerChoiceContext(),
                player.Creature, 1, player.Creature, null);
        }
        else await RewardsCmd.OfferCustom(player, [reward]);
    }

    public static async Task CopySingleCards(Player player, IEnumerable<CardModel> selected)
    {
        using var single = KnowledgeCombat.SingleCard();
        var added = new List<CardPileAddResult>();
        foreach (var original in selected.Distinct().Where(player.Deck.Cards.Contains).ToArray())
        {
            var copy = ShoujoKageki.Core.V1.CardCopies.ForDeck(original, player, ShoujoKageki.Core.V1.CardCopyPurpose.Transfer);
            added.Add(await CardPileCmd.Add(copy, PileType.Deck));
        }
        await CardActions.PreviewAdded(added, PileType.Deck);
    }
}
