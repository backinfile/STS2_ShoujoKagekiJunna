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

var attackIntent = new Rect2(1508, 221, 66, 66);
var firstRow = new[] { new Rect2(1410, 160, 48, 78), new Rect2(1517, 160, 48, 78) };
var secondRow = firstRow.Select(arrow => new Rect2(arrow.Position + Vector2.Down * 96, arrow.Size)).ToArray();
float upperOffset = ArrowLayout.ClearIntentRow(firstRow, [attackIntent], -24);
float lowerOffset = ArrowLayout.ClearIntentRow(secondRow, [attackIntent], upperOffset);
Require(upperOffset == -17 && lowerOffset == 31, "boss intent splits two rows without changing horizontal slots");
Require(ArrowLayout.ClearIntentRow([firstRow[0]], [attackIntent], -24) == 0, "arrows beside an intent stay still");
Require(ArrowLayout.ClearIntentRow(secondRow, [new Rect2(1510, 344, 64, 52), attackIntent], upperOffset) == 140,
    "moving below one intent also clears a second intent");
Console.WriteLine("PASS: dense formation leaves native intent space clear.");

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

// Dense scenes exercise the actual occupied-lane allocator, including a single crowded group.
foreach (var (name, allyLeft, allyRight, left, counts) in new[]
{
    ("48 enemy singles at live formation", 482.5f, 482.5f, false, new[] { 0, 0, 0, 0, 48 }),
    ("45 mixed arrows at live formation", 482.5f, 482.5f, false, new[] { 0, 0, 14, 0, 31 }),
    ("42 enemy singles", 530f, 530f, false, new[] { 0, 0, 0, 0, 42 }),
    ("42 enemy groups", 530f, 530f, false, new[] { 0, 0, 42, 0, 0 }),
    ("mixed enemy arrows", 530f, 530f, false, new[] { 0, 0, 12, 0, 30 }),
    ("friendly groups reuse empty lane", 530f, 530f, false, new[] { 12, 0, 0, 0, 0 }),
    ("friendly singles reuse empty lane", 530f, 530f, false, new[] { 0, 12, 0, 0, 0 }),
    ("all five lanes", 960f, 960f, true, new[] { 3, 3, 9, 12, 15 }),
    ("left edge", 180f, 180f, true, new[] { 2, 3, 7, 11, 19 }),
    ("right edge", 1720f, 1720f, true, new[] { 2, 3, 7, 11, 19 })
})
{
    const float denseSpacing = 80;
    var regions = ArrowLayout.OccupiedRegions(1920, allyLeft, allyRight, left, denseSpacing, counts);
    var points = new Dictionary<ArrowLane, Vector2[]>();
    foreach (var lane in Enum.GetValues<ArrowLane>().Where(lane => counts[(int)lane] > 0))
    {
        var range = regions[(int)lane];
        Require(range.X >= 1920 * .08f && range.Y <= 1920 * .9f && range.Y >= range.X,
            name + ": occupied region stays on screen");
        int columns = Math.Max(1, (int)MathF.Round((range.Y - range.X) / denseSpacing) + 1);
        int rows = (counts[(int)lane] + columns - 1) / columns;
        var slots = Enumerable.Range(0, columns * rows)
            .Select(i => new Vector2(range.X + i % columns * denseSpacing, 188 + i / columns * 96)).ToArray();
        points[lane] = ArrowLayout.Assign(new Vector2?[counts[(int)lane]], slots).Select(i => slots[i]).ToArray();
        if (name is not ("left edge" or "right edge"))
            Require(points[lane].Max(p => p.Y) <= 380, name + ": fits in three overhead rows");
    }
    var all = points.Values.SelectMany(p => p).ToArray();
    for (int i = 0; i < all.Length; i++)
        for (int j = i + 1; j < all.Length; j++)
            Require(all[i].DistanceTo(all[j]) >= denseSpacing - .01f, name + ": icons retain clearance");
    void GroupBeforeSingle(ArrowLane group, params ArrowLane[] singles)
    {
        if (!points.TryGetValue(group, out var groups)) return;
        var singlePoints = singles.Where(points.ContainsKey).SelectMany(lane => points[lane]).ToArray();
        if (singlePoints.Length > 0)
            Require(groups.Max(p => p.X) < singlePoints.Min(p => p.X), name + ": group stays left of singles");
    }
    GroupBeforeSingle(ArrowLane.FriendlyGroup, ArrowLane.FriendlySingle);
    GroupBeforeSingle(ArrowLane.EnemyGroup, ArrowLane.Left, ArrowLane.Right);
    Console.WriteLine("PASS: " + name + " density, clearance and ordering");
}

