using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaCardLibraryRegression;

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    public static void Initialize() => new Harmony("junna.card-library.regression").Patch(
        AccessTools.Method(typeof(NMainMenu), "_Ready"), postfix: new HarmonyMethod(typeof(Probe), nameof(Check)));

    private static async void Check()
    {
        bool passed = false;
        int checkedCards = 0;
        int classificationChecks = 0;
        int visibleChecks = 0;
        var visibleCollections = new List<object>();
        int characterCards = 0, miscellaneousCards = 0, ancientCards = 0;
        string? error = null;
        var grid = new NCardLibraryGrid();
        try
        {
            var source = CardCatalog.Model("U_CARD_044");
            var baseMembers = KnowledgePreviewCards.Create(source).Cast<CatalogCard>().ToArray();
            var upgradedMembers = KnowledgePreviewCards.Create(source, upgradeAll: true).Cast<CatalogCard>().ToArray();
            if (baseMembers.Length != 2 || baseMembers.Any(card => card.IsUpgraded || card.Value("Damage") != 15)
                || upgradedMembers.Length != 2 || upgradedMembers.Any(card => !card.IsUpgraded || card.Value("Damage") != 18)
                || source.IsUpgraded)
                throw new InvalidOperationException("Knowledge library previews must upgrade only the cloned cards.");

            for (int repeat = 0; repeat < 3; repeat++)
            {
                grid.RefreshVisibility();
                var unlocked = (HashSet<ModelId>)AccessTools.Field(typeof(NCardLibraryGrid), "_unlockedCards").GetValue(grid)!;
                for (int i = 0; i < CardCatalog.Cards.Count; i++)
                {
                    var card = CardCatalog.Model(CardCatalog.Cards[i].Id);
                    if (!card.ShouldShowInCardLibrary) continue;
                    if (!unlocked.Contains(card.Id)) throw new InvalidOperationException("Card missing from library: " + card.Id);
                    checkedCards++;
                }
            }
            if (checkedCards == 0) throw new InvalidOperationException("No Junna cards checked");
            var library = GD.Load<PackedScene>("res://scenes/screens/card_library/card_library.tscn").Instantiate<NCardLibrary>();
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(library);
            try
            {
                var characters = (Dictionary<CharacterModel, NCardPoolFilter>)AccessTools.Field(typeof(NCardLibrary), "_cardPoolFilters").GetValue(library)!;
                var filters = (Dictionary<NCardPoolFilter, Func<CardModel, bool>>)AccessTools.Field(typeof(NCardLibrary), "_poolFilters").GetValue(library)!;
                var characterFilter = filters[characters[ModelDb.Character<Junna>()]];
                var misc = (NCardPoolFilter)AccessTools.Field(typeof(NCardLibrary), "_miscPoolFilter").GetValue(library)!;
                var ancient = (NCardPoolFilter)AccessTools.Field(typeof(NCardLibrary), "_ancientsFilter").GetValue(library)!;
                var libraryGrid = (NCardLibraryGrid)AccessTools.Field(typeof(NCardLibrary), "_grid").GetValue(library)!;
                foreach (var selected in new[] { characters[ModelDb.Character<Junna>()], misc, characters[ModelDb.Character<Junna>()] })
                {
                    selected.IsSelected = true;
                    AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter").Invoke(library, [selected]);
                    await library.ToSignal(library.GetTree(), SceneTree.SignalName.ProcessFrame);
                    var visible = libraryGrid.VisibleCards.OfType<JunnaCard>().Select(c => c.DesignId).ToHashSet();
                    foreach (var definition in CardCatalog.Cards.Where(c => !c.ChoiceOnly))
                    {
                        var card = CardCatalog.Model(definition.Id);
                        bool expected = selected == misc
                            ? !definition.Pool && card.Rarity is not (CardRarity.Basic or CardRarity.Ancient)
                            : card.Rarity is CardRarity.Basic or CardRarity.Ancient || definition.Pool &&
                              card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;
                        if (visible.Contains(definition.Id) != expected)
                            throw new InvalidOperationException($"Incorrect visible collection ({(selected == misc ? "misc" : "Junna")}): {definition.Id}");
                        visibleChecks++;
                    }
                    visibleCollections.Add(new { collection = selected == misc ? "misc" : "Junna", count = visible.Count, cards = visible.Order().ToArray() });
                }
                for (int i = 0; i < CardCatalog.Cards.Count; i++)
                {
                    var card = CardCatalog.Model(CardCatalog.Cards[i].Id);
                    bool characterCard = card.Rarity is CardRarity.Basic or CardRarity.Ancient || CardCatalog.Cards[i].Pool &&
                        card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;
                    if (characterFilter(card) != characterCard) throw new InvalidOperationException("Incorrect character collection classification: " + card.Id);
                    if (card.Rarity is CardRarity.Token or CardRarity.Event && !filters[misc](card))
                        throw new InvalidOperationException("Special card missing from miscellaneous collection: " + card.Id);
                    if (!CardCatalog.Cards[i].Pool && card.Rarity is not (CardRarity.Basic or CardRarity.Ancient) && !filters[misc](card))
                        throw new InvalidOperationException("Associated card missing from miscellaneous collection: " + card.Id);
                    if (card.Rarity == CardRarity.Ancient && !filters[ancient](card))
                        throw new InvalidOperationException("Ancient card missing from ancient collection: " + card.Id);
                    if (card.Pool is not JunnaCardPool) throw new InvalidOperationException("Gameplay pool changed: " + card.Id);
                    if (characterFilter(card)) characterCards++;
                    if (filters[misc](card)) miscellaneousCards++;
                    if (filters[ancient](card)) ancientCards++;
                    classificationChecks++;
                }
            }
            finally
            {
                await library.ToSignal(library.GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
                library.Free();
            }
            passed = true;
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally
        {
            grid.Free();
            File.WriteAllText("card-library-regression.json", JsonSerializer.Serialize(new { passed, checkedCards, classificationChecks, visibleChecks, visibleCollections, characterCards, miscellaneousCards, ancientCards, error }));
            ((SceneTree)Engine.GetMainLoop()).Quit(passed ? 0 : 1);
        }
    }
}
