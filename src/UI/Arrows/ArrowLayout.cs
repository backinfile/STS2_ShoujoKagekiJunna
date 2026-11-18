using Godot;

namespace ShoujoKagekiJunna.Combat;

internal enum ArrowLane { FriendlyGroup, FriendlySingle, EnemyGroup, Left, Right }

/// <summary>Target regions and ordered slots, preserving existing arrows before balancing new arrows.</summary>
internal static class ArrowLayout
{
    internal const int MinimumClusterSize = 8;

    // Group regions stay horizontally left of every single-target region in the same faction.
    // Overflow grows down within each region, preserving that rule across multiple rows.
    internal static Vector2[] Regions(float width, float allyLeft, float allyRight,
        bool enemiesLeft, float spacing)
    {
        float min = width * .08f, max = width * .9f;
        float end = Mathf.Clamp(enemiesLeft ? allyRight - spacing * .5f : allyLeft - spacing * .75f,
            min + spacing * (enemiesLeft ? 4 : 2), max - spacing * 3);
        float start = Mathf.Clamp(enemiesLeft ? allyLeft - spacing * 1.5f : end - spacing * 2,
            min + (enemiesLeft ? spacing * 3 : 0), end - spacing);
        var enemyGroup = enemiesLeft ? new Vector2(min, min + spacing)
            : new Vector2(end + spacing, end + spacing * 2);
        return [new(start, start), new(start + spacing, end), enemyGroup,
            new(min + spacing * 2, Math.Max(min + spacing * 2, start - spacing)),
            new(enemiesLeft ? end + spacing : enemyGroup.Y + spacing, max)];
    }

    // Share the available columns between occupied lanes instead of reserving empty lanes.
    internal static Vector2[] OccupiedRegions(float width, float allyLeft, float allyRight,
        bool enemiesLeft, float spacing, IReadOnlyList<int> counts)
    {
        var regions = Regions(width, allyLeft, allyRight, enemiesLeft, spacing);
        float min = width * .08f, max = width * .9f;
        float friendlyStart = regions[(int)ArrowLane.FriendlyGroup].X;
        float friendlyEnd = regions[(int)ArrowLane.FriendlySingle].Y;
        if (enemiesLeft)
        {
            Share(min, friendlyStart - spacing, ArrowLane.EnemyGroup, ArrowLane.Left);
            Share(friendlyStart, friendlyEnd, ArrowLane.FriendlyGroup, ArrowLane.FriendlySingle);
            Share(friendlyEnd + spacing, max, ArrowLane.Right);
        }
        else
        {
            Share(min, friendlyEnd, ArrowLane.FriendlyGroup, ArrowLane.FriendlySingle);
            Share(friendlyEnd + spacing, max, ArrowLane.EnemyGroup, ArrowLane.Right);
        }
        return regions;

        void Share(float start, float end, params ArrowLane[] lanes)
        {
            var active = lanes.Where(lane => counts[(int)lane] > 0).ToArray();
            if (active.Length == 0) return;
            int capacity = Math.Max(active.Length, (int)((end - start) / spacing) + 1);
            var columns = Enumerable.Repeat(1, active.Length).ToArray();
            // Give the next column to the lane with the greatest rows-per-column pressure.
            for (int extra = active.Length; extra < capacity; extra++)
            {
                int next = 0;
                for (int i = 1; i < active.Length; i++)
                    if ((double)counts[(int)active[i]] / columns[i] > (double)counts[(int)active[next]] / columns[next]) next = i;
                columns[next]++;
            }
            for (int i = 0; i < active.Length; i++)
            {
                regions[(int)active[i]] = new(start, start + (columns[i] - 1) * spacing);
                start += columns[i] * spacing;
            }
        }
    }

    internal static ArrowLane Lane(bool friendly, bool group, float targetX, float allyCenter)
        => friendly ? group ? ArrowLane.FriendlyGroup : ArrowLane.FriendlySingle
            : group ? ArrowLane.EnemyGroup : targetX < allyCenter ? ArrowLane.Left : ArrowLane.Right;

    // Blend between count breakpoints so adding one arrow does not jump to the next density.
    internal static float ClusterGap(int count, float full, float compact)
    {
        float loose = full * .9f, medium = full * .7f, tight = full * .45f;
        return count switch
        {
            < MinimumClusterSize => full,
            < 12 => Mathf.Lerp(loose, medium, (count - MinimumClusterSize) / (12f - MinimumClusterSize)),
            < 18 => Mathf.Lerp(medium, tight, (count - 12) / 6f),
            < 28 => Mathf.Lerp(tight, compact, (count - 18) / 10f),
            _ => compact
        };
    }

