using Godot;

namespace ShoujoKagekiJunna.Combat;

internal enum ArrowLane { FriendlyGroup, FriendlySingle, EnemyGroup, Left, Right }

/// <summary>Target regions and ordered slots, preserving existing arrows before balancing new arrows.</summary>
internal static class ArrowLayout
{
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
