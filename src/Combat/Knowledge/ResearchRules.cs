namespace ShoujoKagekiJunna.Combat;

internal enum ResearchCounter { ExtraHits, DepletedPartners }

internal sealed record ResearchRule(string MainCardId, string[] Members, ResearchCounter Counter,
    int? Maximum = null, bool ShowDepletedPartners = false)
{
    internal int Advance(int current)
    {
        int next = checked(current + 1);
        return Maximum is { } maximum ? Math.Min(maximum, next) : next;
    }
}

/// <summary>Research families retain their original saved fields and depletion semantics.</summary>
internal static class ResearchRules
{
    internal static readonly ResearchRule[] All =
    [
        new("JK148", ["JK148B", "JK148C", "JK148D", "JK148E", "JK148F"], ResearchCounter.ExtraHits),
        new("JK184", ["JK184B", "JK184C", "JK184D"], ResearchCounter.ExtraHits, 6, true),
        new("JK314", ["JK122B"], ResearchCounter.DepletedPartners, 3, true)
    ];
    private static readonly Dictionary<string, ResearchRule> Mains = All.ToDictionary(rule => rule.MainCardId, StringComparer.Ordinal);
    private static readonly Dictionary<string, ResearchRule> Partners = All.SelectMany(rule =>
        rule.Members.Select(id => (Id: id, Rule: rule))).ToDictionary(pair => pair.Id, pair => pair.Rule, StringComparer.Ordinal);

    internal static ResearchRule? ForMain(string id) => Mains.GetValueOrDefault(id);
    internal static ResearchRule? ForPartner(string id) => Partners.GetValueOrDefault(id);
    internal static bool Contains(string id) => Mains.ContainsKey(id) || Partners.ContainsKey(id);
}
