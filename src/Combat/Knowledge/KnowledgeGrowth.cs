using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static partial class KnowledgeProgress
{
    private static readonly SavedSpireField<CatalogCard, string> ResearchLinks = new(() => null, "JunnaResearchGroup");
    private static readonly SavedSpireField<CatalogCard, bool> ResearchCompleted = new(() => false, "JunnaResearchCompleted");
    private static readonly SavedSpireField<CatalogCard, int> ResearchCount = new(() => 0, "JunnaDepletedPartners");
    private static readonly SavedSpireField<Player, int> ResearchSequence = new(() => 0, "JunnaResearchSequence");
    private static readonly SavedSpireField<CatalogCard, string> DamageBonus = new(() => "0", "JunnaPermanentDamageBonus");

    public static decimal PermanentDamageBonus(CardModel card) => card is CatalogCard catalog
        ? decimal.Parse(DamageBonus.Get(catalog) ?? "0", System.Globalization.CultureInfo.InvariantCulture) : 0m;

    internal static string? ResearchGroup(CardModel card) => card is CatalogCard catalog ? ResearchLinks.Get(catalog) : null;

    public static int DepletedPartners(CardModel card) => card is not CatalogCard catalog ? 0
        : ResearchRules.ForMain(catalog.DesignId) is { Counter: ResearchCounter.ExtraHits, ShowDepletedPartners: true }
            ? ExtraHits.Get(catalog) : ResearchCount.Get(catalog);

    internal static void PreserveFirstBonus(CardModel card, decimal amount)
    {
        if (card is not CatalogCard { DesignId: "JG38", IsDupe: false } current || amount <= 0) return;
        var permanent = current.DeckVersion as CatalogCard ?? current;
        if (!current.Owner.Deck.Cards.Contains(permanent)) return;
        decimal total = checked(PermanentDamageBonus(permanent) + amount);
        DamageBonus.Set(permanent, total.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (current != permanent) DamageBonus.Set(current, total.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    internal static void BindResearch(IEnumerable<CardModel> acquired)
    {
        var cards = acquired.OfType<CatalogCard>().ToArray();
        if (!cards.Any(card => ResearchRules.ForMain(card.DesignId) != null)) return;
        var owner = cards[0].Owner;
        int sequence = checked(ResearchSequence.Get(owner) + 1);
        ResearchSequence.Set(owner, sequence);
        string link = owner.NetId + "/" + sequence;
        foreach (var card in cards.Where(card => ResearchRules.Contains(card.DesignId)))
            ResearchLinks.Set(card, link);
    }

    private static void ResearchDepleted(CatalogCard source, CardModel permanent)
    {
        if (ResearchRules.ForPartner(source.DesignId) is not { } rule
            || permanent is not CatalogCard member || ResearchCompleted.Get(member)) return;
        string? link = ResearchLinks.Get(member);
        if (string.IsNullOrEmpty(link)) return;
        ResearchCompleted.Set(member, true);
        var field = rule.Counter == ResearchCounter.ExtraHits ? ExtraHits : ResearchCount;
        foreach (var paper in source.Owner.Deck.Cards.OfType<CatalogCard>()
                     .Where(card => card.DesignId == rule.MainCardId && ResearchLinks.Get(card) == link))
        {
            int value = rule.Advance(field.Get(paper));
            field.Set(paper, value);
            if (source.Owner.PlayerCombatState is { } combat)
                foreach (var copy in combat.AllCards.OfType<CatalogCard>().Where(card => card.DeckVersion == paper))
                    field.Set(copy, value);
        }
        source.Owner.PlayerCombatState?.RecalculateCardValues();
    }

    private static void CopyGrowth(CatalogCard source, CatalogCard clone)
    {
        DamageBonus.Set(clone, DamageBonus.Get(source));
        ResearchLinks.Set(clone, ResearchLinks.Get(source));
        ResearchCompleted.Set(clone, ResearchCompleted.Get(source));
        ResearchCount.Set(clone, ResearchCount.Get(source));
    }

    private static async Task OfferKnowledgeCopy(Player player, int maximum)
    {
        var reward = new KnowledgeCopyReward(player, maximum);
        if (player.RunState.CurrentRoom is CombatRoom room) room.AddExtraReward(player, reward);
        else await RewardsCmd.OfferCustom(player, [reward]);
    }

    public static async Task CopySingleCards(Player player, IEnumerable<CardModel> selected)
    {
        using var single = KnowledgeCombat.SingleCard();
        foreach (var original in selected.Distinct().Where(player.Deck.Cards.Contains).ToArray())
        {
            var copy = ShoujoKageki.Core.V1.CardCopies.ForDeck(original, player, ShoujoKageki.Core.V1.CardCopyPurpose.Transfer);
            await CardPileCmd.Add(copy, PileType.Deck);
        }
    }
}
