using ShoujoKagekiJunna.Combat;

int checks = 0;
void Check(bool passed, string name)
{
    if (!passed) throw new InvalidOperationException(name);
    checks++;
}
var expected = new[]
{
    ("JK148", new[] { "JK148B", "JK148C", "JK148D", "JK148E", "JK148F" }, ResearchCounter.ExtraHits, (int?)null, false),
    ("JK184", new[] { "JK184B", "JK184C", "JK184D" }, ResearchCounter.ExtraHits, (int?)6, true),
    ("JK314", new[] { "JK122B" }, ResearchCounter.DepletedArchive, (int?)null, true)
};
foreach (var (main, members, counter, cap, display) in expected)
{
    var rule = ResearchRules.ForMain(main)!;
    Check(rule != null && rule.Members.SequenceEqual(members), "Exact research family");
    Check(rule!.Counter == counter && rule.Maximum == cap && rule.ShowDepletedPartners == display, "Counter source, cap and display semantics");
    bool linked = counter == ResearchCounter.ExtraHits;
    Check(ResearchRules.NeedsLink(main) == linked && members.All(id => ResearchRules.NeedsLink(id) == linked),
        "Only linked research acquires a batch identity");
    Check(ResearchRules.ForPartner(main) == null && ResearchRules.Contains(main), "A main card never consumes its own research progress");
    foreach (string member in members)
        Check(ReferenceEquals(ResearchRules.ForPartner(member), rule) && ResearchRules.Contains(member), "Each physical partner maps to exactly one family");
    if (!linked) continue;
    Check(rule.Advance(0) == 1, "First depletion");
    Check(rule.Advance(cap ?? 100) == (cap ?? 101), "Capped or uncapped growth");
    Check(rule.Advance(-1) == 0, "Retain old arithmetic for a restored negative value");
    bool overflow = false;
    try { rule.Advance(int.MaxValue); } catch (OverflowException) { overflow = true; }
    Check(overflow, "Checked arithmetic before applying the cap");
}
foreach (string unrelated in new[] { "JK148A", "JK184Extra", "JK122", "JK314B", "JB01", "jk184b" })
    Check(!ResearchRules.Contains(unrelated) && ResearchRules.ForMain(unrelated) == null && ResearchRules.ForPartner(unrelated) == null,
        "Do not infer family membership from a prefix");
Console.WriteLine($"PASS {checks} research rule checks.");
