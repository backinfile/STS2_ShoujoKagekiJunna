using Godot;
using ShoujoKagekiJunna.Combat;

var cases = new (string Name, int?[] Previous, int Capacity)[]
{
    ("insert into free gap", [1, 3, null, 6], 8),
    ("insert before first arrow", [null, 0, 2, 5], 8),
    ("insert into full gap", [0, null, 1, 4], 6),
    ("target change reorders arrows", [5, 1, 6], 8),
    ("removal preserves positions", [1, 6], 8),
    ("initial placement", [null, null, null], 6),
    ("full row", [0, null, 1, 2], 4)
};
foreach (var (name, previous, capacity) in cases)
{
    Check(name, previous, Enumerable.Range(0, capacity).Select(i => new Vector2(i * 108, 0)).ToArray());
}
Check("multiple rows", [0, null, 1, 4], Enumerable.Range(0, 6).Select(i => new Vector2(i % 3 * 108, i / 3 * 140)).ToArray());
Console.WriteLine("PASS: 8 ordered layout cases match the exhaustive minimum of moved arrows and travel distance.");

static void Check(string name, int?[] oldSlots, Vector2[] slots)
{
    var previous = oldSlots.Select(index => index is { } i ? (Vector2?)slots[i] : null).ToArray();
    var result = ArrowLayout.Assign(previous, slots);
    if (result.Length != previous.Length || result.Distinct().Count() != result.Length || !result.SequenceEqual(result.Order()))
        throw new Exception(name + ": ordering or slot collision");
    (int Moved, double Travel) Score(int[] assignment)
    {
        int moved = 0;
        double travel = 0;
        for (int i = 0; i < previous.Length; i++)
            if (previous[i] is { } point)
            {
                float distance = point.DistanceTo(slots[assignment[i]]);
                if (distance > .5) moved++;
                travel += distance;
            }
        return (moved, travel);
    }
    var actual = Score(result);
    var best = (Moved: int.MaxValue, Travel: double.PositiveInfinity);
    var candidate = new int[previous.Length];
    void Enumerate(int index, int start)
    {
        if (index == candidate.Length)
        {
            var score = Score(candidate);
            if (score.CompareTo(best) < 0) best = score;
            return;
        }
        for (int slot = start; slot <= slots.Length - candidate.Length + index; slot++)
        {
            candidate[index] = slot;
            Enumerate(index + 1, slot + 1);
        }
    }
    Enumerate(0, 0);
    if (actual.Moved != best.Moved || Math.Abs(actual.Travel - best.Travel) > .001)
        throw new Exception($"{name}: actual {actual}, optimal {best}");
    Console.WriteLine($"PASS: {name}: moved={actual.Moved}, distance={actual.Travel:0.##}");
}
