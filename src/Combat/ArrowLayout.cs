using Godot;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Ordered slot assignment: minimize moved existing arrows, then their travel, then balance new arrows.</summary>
internal static class ArrowLayout
{
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
