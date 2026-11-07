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