foreach (var (count, expectedGap) in new[] { (7, 80f), (8, 72f), (10, 64f), (12, 56f), (18, 36f), (27, 22.5f), (28, 21f), (64, 21f) })
{
    Require(Math.Abs(ArrowLayout.ClusterGap(count, 80, 21) - expectedGap) < .01f, $"gap at {count} copies");
}
float looseRowGap = ArrowLayout.ClusterGap(8, 96, 36.4f);
var narrow = ArrowLayout.Pack(100, looseRowGap, [0, 72, 72, 72]);
Require(Math.Abs(narrow[2].Y - 86.4f) < .01f && narrow[3].X == 72, "loose groups keep wider row spacing");
Console.WriteLine("PASS: cluster gap follows the member count in both directions");

foreach (int count in new[] { 2, 57, 58, 59, 64, 128 })
{
    var points = ArrowLayout.Pack(1200, 36.4f, Enumerable.Repeat(21f, count).ToArray());
    Require(points.Length == count && points.Distinct().Count() == count, "packed copies stay distinct");
    Require(points.GroupBy(p => p.Y).Count() == (count + 57) / 58, "actual spacing determines row capacity");
    foreach (var row in points.GroupBy(p => p.Y).SkipLast(1))
        Require(row.Count() == 58 && row.Max(p => p.X) + 21 > 1200, "every nonfinal row is full");
    Require(points.All(p => p.X >= 0 && p.X <= 1200), "packed arrows stay inside their region");
    Console.WriteLine($"PASS: {count} Stardust arrows fill each row before wrapping");
}
var edgeFit = ArrowLayout.Pack(1225.5f, 36.4f, Enumerable.Repeat(21f, 60).ToArray());
Require(edgeFit.Take(59).All(p => p.Y == 0) && edgeFit[59].Y == 36.4f,
    "reuse the fraction left beyond the old 80-unit grid");
Console.WriteLine("PASS: leftover region width is used before wrapping");
var mixed = ArrowLayout.Pack(234, 96, [0, 21, 21, 80, 56, 56]);
Require(mixed.Take(5).All(p => p.Y == 0) && mixed[3].X == 122 && mixed[4].X == 178 && mixed[5].X == 234,
    "adjacent clusters use their own gap and keep ordinary clearance between them");
var wrapped = ArrowLayout.Pack(200, 96, [0, 21, 21, 80, 56, 56, 56]);
Require(wrapped[4].Y == 0 && wrapped[5].Y == 96 && wrapped[6].X == 56, "mixed gaps wrap at their actual widths");
Console.WriteLine("PASS: mixed arrow groups keep their own gap and wrap at their actual widths");

var ordinary = Enumerable.Repeat(80f, 27).ToArray();
var ordinaryMinimums = Enumerable.Repeat(42f, ordinary.Length).ToArray();
ArrowLayout.FitTwoRows(960, ordinary, ordinaryMinimums);
Require(Math.Abs(ordinary[1] - 960f / 13) < .02f && ArrowLayout.Pack(960, 96, ordinary).Max(point => point.Y) == 96,
    "ordinary arrows tighten just enough to stay on two rows");
var alreadyFits = Enumerable.Repeat(80f, 26).ToArray();
ArrowLayout.FitTwoRows(960, alreadyFits, ordinaryMinimums);
Require(alreadyFits.All(gap => gap == 80), "two rows that already fit retain their spacing");
var mixedPressure = Enumerable.Range(0, 14).Select(i => i <= 7 ? 72f : 80f).ToArray();
var mixedMinimums = Enumerable.Range(0, 14).Select(i => i <= 7 ? 21f : 42f).ToArray();
ArrowLayout.FitTwoRows(320, mixedPressure, mixedMinimums);
Require(mixedPressure[1] < 72 && mixedPressure[8] < 80 && ArrowLayout.Pack(320, 96, mixedPressure).Max(point => point.Y) == 96,
    "a crowded lane tightens both within clusters and between different arrows");
