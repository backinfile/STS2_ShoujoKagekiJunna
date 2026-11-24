using Godot;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<ArrowDeployment, Vector2> _slots = [];
    private readonly Dictionary<ArrowDeployment, float> _slotAnchors = [];
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
        foreach (var removed in _slots.Keys.Where(a => !_live.Contains(a) || a.Resolving).ToArray())
        {
            _slots.Remove(removed);
            _slotAnchors.Remove(removed);
        }
        var formation = ArrowLayout.Regions(canvas.X, allyLeft, allyRight, enemiesLeft, SlotSpacing);
        var counts = Enum.GetValues<ArrowLane>().Select(lane => lanes.Count(value => value == lane)).ToArray();
        var regions = ArrowLayout.OccupiedRegions(canvas.X, allyLeft, allyRight,
            enemiesLeft, SlotSpacing, counts);
        // Keep the same lane boundaries when only their populations change.
        if (canvas == _layoutCanvas && formation.SequenceEqual(_layoutFormation)
            && lanes.Distinct().Order().SequenceEqual(_layoutLanes.Distinct().Order())
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
            var origin = new Vector2(range.X, rowY);
            var oldOrigin = new Vector2(range.X, _layoutRow);
            var previousInRegion = group.Select(arrow => _slots.TryGetValue(arrow, out var point)
                ? (Vector2?)(point - oldOrigin) : null).ToArray();
            var anchors = group.Select(arrow => _slotAnchors.TryGetValue(arrow, out float x)
                ? (float?)(x - origin.X) : null).ToArray();
            // OccupiedRegions ends on the old 80-unit grid. Keep the remainder available
            // when a dense lane shrinks as well as when it grows.
            float endX = lane == ArrowLane.Right ? canvas.X * .9f
                : lane == ArrowLane.Left ? formation[(int)ArrowLane.FriendlyGroup].X - SlotSpacing : range.Y;
            float width = Math.Max(range.Y, endX) - range.X;
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
                float rowSpacing = singleCluster
                    ? ArrowLayout.ClusterGap(_arrowClusters[group[0]].Count, RowSpacing, IconSize.Y * .5f)
                    : RowSpacing;
                ArrowLayout.FitTwoRows(width, gaps, minimumGaps);
                var packed = ArrowLayout.Pack(width, rowSpacing, gaps, previousInRegion, (left, right) =>
                    _arrowClusters.TryGetValue(group[left], out var cluster)
                    && cluster.Leader == _arrowClusters.GetValueOrDefault(group[right]).Leader ? IconWidth * .5f : IconWidth, anchors);
                StoreSlots(group, packed, origin);
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
            var preferred = assigned.Select(index => slots[index] - origin).ToArray();
            var retained = ArrowLayout.KeepSlots(width, RowSpacing, previousInRegion, preferred, (_, _) => IconWidth, anchors);
            StoreSlots(group, retained ?? preferred, origin);
        }
        _layoutCanvas = canvas;
        _layoutRow = rowY;
        _layoutOrder = arrows.ToArray();
        _layoutLanes = lanes;
        _layoutClusters = _clusterBuffer.ToArray();
        _layoutRegions = regions;
        _layoutFormation = formation;
    }

    private void StoreSlots(ArrowDeployment[] arrows, Vector2[] positions, Vector2 origin)
    {
        for (int i = 0; i < arrows.Length; i++)
        {
            var arrow = arrows[i];
            var point = positions[i] + origin;
            // Keep the original anchor through small nudges so successive copies cannot ratchet it across the row.
            if (!_slotAnchors.TryGetValue(arrow, out float anchor) || !_slots.TryGetValue(arrow, out var old)
                || !Mathf.IsEqualApprox(old.Y, point.Y) || Math.Abs(point.X - anchor) > ArrowLayout.MaximumSlotNudge + .01f)
                _slotAnchors[arrow] = point.X;
            _slots[arrow] = point;
        }
    }
}
