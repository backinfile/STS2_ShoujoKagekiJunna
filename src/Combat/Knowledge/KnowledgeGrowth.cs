using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static partial class KnowledgeProgress
{
    // Historical run/replay properties still need a registered ID. Current hits use Core's archive.
    private static readonly SavedSpireField<CatalogCard, int> LegacyDepletedPartners = new(() => 0, "JunnaDepletedPartners");
    private static readonly SavedSpireField<CatalogCard, int> ResearchHits = new(() => 0, "JunnaPermanentHits");
    private static readonly SavedSpireField<CatalogCard, string> ResearchLinks = new(() => null, "JunnaResearchGroup");
    private static readonly SavedSpireField<CatalogCard, bool> ResearchCompleted = new(() => false, "JunnaResearchCompleted");
    private static readonly SavedSpireField<Player, int> ResearchSequence = new(() => 0, "JunnaResearchSequence");
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
