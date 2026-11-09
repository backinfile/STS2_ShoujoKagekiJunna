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
Check(relations.Count == 2, "Overlapping groups must merge transitively, leaving unrelated groups intact.");
Check(merged.CardCounts.Keys.ToHashSet().SetEquals(["JK126", "A", "B", "C", "D"]), "All members must share one group.");
Check(merged.EntranceId == "JK126" && merged.Members.First().Id == "JK126", "JK126 must remain the first and only entrance.");
Check(merged.CardCounts["D"] == 2 && merged.CardCounts["C"] == 1, "Merging must preserve group multiplicities without counting physical copies.");
merged = KnowledgeRelation.Merge(relations, new("X", new() { ["X"] = 1, ["D"] = 1 }), "JK126");
Check(relations.Count == 1 && merged.EntranceId == "JK126" && merged.CardCounts.ContainsKey("Y"), "A later merge must keep JK126 as entrance and include the complete other group.");
var restored = JsonSerializer.Deserialize<List<KnowledgeRelation>>(JsonSerializer.Serialize(relations))!;
Check(restored.Single().EntranceId == "JK126" && restored.Single().Members.SequenceEqual(merged.Members), "Run relations and counts must survive serialization.");
KnowledgeRelation.Merge(restored, new("JK126", new() { ["JK126"] = 1, ["A"] = 1 }), "JK126");
Check(restored.Count == 1 && restored.Single().CardCounts["D"] == 2, "Reacquiring an existing ID must not duplicate or shrink its group.");

Console.WriteLine($"PASS {checks} knowledge group checks, including counts, transitive ID relations, entrance priority and serialization.");
