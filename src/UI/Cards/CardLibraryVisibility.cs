using HarmonyLib;
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