    internal static void FitTwoRows(float width, float[] gaps, float[] minimumGaps)
    {
        if (Fits(1)) return;

        // Use the widest gaps that fit. If even the minimum needs more rows, leave the remaining wrapping to Pack.
        float low = 0, high = 1;
        for (int i = 0; i < 12; i++)
        {
            float scale = (low + high) * .5f;
            if (Fits(scale)) low = scale;
            else high = scale;
        }
        for (int i = 1; i < gaps.Length; i++)
            gaps[i] = Mathf.Lerp(minimumGaps[i], gaps[i], low);

        bool Fits(float scale)
        {
            float x = 0;
            bool secondRow = false;
            for (int i = 1; i < gaps.Length; i++)
            {
                float next = x + Mathf.Lerp(minimumGaps[i], gaps[i], scale);
                if (next > width + .01f)
                {
                    if (secondRow) return false;
                    secondRow = true;
                    x = 0;
                }
                else x = next;
            }
            return true;
        }
    }

    // A new row starts only when the next arrow, at its actual spacing, no longer fits.
    internal static Vector2[] Pack(float width, float rowSpacing, IReadOnlyList<float> gaps,
        IReadOnlyList<Vector2?>? previous = null)
    {
        var result = new Vector2[gaps.Count];
        if (result.Length == 0) return result;

        var offsets = new float[result.Length];
        var rowStarts = new List<int> { 0 };
        for (int i = 1; i < result.Length; i++)
        {
            offsets[i] = offsets[i - 1] + gaps[i];
            if (offsets[i] - offsets[rowStarts[^1]] > width + .01f) rowStarts.Add(i);
        }

        if (rowStarts.Count == 2 && previous != null)
        {
            // If either row can take the newcomer, choose the split with fewer existing arrows changing rows.
            int changes = previous.Count(point => point is { Y: <= .5f });
            int bestChanges = int.MaxValue;
            int lastSplit = rowStarts[1];
            for (int split = 1; split <= lastSplit; split++)
            {
                if (previous[split - 1] is { } old) changes += old.Y <= .5f ? -1 : 1;
                if (offsets[^1] - offsets[split] > width + .01f || changes > bestChanges) continue;
                rowStarts[1] = split;
                bestChanges = changes;
            }
        }

        var shifts = new List<float>();
        for (int row = 0; row < rowStarts.Count; row++)
        {
            int start = rowStarts[row];
            int end = row + 1 < rowStarts.Count ? rowStarts[row + 1] : result.Length;
            float free = Math.Max(0, width - (offsets[end - 1] - offsets[start]));
            float shift = rowStarts.Count == 1 ? free * .5f : 0;
            if (previous != null)
            {
                shifts.Clear();
                for (int i = start; i < end; i++)
                    if (previous[i] is { } old) shifts.Add(old.X - (offsets[i] - offsets[start]));
                if (shifts.Count > 0)
                {
                    // Any median shift minimizes this row's total horizontal travel.
                    shifts.Sort();
                    shift = Math.Clamp(shift, shifts[(shifts.Count - 1) / 2], shifts[shifts.Count / 2]);
                    shift = Math.Clamp(shift, 0, free);
                }
            }
            for (int i = start; i < end; i++)
                result[i] = new(offsets[i] - offsets[start] + shift, row * rowSpacing);
        }
        return result;
    }

    private readonly record struct Cost(int Moved, double Travel, double Balance) : IComparable<Cost>
    {
        public int CompareTo(Cost other)
        {
            int result = Moved.CompareTo(other.Moved);
            if (result == 0) result = Travel.CompareTo(other.Travel);
            return result == 0 ? Balance.CompareTo(other.Balance) : result;
        }
        internal static Cost Impossible => new(int.MaxValue, double.PositiveInfinity, double.PositiveInfinity);
    }

    internal static int[] Assign(IReadOnlyList<Vector2?> previous, IReadOnlyList<Vector2> slots)
    {
        int count = previous.Count, capacity = slots.Count;
        if (count == 0) return [];
        if (capacity < count) throw new ArgumentException("Not enough arrow slots", nameof(slots));
        var parents = new int[count, capacity];
        var costs = Enumerable.Repeat(Cost.Impossible, capacity).ToArray();
        for (int index = 0; index < count; index++)
        {
            var next = Enumerable.Repeat(Cost.Impossible, capacity).ToArray();
            var best = index == 0 ? new Cost(0, 0, 0) : Cost.Impossible;
            int parent = -1;
            double preferred = (index + 1d) * (capacity + 1) / (count + 1) - 1;
            for (int slot = 0; slot < capacity; slot++)
            {
                if (index > 0 && slot > 0 && costs[slot - 1].CompareTo(best) < 0)
                {
                    best = costs[slot - 1];
                    parent = slot - 1;
                }
                if (slot < index || slot > capacity - count + index || best.Moved == int.MaxValue) continue;
                double distance = previous[index] is { } old ? old.DistanceTo(slots[slot]) : 0;
                next[slot] = new(best.Moved + (distance > .5 ? 1 : 0), best.Travel + distance,
                    best.Balance + (previous[index] == null ? Math.Pow(slot - preferred, 2) : 0));
                parents[index, slot] = parent;
            }
            costs = next;
        }
        int end = 0;
        for (int slot = 1; slot < capacity; slot++)
            if (costs[slot].CompareTo(costs[end]) < 0) end = slot;
        var result = new int[count];
        for (int index = count - 1; index >= 0; index--)
        {
            result[index] = end;
            end = parents[index, end];
        }
        return result;
    }
}
