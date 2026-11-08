using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Physical knowledge links are normalized by model lifecycle operations, never by UI reads.</summary>
internal static class KnowledgeRelations
{
    private static readonly SavedSpireField<CardModel, string> Links = new(() => null, "JunnaKnowledgeLinks");
    private static readonly SavedSpireField<Player, int> NextGroup = new(() => 0, "JunnaKnowledgeGroupSequence");
    private static readonly SavedSpireField<Player, int> Format = new(() => 0, "JunnaKnowledgeLinksFormat");
    internal static void Initialize() { _ = Links; _ = NextGroup; _ = Format; }
    internal static bool IsLinked(CardModel card) => !string.IsNullOrEmpty(Links.Get(card));
    private static string[] Groups(CardModel card) => Links.Get(card)?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];
    internal static IReadOnlyList<CardModel> Members(CardModel source)
    {
        if (!source.IsMutable || source.Owner == null || !IsLinked(source)) return [];
        var groups = Groups(source).ToHashSet(StringComparer.Ordinal);
        var cards = source.Pile?.IsCombatPile == true && source.Owner.PlayerCombatState != null
            ? source.Owner.PlayerCombatState.AllCards : source.Owner.Deck.Cards;
        var members = cards.Where(card => Groups(card).Any(groups.Contains)).ToArray();
        return members.Length > 0 ? members : [source];
    }
    internal static void Copy(CardModel source, CardModel copy) => Link(copy, Groups(source));
    internal static string CreateGroup(Player owner)
    {
        int sequence = checked(NextGroup.Get(owner) + 1);
        NextGroup.Set(owner, sequence);
        return owner.NetId + "/" + sequence;
    }
    internal static void Link(CardModel card, IEnumerable<string> groups)
    {
        var merged = Groups(card).Concat(groups).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (merged.Length == 0) return;
        Links.Set(card, string.Join("\n", merged));
        card.AddKeyword(Models.JunnaKeywords.Knowledge);
    }
    internal static void Normalize(Player player)
    {
        if (Format.Get(player) > 1) throw new InvalidDataException("Unsupported knowledge relation format.");
        NormalizeCards(player.Deck.Cards);
        if (player.PlayerCombatState is { } combat) NormalizeCards(combat.AllCards);
        string prefix = player.NetId + "/";
        int maximum = player.Deck.Cards.SelectMany(Groups).Where(group => group.StartsWith(prefix, StringComparison.Ordinal))
            .Select(group => int.TryParse(group.AsSpan(prefix.Length), out int sequence) ? sequence : 0).DefaultIfEmpty().Max();
        NextGroup.Set(player, Math.Max(NextGroup.Get(player), maximum));
        Format.Set(player, 1);
    }
    private static void NormalizeCards(IEnumerable<CardModel> cards)
    {
        var snapshot = cards.Distinct().ToArray();
        foreach (var research in snapshot.Where(card => KnowledgeProgress.ResearchGroup(card) != null)
                     .GroupBy(KnowledgeProgress.ResearchGroup, StringComparer.Ordinal))
        {
            var groups = research.SelectMany(Groups).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            foreach (var member in research) Link(member, groups);
        }
    }
    [HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
    private static class Restore
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(RunState __result)
        {
            foreach (var player in __result.Players) Normalize(player);
        }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.SyncWithSerializedPlayer))]
    private static class Synchronize
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(Player __instance) => Normalize(__instance);
    }
}
