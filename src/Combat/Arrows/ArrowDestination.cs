using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal sealed record ArrowDestination(Player Player, PileType Pile, CardPilePosition Position)
{
    private static readonly ConditionalWeakTable<CardModel, ArrowDestination> Destinations = new();

    internal static ArrowDestination Take(CardModel card, PileType fallback)
    {
        if (Destinations.TryGetValue(card, out var destination))
        {
            Destinations.Remove(card);
            return destination;
        }
        return new(card.Owner, fallback == ArrowPile.PileId ? PileType.Discard : fallback, CardPilePosition.Bottom);
    }

    internal Task Move(PlayerChoiceContext context, CardModel card)
    {
#if STS2_MAIN || STS2_BETA
        if (Player != card.Owner && Pile != PileType.None)
            return CardPileCmd.GiveToAnotherPlayer(card, Player, Pile, Position);
#endif
        if (Pile == PileType.None) return CardPileCmd.RemoveFromCombat(card);
        return CardPileCmd.Add(card, Pile, Position);
    }

#if STS2_MAIN || STS2_BETA
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardPlayResultLocation))]
    private static class Capture
    {
        private static void Postfix(CardModel card, CardLocation __result)
        {
            if (CardCatalog.IsArrow(card))
                Destinations.AddOrUpdate(card, new(__result.player, __result.pileType, __result.position));
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.GiveToAnotherPlayer))]
    private static class TransferAfterPlay
    {
        private static bool Prefix(CardModel card, Player player, PileType pileType, CardPilePosition position, ref Task __result)
        {
            if (card.Pile?.Type != ArrowPile.PileId || ArrowCombat.Find(card) is not { } arrow
                || arrow.Destination != new ArrowDestination(player, pileType, position)) return true;
            __result = Task.CompletedTask;
            return false;
        }
    }
#else
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardPlayResultPileTypeAndPosition))]
    private static class Capture
    {
        private static void Postfix(CardModel card, (PileType Pile, CardPilePosition Position) __result)
        {
            if (CardCatalog.IsArrow(card))
                Destinations.AddOrUpdate(card, new(card.Owner, __result.Pile, __result.Position));
        }
    }
#endif
}
