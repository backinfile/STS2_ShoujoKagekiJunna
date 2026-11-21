using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShoujoKagekiJunna.Combat;

internal static class RemovedKnowledgeCards
{
    internal static bool Contains(ModelId? id) => id?.Category == "CARD"
        && id.Entry is "JUNNA_JK359" or "JUNNA_JK360" or "JUNNA_JK361" or "JUNNA_JK362" or "JUNNA_JK363"
            or "JUNNA_JK364" or "JUNNA_JK365" or "JUNNA_JK366" or "JUNNA_JK367";

    [HarmonyPatch(typeof(Player), nameof(Player.FromSerializable))]
    private static class LoadPlayer
    {
        private static void Prefix(SerializablePlayer save) => RemoveCards(save);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.SyncWithSerializedPlayer))]
    private static class SyncPlayer
    {
        private static void Prefix(SerializablePlayer player) => RemoveCards(player);
    }

    private static void RemoveCards(SerializablePlayer save)
        => save.Deck.RemoveAll(card => Contains(card.Id));
}
