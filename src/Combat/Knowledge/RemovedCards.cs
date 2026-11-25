using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShoujoKagekiJunna.Combat;

internal static class RemovedCards
{
    private static readonly HashSet<string> ModelEntries = LoadEntries();

    private static HashSet<string> LoadEntries()
    {
        using var stream = typeof(RemovedCards).Assembly.GetManifestResourceStream("Junna.RemovedCardModels")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!.Values.ToHashSet(StringComparer.Ordinal);
    }

    internal static bool Contains(ModelId? id) => id?.Category == "CARD"
        && ModelEntries.Contains(id.Entry);

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
    {
        save.Deck.RemoveAll(card => Contains(card.Id));
        save.DiscoveredCards.RemoveAll(Contains);
    }
}
