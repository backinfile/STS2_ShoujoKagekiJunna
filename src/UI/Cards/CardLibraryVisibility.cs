using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.RefreshVisibility))]
internal static class CardLibraryVisibility
{
    private static void Postfix(object ____unlockedCards)
    {
        // Library access includes generated and associated cards outside the reward pool.
        for (int i = 0; i < CardCatalog.Cards.Count; i++)
        {
            var card = CardCatalog.Model(CardCatalog.Cards[i].Id);
            if (!card.ShouldShowInCardLibrary) continue;
            // Newer games store IDs; older games store the models themselves.
            if (____unlockedCards is HashSet<ModelId> ids) ids.Add(card.Id);
            else ((HashSet<CardModel>)____unlockedCards).Add(card);
        }
    }
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary._Ready))]
internal static class CardLibraryCharacterFilter
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Dictionary<CharacterModel, NCardPoolFilter> ____cardPoolFilters,
        Dictionary<NCardPoolFilter, Func<CardModel, bool>> ____poolFilters,
        NCardPoolFilter ____miscPoolFilter)
    {
        if (!____cardPoolFilters.TryGetValue(ModelDb.Character<Junna>(), out var filter)) return;
        var belongsToCharacter = ____poolFilters[filter];
        // Collection categories follow the runtime pool; model ownership and card styling stay intact.
        ____poolFilters[filter] = card => belongsToCharacter(card) &&
            (card.Rarity == CardRarity.Basic ||
             card is JunnaCard junna && CardCatalog.Get(junna.DesignId).Pool &&
             card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare);
        var belongsToMiscellaneous = ____poolFilters[____miscPoolFilter];
        ____poolFilters[____miscPoolFilter] = card => belongsToMiscellaneous(card) ||
            card is JunnaCard junna && !CardCatalog.Get(junna.DesignId).Pool &&
            card.Rarity is not (CardRarity.Basic or CardRarity.Ancient);
    }
}
