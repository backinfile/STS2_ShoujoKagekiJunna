using Godot;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<ArrowDeployment, Vector2> _slots = [];
    private Vector2 _layoutCanvas;
    private float _layoutRow;
    private ArrowDeployment[] _layoutOrder = [];
    private ArrowLane[] _layoutLanes = [];
    private ArrowCluster?[] _layoutClusters = [];
    private readonly List<ArrowCluster?> _clusterBuffer = [];
    private Vector2[] _layoutRegions = [];
    private Vector2[] _layoutFormation = [];
    private readonly List<ArrowLane> _laneBuffer = [];
    private float _layoutAllyLeft, _layoutAllyRight;
    private bool _layoutEnemiesLeft;

    private void UpdateLayout(IReadOnlyList<ArrowDeployment> arrows, Vector2 canvas, float rowY)
    {
        float allyLeft = float.PositiveInfinity, allyRight = float.NegativeInfinity;
        float enemyLeft = float.PositiveInfinity, enemySum = 0;
        int enemyCount = 0;
        foreach (var (creature, point) in _targetPositions)
        {
            if (!creature.IsAlive) continue;
            if (creature.IsEnemy)
            {
                enemyLeft = Math.Min(enemyLeft, point.X);
                enemySum += point.X;
                enemyCount++;
            }
            else
            {
                allyLeft = Math.Min(allyLeft, point.X);
                allyRight = Math.Max(allyRight, point.X);
            }
        }
        if (float.IsPositiveInfinity(allyLeft)) allyLeft = allyRight = canvas.X * .25f;
        float allyCenter = (allyLeft + allyRight) * .5f;
        _laneBuffer.Clear();
        _clusterBuffer.Clear();
        bool same = arrows.Count == _layoutOrder.Length;
        for (int i = 0; i < arrows.Count; i++)
        {
            var arrow = arrows[i];
            var targets = _views[arrow].Targets;
            float targetX = targets.Count > 0 ? targets[0].X : enemyCount == 0 ? canvas.X : enemySum / enemyCount;
            var lane = ArrowLayout.Lane(arrow.TargetCategory <= 1, arrow.TargetsGroup, targetX, allyCenter);
            _laneBuffer.Add(lane);
            ArrowCluster? cluster = _arrowClusters.TryGetValue(arrow, out var state) ? state : null;
            _clusterBuffer.Add(cluster);
            same = same && arrow == _layoutOrder[i] && lane == _layoutLanes[i] && cluster == _layoutClusters[i];
        }
        bool enemiesLeft = enemyLeft < allyCenter || _laneBuffer.Contains(ArrowLane.Left);
        if (same && canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)
            && allyLeft == _layoutAllyLeft && allyRight == _layoutAllyRight && enemiesLeft == _layoutEnemiesLeft) return;
        var lanes = _laneBuffer.ToArray();
        foreach (var removed in _slots.Keys.Where(a => !_live.Contains(a) || a.Resolving).ToArray()) _slots.Remove(removed);
        var formation = ArrowLayout.Regions(canvas.X, allyLeft, allyRight, enemiesLeft, SlotSpacing);
        var counts = Enum.GetValues<ArrowLane>().Select(lane => lanes.Count(value => value == lane)).ToArray();
        var regions = ArrowLayout.OccupiedRegions(canvas.X, allyLeft, allyRight,
            enemiesLeft, SlotSpacing, counts);
        // Removing arrows keeps the reclaimed columns and leaves the survivors in place.
        if (canvas == _layoutCanvas && formation.SequenceEqual(_layoutFormation)
            && lanes.Distinct().Order().SequenceEqual(_layoutLanes.Distinct().Order())
            && arrows.Select((arrow, i) =>
            {
                int old = Array.IndexOf(_layoutOrder, arrow);
                return old >= 0 && _layoutLanes[old] == lanes[i];
            }).All(same => same)
            && _layoutRegions.Length == regions.Length)
            regions = _layoutRegions;
        _layoutAllyLeft = allyLeft;
        _layoutAllyRight = allyRight;
        _layoutEnemiesLeft = enemiesLeft;
        // Cache the target regions as well as order: a side change can leave display order unchanged.
        if (same && arrows.SequenceEqual(_layoutOrder) && lanes.SequenceEqual(_layoutLanes)
            && regions.SequenceEqual(_layoutRegions) && canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)) return;
        foreach (var lane in Enum.GetValues<ArrowLane>())
        {
            var group = arrows.Where((_, index) => lanes[index] == lane).ToArray();
            if (group.Length == 0) continue;
            var range = regions[(int)lane];
            int columns = Math.Max(1, (int)MathF.Round((range.Y - range.X) / SlotSpacing) + 1);
            bool hasClusters = group.Any(_arrowClusters.ContainsKey);
            if (hasClusters || group.Length > columns * 2)
            {
                var gaps = new float[group.Length];
                var minimumGaps = new float[group.Length];
                bool singleCluster = hasClusters;
                for (int i = 1; i < group.Length; i++)
                {
                    bool joined = _arrowClusters.TryGetValue(group[i], out var cluster)
                        && cluster.Leader == _arrowClusters.GetValueOrDefault(group[i - 1]).Leader;
                    gaps[i] = joined ? ArrowLayout.ClusterGap(cluster.Count, SlotSpacing, IconWidth * .5f) : SlotSpacing;
                    minimumGaps[i] = joined ? IconWidth * .5f : IconWidth;
                    singleCluster &= joined;
                }
                // OccupiedRegions ends on the old 80-unit grid; use the remaining width as well.
                float endX = lane == ArrowLane.Right ? canvas.X * .9f
                    : lane == ArrowLane.Left ? formation[(int)ArrowLane.FriendlyGroup].X - SlotSpacing : range.Y;
                float rowSpacing = singleCluster
                    ? ArrowLayout.ClusterGap(_arrowClusters[group[0]].Count, RowSpacing, IconSize.Y * .5f)
                    : RowSpacing;
                float width = Math.Max(range.Y, endX) - range.X;
                ArrowLayout.FitTwoRows(width, gaps, minimumGaps);
                var origin = new Vector2(range.X, rowY);
                var previousInRegion = group.Select(arrow => _slots.TryGetValue(arrow, out var slot)
                    ? (Vector2?)new Vector2(slot.X - origin.X, slot.Y - _layoutRow) : null).ToArray();
                var packed = ArrowLayout.Pack(width, rowSpacing, gaps, previousInRegion);
                for (int i = 0; i < group.Length; i++) _slots[group[i]] = packed[i] + origin;
                continue;
            }
            var previous = group.Select(arrow => _slots.TryGetValue(arrow, out var point) ? (Vector2?)point : null).ToArray();
            float bottom = previous.Where(p => p.HasValue).Select(p => p!.Value.Y).DefaultIfEmpty(rowY).Max();
            bool retainRows = canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)
                && _layoutRegions.Length == regions.Length && range == _layoutRegions[(int)lane];
            int retainedRows = retainRows ? Math.Clamp((int)MathF.Round((bottom - rowY) / RowSpacing) + 1, 1, 2) : 1;
            int rows = Math.Max(retainedRows, (group.Length + columns - 1) / columns);
            var slots = Enumerable.Range(0, columns * rows)
                .Select(slot => new Vector2(range.X + slot % columns * SlotSpacing, rowY + slot / columns * RowSpacing)).ToArray();
            var assigned = ArrowLayout.Assign(previous, slots);
            for (int index = 0; index < group.Length; index++) _slots[group[index]] = slots[assigned[index]];
        }
        _layoutCanvas = canvas;
        _layoutRow = rowY;
        _layoutOrder = arrows.ToArray();
        _layoutLanes = lanes;
        _layoutClusters = _clusterBuffer.ToArray();
        _layoutRegions = regions;
        _layoutFormation = formation;
    }
}
