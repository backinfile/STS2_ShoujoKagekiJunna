using System.Reflection;
using System.Text.Json;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

// Frozen from the reviewed workbench specification, separate from the runtime resource.
internal static class AuditExpectations
{
    private sealed record Snapshot(int Revision, CardDefinition[] Cards);
    private static readonly Snapshot Expected = Load();
    internal static IReadOnlyList<CardDefinition> Cards => Expected.Cards;
    private static Snapshot Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JunnaTests.CardPool")!;
        var snapshot = JsonSerializer.Deserialize<Snapshot>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (snapshot.Revision != CardCatalog.Revision || !snapshot.Cards.Select(c => c.Id).Order()
                .SequenceEqual(CardCatalog.Cards.Select(c => c.Id).Order()))
            throw new InvalidOperationException("Runtime pool and per-card effect expectations have diverged; review the workbench changes.");
        return snapshot;
    }
}
