using System.Text.Json;
using ShoujoKagekiJunna.Models;

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
int checks = 0;

void CheckMembers(KnowledgeGroup group, params (string Id, int Count)[] expected)
{
    if (!group.Members.SequenceEqual(expected))
        throw new InvalidOperationException($"Unexpected members for {group.MainCardId}.");
    checks++;
}

var legacy = JsonSerializer.Deserialize<KnowledgeGroup>("""
    {"mainCardId":"A","grantedCardIds":["B","C"],"grantedCardCounts":{"B":2}}
    """, options)!;
CheckMembers(legacy, ("A", 1), ("B", 2), ("C", 1));
legacy.GrantedCardCounts["B"] = 9;
CheckMembers(legacy, ("A", 1), ("B", 9), ("C", 1));
CheckMembers(new("A", [], [], 3), ("A", 3));
CheckMembers(new("A", ["B"], new() { ["B"] = 0 }, 2), ("A", 2), ("B", 0));

using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "card-pool.json")));
foreach (var definition in catalog.RootElement.GetProperty("groups").EnumerateArray())
{
    var group = definition.Deserialize<KnowledgeGroup>(options)!;
    var expected = new List<(string Id, int Count)>
    {
        (definition.GetProperty("mainCardId").GetString()!,
            definition.TryGetProperty("mainCardCount", out var mainCount) ? mainCount.GetInt32() : 1)
    };
    foreach (var id in definition.GetProperty("grantedCardIds").EnumerateArray())
    {
        string memberId = id.GetString()!;
        var counts = definition.GetProperty("grantedCardCounts");
        expected.Add((memberId, counts.TryGetProperty(memberId, out var count) ? count.GetInt32() : 1));
    }
    CheckMembers(group, expected.ToArray());
}

Console.WriteLine($"PASS {checks} knowledge group checks, including legacy defaults, repeated main cards and live counts.");