var overflow = Enumerable.Repeat(80f, 49).ToArray();
ArrowLayout.FitTwoRows(960, overflow, Enumerable.Repeat(42f, overflow.Length).ToArray());
Require(overflow.Skip(1).All(gap => gap == 42) && ArrowLayout.Pack(960, 96, overflow).Max(point => point.Y) == 192,
    "a third row opens once minimum spacing cannot fit the arrows");
Console.WriteLine("PASS: two-row fitting preserves spacing until needed and respects the minimum");

Vector2?[] beforeCluster = [new(560, 0), new(800, 0), new(880, 0), new(960, 0), new(1040, 0), new(1120, 0), new(1200, 0), null];
var afterCluster = ArrowLayout.Pack(1225.5f, 86.4f, Enumerable.Repeat(72f, 8).ToArray(), beforeCluster);
float clusterTravel = beforeCluster.Take(7).Select((old, i) => old!.Value.DistanceTo(afterCluster[i])).Sum();

var twoFullRows = Enumerable.Range(0, 32).Select(i => (Vector2?)new Vector2(i % 16 * 80, i / 16 * 96)).Append(null).ToArray();
var newcomerGaps = Enumerable.Repeat(80f, 33).ToArray();
ArrowLayout.FitTwoRows(1225.5f, newcomerGaps, Enumerable.Repeat(42f, 33).ToArray());
var afterNewcomer = ArrowLayout.Pack(1225.5f, 96, newcomerGaps, twoFullRows);
int changedRows = twoFullRows.Take(32).Select((old, i) => old!.Value.Y != afterNewcomer[i].Y).Count(changed => changed);
Console.WriteLine($"7→8 existing-arrow travel: {clusterTravel:F1}; 32→33 existing arrows changing rows: {changedRows}");
Require(clusterTravel < 400 && changedRows == 0, "existing arrows stay close to their slots as the gaps contract");

var survivors = new Vector2?[] { new(120, 0), new(282, 0), new(96, 96), new(176, 96) };
var afterRemoval = ArrowLayout.Pack(420, 86.4f, [0, 72, 72, 72], survivors);
Require(afterRemoval.Select(p => (Vector2?)p).SequenceEqual(survivors),
    "removal and changing density leave every surviving arrow at its existing slot");

var addingCopies = survivors.Concat(new Vector2?[4]).ToArray();
var afterCopies = ArrowLayout.Pack(420, 86.4f, Enumerable.Repeat(42f, 8).ToArray(), addingCopies);
Require(afterCopies.Take(survivors.Length).Select(p => (Vector2?)p).SequenceEqual(survivors)
    && afterCopies.Distinct().Count() == afterCopies.Length,
    "new copies use free positions without moving settled arrows");

int[] nearFitXs = [0, 48, 128, 176];
var nearFit = Enumerable.Range(0, 8)
    .Select(i => (Vector2?)new Vector2(nearFitXs[i % 4], i / 4 * 96))
    .Append(null).ToArray();
var nearFitPreferred = nearFit.Select(point => point ?? new Vector2(88, 0)).ToArray();
var nudged = ArrowLayout.KeepSlots(182, 96, nearFit, nearFitPreferred, (_, _) => 42);
Require(nudged != null && nudged.Take(8).Where((point, i) => point != nearFit[i]).Count() == 1
    && nudged.Take(8).Select((point, i) => point.DistanceTo(nearFit[i]!.Value)).Max() == 4
    && nudged.GroupBy(point => point.Y).All(row => row.OrderBy(point => point.X)
        .Zip(row.OrderBy(point => point.X).Skip(1)).All(pair => pair.Second.X - pair.First.X >= 42)),
    "a four-unit nudge opens the last usable gap without rearranging the row");
float?[] usedNudgeBudget = [0, 56, 120, 176, 0, 56, 120, 176, null];
Require(ArrowLayout.KeepSlots(182, 96, nearFit, nearFitPreferred, (_, _) => 42, usedNudgeBudget) == null,
    "repeated inserts cannot push a settled arrow past its original eight-unit allowance");

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
