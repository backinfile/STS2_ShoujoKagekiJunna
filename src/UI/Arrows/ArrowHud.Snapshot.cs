using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly List<ArrowDeployment> _displayOrder = [], _removed = [];
    private readonly HashSet<ArrowDeployment> _live = [];
    private readonly Dictionary<Creature, Vector2> _targetPositions = [];
    private readonly Dictionary<Creature, int> _targetOrder = [];
    private readonly Dictionary<(Creature Owner, bool Allies), List<Vector2>> _groupTargets = [];
    private Comparison<ArrowDeployment>? _compareDisplayOrder;
    private static readonly Comparison<Vector2> CompareTargetX = (left, right) => left.X.CompareTo(right.X);

    private void RefreshArrowSnapshots(NCombatRoom room)
    {
        _live.Clear();
        foreach (var arrow in ArrowCombat.Deployed) _live.Add(arrow);
        _removed.Clear();
        foreach (var arrow in _views.Keys)
            if (!_live.Contains(arrow)) _removed.Add(arrow);
        foreach (var removed in _removed)
        {
            FinishDeployment(removed);
            RemovePreviewCard(removed);
            if (!_views[removed].Releasing) _views[removed].Root.QueueFree();
            _views.Remove(removed);
        }
        if (_removed.Count > 0) _bodyOrder.RemoveAll(entry => !_live.Contains(entry.Arrow));
        var local = GetGlobalTransform().AffineInverse();
        _targetPositions.Clear();
        foreach (var node in room.CreatureNodes) _targetPositions[node.Entity] = local * node.VfxSpawnPosition;
        _targetOrder.Clear();
        if (ArrowCombat.Deployed.Count > 0 && ArrowCombat.Deployed[0].Card.CombatState is { } combat)
        {
            int index = 0;
            foreach (var creature in combat.Creatures) _targetOrder[creature] = index++;
        }
        _displayOrder.Clear();
        _groupTargets.Clear();
        foreach (var arrow in ArrowCombat.Deployed)
        {
            if (arrow.Resolving) continue;
            if (!_views.TryGetValue(arrow, out var view))
            {
                view = CreateArrow(arrow);
                _views.Add(arrow, view);
            }
            if (view.Releasing) continue;
            view.TargetOrder = arrow.Target is { } target ? _targetOrder.GetValueOrDefault(target, -1) : 0;
            view.Targets.Clear();
            if (arrow.Card.CombatState != null)
            {
                if (arrow.TargetsGroup)
                {
                    var key = (arrow.Card.Owner.Creature, arrow.TargetsAllPlayers);
                    if (_groupTargets.TryGetValue(key, out var targets)) view.Targets.AddRange(targets);
                    else
                    {
                        foreach (var creature in arrow.Targets) AddTargetPoint(arrow, view, creature);
                        view.Targets.Sort(CompareTargetX);
                        _groupTargets.Add(key, view.Targets);
                    }
                }
                else AddTargetPoint(arrow, view, arrow.Target);
            }
            var total = Vector2.Zero;
            foreach (var point in view.Targets) total += point;
            view.TargetCenter = view.Targets.Count == 0 ? Vector2.Zero : total / view.Targets.Count;
            _displayOrder.Add(arrow);
        }
        _displayOrder.Sort(_compareDisplayOrder ??= CompareDisplayOrder);
        RefreshArrowClusters();
    }

    private void AddTargetPoint(ArrowDeployment arrow, ArrowView view, Creature? creature)
    {
        if (creature != null && (creature.IsAlive || ArrowCombat.AwaitingReturn(arrow))
            && _targetPositions.TryGetValue(creature, out var point)) view.Targets.Add(point);
    }

    private int CompareDisplayOrder(ArrowDeployment left, ArrowDeployment right)
    {
        int result = left.TargetCategory.CompareTo(right.TargetCategory);
        if (result == 0) result = _views[left].TargetOrder.CompareTo(_views[right].TargetOrder);
        return result == 0 ? left.Sequence.CompareTo(right.Sequence) : result;
    }
}
