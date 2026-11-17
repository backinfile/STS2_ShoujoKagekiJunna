using System.Text.Json;
using Godot;
using HarmonyLib;
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
            passed = true;
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally
        {
            grid.Free();
            File.WriteAllText("card-library-regression.json", JsonSerializer.Serialize(new { passed, checkedCards, error }));
            ((SceneTree)Engine.GetMainLoop()).Quit(passed ? 0 : 1);
        }
    }
}
