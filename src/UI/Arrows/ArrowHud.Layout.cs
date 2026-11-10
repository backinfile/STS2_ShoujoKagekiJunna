using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<ArrowDeployment, Vector2> _slots = [];
    private readonly HashSet<ArrowDeployment> _visibleArrows = [];
    private sealed record OverflowView(NButton Button, List<ArrowDeployment> Arrows);
    private readonly Dictionary<ArrowLane, OverflowView> _overflow = [];
    private float _layoutBottom;
    private Vector2 _layoutCanvas;
    private float _layoutRow;
    private ArrowDeployment[] _layoutOrder = [];
    private ArrowLane[] _layoutLanes = [];
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
        float creatureTop = _targetPositions.Values.Select(point => point.Y).DefaultIfEmpty(canvas.Y).Min();
        float bottom = Math.Max(rowY, Math.Min(canvas.Y * .42f, creatureTop - 180));
        _laneBuffer.Clear();
        bool same = arrows.Count == _layoutOrder.Length;
        for (int i = 0; i < arrows.Count; i++)
        {
            var arrow = arrows[i];
            var targets = _views[arrow].Targets;
            float targetX = targets.Count > 0 ? targets[0].X : enemyCount == 0 ? canvas.X : enemySum / enemyCount;
            var lane = ArrowLayout.Lane(arrow.TargetCategory <= 1, _views[arrow].Group, targetX, allyCenter);
            _laneBuffer.Add(lane);
            same = same && arrow == _layoutOrder[i] && lane == _layoutLanes[i];
        }
        bool enemiesLeft = enemyLeft < allyCenter || _laneBuffer.Contains(ArrowLane.Left);
        if (same && canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)
            && allyLeft == _layoutAllyLeft && allyRight == _layoutAllyRight && enemiesLeft == _layoutEnemiesLeft
            && Mathf.IsEqualApprox(bottom, _layoutBottom)) return;
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
        if (arrows.SequenceEqual(_layoutOrder) && lanes.SequenceEqual(_layoutLanes)
            && regions.SequenceEqual(_layoutRegions) && canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)
            && Mathf.IsEqualApprox(bottom, _layoutBottom)) return;
        _visibleArrows.Clear();
        foreach (var overflow in _overflow.Values)
        {
            overflow.Button.Hide();
            overflow.Arrows.Clear();
        }
        int maxRows = ArrowLayout.RowCapacity(rowY, bottom, RowSpacing);
        foreach (var lane in Enum.GetValues<ArrowLane>())
        {
            var group = arrows.Where((_, index) => lanes[index] == lane).ToArray();
            if (group.Length == 0) continue;
            var range = regions[(int)lane];
            int columns = Math.Max(1, (int)MathF.Round((range.Y - range.X) / SlotSpacing) + 1);
            int capacity = columns * maxRows;
            bool overflowing = group.Length > capacity;
            int visibleCount = overflowing ? capacity - 1 : group.Length;
            var visible = group.Take(visibleCount).ToArray();
            var previous = visible.Select(arrow => _slots.TryGetValue(arrow, out var point) ? (Vector2?)point : null).ToArray();
            float previousBottom = previous.Where(p => p.HasValue).Select(p => p!.Value.Y).DefaultIfEmpty(rowY).Max();
            bool retainRows = canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)
                && _layoutRegions.Length == regions.Length && range == _layoutRegions[(int)lane];
            int retainedRows = retainRows ? Math.Max(1, (int)MathF.Round((previousBottom - rowY) / RowSpacing) + 1) : 1;
            int rows = Math.Min(maxRows, Math.Max(retainedRows, (visibleCount + columns - 1) / columns));
            if (overflowing) rows = maxRows;
            var slots = Enumerable.Range(0, columns * rows)
                .Select(slot => new Vector2(range.X + slot % columns * SlotSpacing, rowY + slot / columns * RowSpacing)).ToArray();
            var assigned = ArrowLayout.Assign(previous, overflowing ? slots[..^1] : slots);
            for (int index = 0; index < visible.Length; index++)
            {
                _slots[visible[index]] = slots[assigned[index]];
                _visibleArrows.Add(visible[index]);
            }
            if (overflowing)
            {
                if (!_overflow.TryGetValue(lane, out var overflow))
                {
                    var button = MakeArrowButton("+", "ArrowOverflow" + lane);
                    AddChild(button);
                    _overflow.Add(lane, overflow = new(button, []));
                }
                var position = slots[^1];
                overflow.Button.Position = position - overflow.Button.Size * .5f;
                overflow.Button.GetNode<Label>("Text").Text = $"+{group.Length - visibleCount}";
                overflow.Button.Show();
                foreach (var arrow in group.Skip(visibleCount))
                {
                    overflow.Arrows.Add(arrow);
                    _slots[arrow] = position;
                }
            }
        }
        _layoutBottom = bottom;
        _layoutCanvas = canvas;
        _layoutRow = rowY;
        _layoutOrder = arrows.ToArray();
        _layoutLanes = lanes;
        _layoutRegions = regions;
        _layoutFormation = formation;
    }
}
