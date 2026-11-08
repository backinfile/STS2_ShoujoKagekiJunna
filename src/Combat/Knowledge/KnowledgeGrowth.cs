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
    private static readonly SavedSpireField<Player, int> ResearchSequence = new(() => 0, "JunnaResearchSequence");
    private static readonly SavedSpireField<CatalogCard, string> DamageBonus = new(() => "0", "JunnaPermanentDamageBonus");

    public static decimal PermanentDamageBonus(CardModel card) => card is CatalogCard catalog
        ? decimal.Parse(DamageBonus.Get(catalog) ?? "0", System.Globalization.CultureInfo.InvariantCulture) : 0m;

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
        if (!cards.Any(c => c.DesignId is "JK148" or "JK184")) return;
        var owner = cards[0].Owner;
        int sequence = checked(ResearchSequence.Get(owner) + 1);
        ResearchSequence.Set(owner, sequence);
        string link = owner.NetId + "/" + sequence;
        foreach (var card in cards.Where(c => c.DesignId is "JK148" or "JK148B" or "JK148C" or "JK148D" or "JK148E" or "JK148F" or "JK184" or "JK184B" or "JK184C" or "JK184D"))
            ResearchLinks.Set(card, link);
    }

    private static void ResearchDepleted(CatalogCard source, CardModel permanent)
    {
        if (source.DesignId is not ("JK148B" or "JK148C" or "JK148D" or "JK148E" or "JK148F" or "JK184B" or "JK184C" or "JK184D")
            || permanent is not CatalogCard member || ResearchCompleted.Get(member)) return;
        string? link = ResearchLinks.Get(member);
        if (string.IsNullOrEmpty(link)) return;
        ResearchCompleted.Set(member, true);
        string mainId = source.DesignId.StartsWith("JK184", StringComparison.Ordinal) ? "JK184" : "JK148";
        foreach (var paper in source.Owner.Deck.Cards.OfType<CatalogCard>()
                     .Where(c => c.DesignId == mainId && ResearchLinks.Get(c) == link))
        {
            int hits = checked(ExtraHits.Get(paper) + 1);
            if (mainId == "JK184") hits = Math.Min(6, hits);
            ExtraHits.Set(paper, hits);
            if (source.Owner.PlayerCombatState is { } combat)
                foreach (var copy in combat.AllCards.OfType<CatalogCard>().Where(c => c.DeckVersion == paper))
                    ExtraHits.Set(copy, hits);
        }
        source.Owner.PlayerCombatState?.RecalculateCardValues();
    }

    private static void CopyGrowth(CatalogCard source, CatalogCard clone)
    {
        DamageBonus.Set(clone, DamageBonus.Get(source));
        ResearchLinks.Set(clone, ResearchLinks.Get(source));
        ResearchCompleted.Set(clone, ResearchCompleted.Get(source));
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
