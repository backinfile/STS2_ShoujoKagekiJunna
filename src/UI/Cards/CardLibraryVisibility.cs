using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.RefreshVisibility))]
internal static class CardLibraryVisibility
{
    private static void Postfix(HashSet<CardModel> ____unlockedCards)
    {
        // Library access includes generated and associated cards outside the reward pool.
        ____unlockedCards.UnionWith(CardCatalog.Cards
            .Select(definition => CardCatalog.Model(definition.Id))
            .Where(card => card.ShouldShowInCardLibrary));
    }
}
