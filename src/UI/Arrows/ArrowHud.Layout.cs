using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<ArrowDeployment, Vector2> _slots = [];
    private Vector2 _layoutCanvas;
    private float _layoutRow;
    private ArrowDeployment[] _layoutOrder = [];
    private ArrowLane[] _layoutLanes = [];
    private Vector2[] _layoutRegions = [];
    private Vector2[] _layoutFormation = [];

    private void UpdateLayout(IReadOnlyList<ArrowDeployment> arrows, Vector2 canvas, float rowY)
    {
        foreach (var removed in _slots.Keys.Where(a => !arrows.Contains(a)).ToArray()) _slots.Remove(removed);
        var creatures = NCombatRoom.Instance!.CreatureNodes.Where(n => n.Entity.IsAlive).ToArray();
        var allies = creatures.Where(n => !n.Entity.IsEnemy).Select(n => LocalPoint(n.VfxSpawnPosition).X).ToArray();
        var enemies = creatures.Where(n => n.Entity.IsEnemy).Select(n => LocalPoint(n.VfxSpawnPosition).X).ToArray();
        float allyLeft = allies.Length == 0 ? canvas.X * .25f : allies.Min();
        float allyRight = allies.Length == 0 ? allyLeft : allies.Max();
        float allyCenter = (allyLeft + allyRight) * .5f;
        var lanes = arrows.Select(arrow =>
        {
            var targets = TargetPoints(arrow);
            float targetX = targets.Length == 0 ? enemies.DefaultIfEmpty(canvas.X).Average() : targets[0].X;
            return ArrowLayout.Lane(arrow.TargetCategory <= 1, arrow.TargetsGroup, targetX, allyCenter);
        }).ToArray();
        bool enemiesLeft = enemies.Any(x => x < allyCenter) || lanes.Contains(ArrowLane.Left);
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
        // Cache the target regions as well as order: a side change can leave display order unchanged.
        if (arrows.SequenceEqual(_layoutOrder) && lanes.SequenceEqual(_layoutLanes)
            && regions.SequenceEqual(_layoutRegions) && canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)) return;
        foreach (var lane in Enum.GetValues<ArrowLane>())
        {
            var group = arrows.Where((_, index) => lanes[index] == lane).ToArray();
            if (group.Length == 0) continue;
            var range = regions[(int)lane];
            int columns = Math.Max(1, (int)MathF.Round((range.Y - range.X) / SlotSpacing) + 1);
            var previous = group.Select(arrow => _slots.TryGetValue(arrow, out var point) ? (Vector2?)point : null).ToArray();
            float bottom = previous.Where(p => p.HasValue).Select(p => p!.Value.Y).DefaultIfEmpty(rowY).Max();
            bool retainRows = canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)
                && _layoutRegions.Length == regions.Length && range == _layoutRegions[(int)lane];
            int retainedRows = retainRows ? Math.Max(1, (int)MathF.Round((bottom - rowY) / RowSpacing) + 1) : 1;
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
        _layoutRegions = regions;
        _layoutFormation = formation;
    }
}
