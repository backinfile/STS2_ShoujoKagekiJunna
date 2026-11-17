using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class BurningSticksProbe : AbstractConsoleCmd
{
    private static CardModel? _permanent, _source;
    private static int _remaining;
    public override string CmdName => "junna_burning_sticks";
    public override string Args => "prepare <remaining> [upgrade] | inspect";
    public override string Description => "Check native Burning Sticks copying the last Shine of JF13.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "inspect") { Save(player); return new(true, "Snapshot saved"); }
        return new(Prepare(player, int.Parse(args[1]), args.Contains("upgrade")), true, "Preparing Burning Sticks fixture");
    }
    private static async Task Prepare(Player player, int remaining, bool upgraded)
    {
        foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
        { ArrowCombat.Cancel(card); if (card.Pile != null) await CardPileCmd.RemoveFromCombat(card, skipVisuals: false); }
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
        await RelicCmd.Obtain(ModelDb.Relic<BurningSticks>().ToMutable(), player);
        await CardPileCmd.RemoveFromDeck(player.Deck.Cards.ToArray(), showPreview: false);
        using var single = KnowledgeCombat.SingleCard();
        _remaining = remaining;
        _permanent = player.RunState.CreateCard(CardCatalog.Model("JF13"), player);
        if (upgraded) CardCmd.Upgrade(_permanent);
        Shine.SetState(_permanent, remaining, 6);
        await CardPileCmd.Add(_permanent, PileType.Deck, skipVisuals: true);
        _source = player.Creature.CombatState!.CloneCard(_permanent);
        _source.DeckVersion = _permanent;
        await CardPileCmd.Add(_source, PileType.Hand, skipVisuals: false);
        var draw = player.Creature.CombatState.CreateCard<DefendIronclad>(player);
        await CardPileCmd.Add(draw, PileType.Draw, CardPilePosition.Bottom, skipVisuals: true);
        player.PlayerCombatState.Energy = 3;
        Save(player);
    }
    private static void Save(Player player)
    {
        var directory = Path.GetFullPath("../../../out/tmp/burning-sticks", System.Environment.CurrentDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "live.json"), JsonSerializer.Serialize(new
        {
            initialRemaining = _remaining, upgraded = _source?.IsUpgraded, block = player.Creature.Block,
            sourceRemaining = _source == null ? null : Shine.GetState(_source)?.Remaining,
            sourcePile = _source?.Pile?.Type.ToString(), sourceRemoved = _source?.HasBeenRemovedFromState,
            permanentRemoved = _permanent?.HasBeenRemovedFromState,
            depleted = Shine.GetDepletedCards(player).Select(c => c.ModelId),
            relicUsed = AccessTools.Field(typeof(BurningSticks), "_wasUsedThisCombat").GetValue(player.Relics.OfType<BurningSticks>().Single()),
            hand = player.PlayerCombatState!.Hand.Cards.Select(c => new
            { id = c.Id.Entry, c.IsUpgraded, remaining = Shine.GetState(c)?.Remaining }),
            exhaust = player.PlayerCombatState.ExhaustPile.Cards.Select(c => c.Id.Entry)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
