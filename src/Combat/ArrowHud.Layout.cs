using Godot;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly Dictionary<ArrowDeployment, Vector2> _slots = [];
    private Vector2 _layoutCanvas;
    private float _layoutRow;

    private void UpdateLayout(IReadOnlyList<ArrowDeployment> arrows, Vector2 canvas, float rowY)
    {
        foreach (var removed in _slots.Keys.Where(a => !arrows.Contains(a)).ToArray()) _slots.Remove(removed);
        // The desired positions are kept separately from bobbing and the movement animation.
        bool sameOrder = arrows.SequenceEqual(_slots.OrderBy(pair => pair.Value.Y).ThenBy(pair => pair.Value.X).Select(pair => pair.Key));
        if (sameOrder && canvas == _layoutCanvas && Mathf.IsEqualApprox(rowY, _layoutRow)) return;
        float start = canvas.X * .25f;
        int columns = Math.Max(1, (int)((canvas.X * .9f - start) / SlotSpacing) + 1);
        int retainedRows = _slots.Count == 0 ? 1 : Math.Max(1, (int)MathF.Round((_slots.Values.Max(p => p.Y) - rowY) / 140) + 1);
        int rows = Math.Max(retainedRows, (arrows.Count + 1 + columns - 1) / columns);
        var slots = Enumerable.Range(0, columns * rows)
            .Select(slot => new Vector2(start + slot % columns * SlotSpacing, rowY + slot / columns * 140)).ToArray();
        var previous = arrows.Select(arrow => _slots.TryGetValue(arrow, out var point) ? (Vector2?)point : null).ToArray();
        var assigned = ArrowLayout.Assign(previous, slots);
        _slots.Clear();
        for (int index = 0; index < arrows.Count; index++) _slots.Add(arrows[index], slots[assigned[index]]);
        _layoutCanvas = canvas;
        _layoutRow = rowY;
    }
}
