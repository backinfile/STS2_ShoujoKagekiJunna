using System.Text.Json;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaCompatibilityProbe;

public sealed class GenerationPoolCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_generation_pool";
    public override string Args => "check";
    public override string Description => "Check Junna random sources and explicit knowledge grants.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Junna combat required");
        var checks = new List<object>();
        void Check(string name, Func<bool> test)
        {
            try { checks.Add(new { name, pass = test() }); }
            catch (Exception error) { checks.Add(new { name, pass = false, error = error.ToString() }); }
        }
        bool Allowed(CardModel card) => card is JunnaCard junna && CardCatalog.Get(junna.DesignId).Pool
            && !CardCatalog.Get(junna.DesignId).ChoiceOnly
            && card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;
        var all = player.Character.CardPool.AllCards.ToArray();
        var unlocked = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).ToArray();
        Check("unlocked candidates exclude every non-pool and internal card", () => unlocked.All(Allowed));
        Check("direct combat filter excludes every non-pool and internal card", () => CardFactory.FilterForCombat(all).All(Allowed));
        Check("distinct combat generation excludes every non-pool and internal card", () =>
            CardFactory.GetDistinctForCombat(player, all, all.Length, new Rng(184)).All(Allowed));
        Check("repeated combat generation excludes every non-pool and internal card", () =>
            CardFactory.GetForCombat(player, all, 512, new Rng(184)).All(Allowed));
        Check("normal rewards exclude every non-pool and internal card", () =>
            CardFactory.CreateForReward(player, 3, CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool])).All(result => Allowed(result.Card)));
        Check("merchant candidates exclude every non-pool and internal card", () =>
            MegaCrit.Sts2.Core.Hooks.Hook.ModifyMerchantCardPool(player.RunState, player, all).All(Allowed));
        Check("merchant generation excludes singing companions", () => Enumerable.Range(0, 32).All(_ =>
            Allowed(CardFactory.CreateForMerchant(player, all, CardRarity.Rare).Card)));
        var original = player.RunState.CreateCard(CardCatalog.Model("JK184"), player);
        Check("default transformation excludes every non-pool and internal card", () =>
            CardFactory.GetDefaultTransformationOptions(original, false).All(Allowed));
        Check("custom transformation excludes singing companions", () => Enumerable.Range(0, 64).All(index =>
            Allowed(CardFactory.CreateRandomCardForTransform(original, all, false, new Rng((ulong)index)).CanonicalInstance)));
        Check("all three singing companions remain registered", () =>
            new[] { "JK184B", "JK184C", "JK184D" }.All(id => CardCatalog.Model(id).Rarity == CardRarity.Rare));
        Check("explicit knowledge group retains all six companions", () =>
            KnowledgeCombat.Group(original)!.Members.Sum(member => member.Count) == 7);
        string directory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/card-description-pool-20261110"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "generation-results.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        bool pass = checks.All(check => (bool)check.GetType().GetProperty("pass")!.GetValue(check)!);
        return new(pass, JsonSerializer.Serialize(checks));
    }
}
