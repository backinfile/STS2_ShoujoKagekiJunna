namespace ShoujoKagekiJunna.Models;

public sealed record KnowledgeGroup(string MainCardId, string[] GrantedCardIds,
    Dictionary<string, int> GrantedCardCounts, int MainCardCount = 1)
{
    public IEnumerable<(string Id, int Count)> Members => GrantedCardIds.Prepend(MainCardId)
        .Select(id => (id, id == MainCardId ? MainCardCount : GrantedCardCounts.GetValueOrDefault(id, 1)));

    public static KnowledgeGroup[] Initialize(IEnumerable<KnowledgeGroup> definitions)
    {
        var originals = definitions.ToArray();
        var groups = originals.ToDictionary(group => group.MainCardId);
        var references = originals.SelectMany(group => group.GrantedCardIds
            .Where(id => group.GrantedCardCounts.GetValueOrDefault(id, 1) > 0).Distinct()
            .Select(id => (Id: id, Group: group))).GroupBy(link => link.Id);
        foreach (var reference in references)
        {
            if (groups.ContainsKey(reference.Key)) continue;
            var sources = reference.Select(link => link.Group).ToArray();
            var counts = sources.Length == 1
                ? sources[0].Members.ToDictionary(member => member.Id, member => member.Count)
                : sources.ToDictionary(source => source.MainCardId, _ => 1);
            counts.Remove(reference.Key, out int ownCount);
            groups.Add(reference.Key, new(reference.Key, counts.Keys.ToArray(), counts,
                ownCount > 0 ? ownCount : 1));
        }
        return groups.Values.ToArray();
    }
}
