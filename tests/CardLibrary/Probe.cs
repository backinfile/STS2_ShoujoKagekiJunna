using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using ShoujoKagekiJunna.Models;

namespace JunnaCardLibraryRegression;

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    public static void Initialize() => new Harmony("junna.card-library.regression").Patch(
        AccessTools.Method(typeof(NMainMenu), "_Ready"), postfix: new HarmonyMethod(typeof(Probe), nameof(Check)));

    private static void Check()
    {
        bool passed = false;
        int checkedCards = 0;
        int classificationChecks = 0;
        int characterCards = 0, miscellaneousCards = 0, ancientCards = 0;
        string? error = null;
        var grid = new NCardLibraryGrid();
        try
        {
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
                for (int i = 0; i < CardCatalog.Cards.Count; i++)
                {
                    var card = CardCatalog.Model(CardCatalog.Cards[i].Id);
                    bool characterCard = card.Rarity == CardRarity.Basic || CardCatalog.Cards[i].Pool &&
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
            finally { library.Free(); }
            passed = true;
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally
        {
            grid.Free();
            File.WriteAllText("card-library-regression.json", JsonSerializer.Serialize(new { passed, checkedCards, classificationChecks, characterCards, miscellaneousCards, ancientCards, error }));
            ((SceneTree)Engine.GetMainLoop()).Quit(passed ? 0 : 1);
        }
    }
}
