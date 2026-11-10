namespace ShoujoKagekiJunna.Models;

/// <summary>One source model's run-wide direct associations and quantities.</summary>
public sealed record KnowledgeRelation(string EntranceId, Dictionary<string, int> CardCounts)
{
    public IEnumerable<(string Id, int Count)> Members => CardCounts.OrderBy(pair => pair.Key != EntranceId)
        .Select(pair => (pair.Key, pair.Value));

    public static KnowledgeRelation Merge(List<KnowledgeRelation> groups, KnowledgeRelation incoming, string preferredEntrance)
    {
        var counts = new Dictionary<string, int>(incoming.CardCounts);
        foreach (var group in groups.Where(group => group.EntranceId == incoming.EntranceId).ToArray())
        {
            foreach (var (id, count) in group.CardCounts)
                counts[id] = Math.Max(counts.GetValueOrDefault(id), count);
            groups.Remove(group);
        }
        var merged = new KnowledgeRelation(incoming.EntranceId, counts);
        groups.Add(merged);
        return merged;
    }
}
