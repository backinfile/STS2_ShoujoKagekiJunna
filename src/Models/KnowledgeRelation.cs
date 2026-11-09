namespace ShoujoKagekiJunna.Models;

/// <summary>A run-wide group of model IDs, independent of physical card instances.</summary>
public sealed record KnowledgeRelation(string EntranceId, Dictionary<string, int> CardCounts)
{
    public IEnumerable<(string Id, int Count)> Members => CardCounts.OrderBy(pair => pair.Key != EntranceId)
        .Select(pair => (pair.Key, pair.Value));

    public static KnowledgeRelation Merge(List<KnowledgeRelation> groups, KnowledgeRelation incoming, string preferredEntrance)
    {
        var counts = new Dictionary<string, int>(incoming.CardCounts);
        string entrance = incoming.EntranceId;
        bool changed;
        do
        {
            changed = false;
            foreach (var group in groups.ToArray())
            {
                if (!group.CardCounts.Keys.Any(counts.ContainsKey)) continue;
                if (entrance != preferredEntrance) entrance = group.EntranceId;
                foreach (var (id, count) in group.CardCounts)
                    counts[id] = Math.Max(counts.GetValueOrDefault(id), count);
                groups.Remove(group);
                changed = true;
            }
        } while (changed);
        if (counts.ContainsKey(preferredEntrance)) entrance = preferredEntrance;
        var merged = new KnowledgeRelation(entrance, counts);
        groups.Add(merged);
        return merged;
    }
}
