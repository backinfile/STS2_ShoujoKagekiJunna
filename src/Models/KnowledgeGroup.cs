namespace ShoujoKagekiJunna.Models;

public sealed record KnowledgeGroup(string MainCardId, string[] GrantedCardIds,
    Dictionary<string, int> GrantedCardCounts, int MainCardCount = 1)
{
    public IEnumerable<(string Id, int Count)> Members => GrantedCardIds.Prepend(MainCardId)
        .Select(id => (id, id == MainCardId ? MainCardCount : GrantedCardCounts.GetValueOrDefault(id, 1)));
}
