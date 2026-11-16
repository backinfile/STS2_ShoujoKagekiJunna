using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Saves;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed class LibraryOptionsCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_library_fix";
    public override string Args => "check | show [query] | close";
    public override string Description => "Checks option visibility and discovered generated cards in the native library.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (args.FirstOrDefault() == "close")
        {
            MegaCrit.Sts2.Core.Nodes.Screens.Capstones.NCapstoneContainer.Instance?.Close();
            return new(true, "Closed native library");
        }
        if (args.FirstOrDefault() == "show" && player != null)
        {
            var capstone = MegaCrit.Sts2.Core.Nodes.NRun.Instance!.GlobalUi.SubmenuStack;
            capstone.ShowScreen(MegaCrit.Sts2.Core.Nodes.Screens.CapstoneSubmenuType.Compendium);
            var stack = capstone.Stack;
            var library = stack.GetSubmenuType<NCardLibrary>();
            library.Initialize(player.RunState);
            stack.Push(library);
            var misc = (NCardPoolFilter)AccessTools.Field(typeof(NCardLibrary), "_miscPoolFilter").GetValue(library)!;
            misc.IsSelected = true;
            var search = (MegaCrit.Sts2.Core.Nodes.CommonUi.NSearchBar)AccessTools.Field(typeof(NCardLibrary), "_searchBar").GetValue(library)!;
            search.TextArea.Text = args.ElementAtOrDefault(1) ?? "流星";
            AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter").Invoke(library, [misc]);
            return new(true, "Opened native library");
        }
        var rows = new List<object>();
        void Expect(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var grid = new NCardLibraryGrid();
        try
        {
            var pool = ModelDb.CardPool<JunnaCardPool>();
            var unlocks = SaveManager.Instance.GenerateUnlockStateFromProgress();
            var rewards = pool.GetUnlockedCards(unlocks, CardMultiplayerConstraint.None).Select(c => c.Id).ToHashSet();
            grid.RefreshVisibility();
            var visibility = AccessTools.Method(typeof(NCardLibraryGrid), "GetCardVisibility");
            foreach (var option in new CardModel[]
            {
                ModelDb.Card<JunnaArrowTargetSelf>(), ModelDb.Card<JunnaArrowTargetEnemy>(),
                ModelDb.Card<JunnaNotesDraw>(), ModelDb.Card<JunnaNotesEnergy>(),
                ModelDb.Card<JunnaPrepareArrows>(), ModelDb.Card<JunnaPrepareKnowledge>(),
                ModelDb.Card<JunnaEnergyNow>(), ModelDb.Card<JunnaEnergyLater>(),
                CardCatalog.Model("JK78D"), CardCatalog.Model("JK78E")
            }) Expect(option.Id.Entry + " hidden", option.ShouldShowInCardLibrary, false);
            foreach (var id in new[] { "JA81B", "JA119", "JB01", "JB02", "JK235B", "JK206B" })
            {
                var card = CardCatalog.Model(id);
                Expect(id + " discovered", SaveManager.Instance.Progress.DiscoveredCards.Contains(card.Id), true);
                Expect(id + " native visibility", visibility.Invoke(grid, [card])!.ToString()!, "Visible");
            }
            var seen = (HashSet<ModelId>)AccessTools.Field(typeof(NCardLibraryGrid), "_seenCards").GetValue(grid)!;
            var meteor = CardCatalog.Model("JA81B"); seen.Remove(meteor.Id);
            Expect("Undiscovered meteor native visibility", visibility.Invoke(grid, [meteor])!.ToString()!, "NotSeen");
            var after = pool.GetUnlockedCards(unlocks, CardMultiplayerConstraint.None).Select(c => c.Id).ToHashSet();
            Expect("Library inspection preserves reward eligibility", after.SetEquals(rewards), true);
            foreach (var id in new[] { "JA81B", "JA119", "JK235B", "JK206B", "JK78D", "JK78E" })
                Expect(id + " stays outside rewards", rewards.Contains(CardCatalog.Model(id).Id), false);
        }
        catch (Exception error) { rows.Add(new { name = "exception", pass = false, error = error.ToString() }); }
        finally
        {
            grid.Free();
            string path = Path.GetFullPath("../../../out/tmp/option-library/results.json", Environment.CurrentDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        return new(true, "Library checks completed");
    }
}
