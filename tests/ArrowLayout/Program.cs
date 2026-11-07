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

const float spacing = 108;
foreach (var (name, allyLeft, allyRight, left, right) in new[]
{
    ("normal", 530f, 530f, false, true),
    ("surrounded", 960f, 960f, true, true),
    ("left enemies only", 1400f, 1400f, true, false),
    ("allied formation surrounded", 780f, 1060f, true, true),
    ("near left edge", 180f, 180f, true, true),
    ("near right edge", 1720f, 1720f, true, true)
})
{
    var regions = ArrowLayout.Regions(1920, allyLeft, allyRight, left, spacing);
    var active = new[] { regions[(int)ArrowLane.FriendlyGroup], regions[(int)ArrowLane.FriendlySingle], regions[(int)ArrowLane.EnemyGroup] }
        .Concat(left ? [regions[(int)ArrowLane.Left]] : Array.Empty<Vector2>())
        .Concat(right ? [regions[(int)ArrowLane.Right]] : Array.Empty<Vector2>()).OrderBy(r => r.X).ToArray();
    foreach (var region in active)
        Require(region.X >= 1920 * .08f && region.Y <= 1920 * .9f && region.Y >= region.X,
            name + ": region stays inside visible canvas");
    for (int index = 1; index < active.Length; index++)
        Require(active[index].X - active[index - 1].Y >= spacing - .01f, name + ": regions remain separated");
    if (name is "normal" or "surrounded" or "left enemies only")
        Require(regions[(int)ArrowLane.FriendlySingle].Y < allyLeft, name + ": friendly arrows stay left of player");
    if (name == "surrounded")
        Require(regions[(int)ArrowLane.Left].Y < allyLeft && regions[(int)ArrowLane.Right].X > allyRight,
            "enemy singles stay on their target side");

    // Populate several rows: row wrapping must never put a group to the right of a single.
    Vector2[] Populate(ArrowLane lane)
    {
        var range = regions[(int)lane];
        int columns = Math.Max(1, (int)((range.Y - range.X) / spacing) + 1);
        const int count = 7;
        int rows = (count + columns - 1) / columns;
        var slots = Enumerable.Range(0, columns * rows)
            .Select(i => new Vector2(range.X + i % columns * spacing, i / columns * 140)).ToArray();
        return ArrowLayout.Assign(new Vector2?[count], slots).Select(i => slots[i]).ToArray();
    }
    var friendlyGroups = Populate(ArrowLane.FriendlyGroup);
    var friendlySingles = Populate(ArrowLane.FriendlySingle);
    var enemyGroups = Populate(ArrowLane.EnemyGroup);
    var enemySingles = (left ? Populate(ArrowLane.Left) : Array.Empty<Vector2>())
        .Concat(right ? Populate(ArrowLane.Right) : Array.Empty<Vector2>()).ToArray();
    Require(friendlyGroups.Max(p => p.X) < friendlySingles.Min(p => p.X), name + ": every ally group is left of every ally single");
    Require(enemyGroups.Max(p => p.X) < enemySingles.Min(p => p.X), name + ": every enemy group is left of every enemy single");
    Console.WriteLine("PASS: " + name + " formation and group-before-single order across rows");
}
Require(ArrowLayout.Lane(true, true, 400, 960) == ArrowLane.FriendlyGroup, "all allies use group region");
Require(ArrowLayout.Lane(true, false, 960, 960) == ArrowLane.FriendlySingle, "self uses friendly single region");
Require(ArrowLayout.Lane(false, true, 400, 960) == ArrowLane.EnemyGroup, "left enemy group uses group region");
Require(ArrowLayout.Lane(false, true, 1500, 960) == ArrowLane.EnemyGroup, "right enemy group uses group region");
Require(ArrowLayout.Lane(false, false, 400, 960) == ArrowLane.Left, "left enemy single");
Require(ArrowLayout.Lane(false, false, 1500, 960) == ArrowLane.Right, "right enemy single");
Console.WriteLine("PASS: target lanes preserve faction and group-before-single order.");

static void Require(bool condition, string name)
{
    if (!condition) throw new Exception(name);
}

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
