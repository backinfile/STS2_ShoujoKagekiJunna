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

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

var relations = new List<KnowledgeRelation>
{
    new("C", new() { ["C"] = 1, ["D"] = 2 }),
    new("A", new() { ["A"] = 1, ["B"] = 1, ["C"] = 1 }),
    new("X", new() { ["X"] = 1, ["Y"] = 1 })
};
var merged = KnowledgeRelation.Merge(relations, new("JK126", new() { ["JK126"] = 1, ["A"] = 1 }), "JK126");
Check(relations.Count == 4, "A new source must preserve every other source list.");
Check(merged.CardCounts.Keys.ToHashSet().SetEquals(["JK126", "A"]), "Only direct associations belong to the source list.");
Check(merged.EntranceId == "JK126" && merged.Members.First().Id == "JK126", "Source remains first in its own list.");
Check(relations.Single(group => group.EntranceId == "C").CardCounts["D"] == 2, "Shared members preserve their own list and quantities.");
merged = KnowledgeRelation.Merge(relations, new("X", new() { ["X"] = 1, ["D"] = 1 }), "JK126");
Check(relations.Count == 4 && merged.EntranceId == "X" && merged.CardCounts.ContainsKey("Y") && merged.CardCounts.ContainsKey("D"), "Editing X must only merge X's own associations.");
var restored = JsonSerializer.Deserialize<List<KnowledgeRelation>>(JsonSerializer.Serialize(relations))!;
Check(restored.Single(group => group.EntranceId == "X").Members.SequenceEqual(merged.Members), "Independent lists and counts must survive serialization.");
KnowledgeRelation.Merge(restored, new("JK126", new() { ["JK126"] = 1, ["A"] = 1 }), "JK126");
Check(restored.Count == 4 && restored.Single(group => group.EntranceId == "C").CardCounts["D"] == 2, "Reacquiring a source must preserve other lists.");

Console.WriteLine($"PASS {checks} knowledge group checks, including quantities, one-way lists and serialization.");
