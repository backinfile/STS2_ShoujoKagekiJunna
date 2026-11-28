using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly List<ArrowDeployment> _displayOrder = [], _waitingOrder = [], _removed = [];
    private readonly HashSet<ArrowDeployment> _live = [];
    private readonly Dictionary<Creature, Vector2> _targetPositions = [];
    private readonly Dictionary<Creature, int> _targetOrder = [];
    private readonly Dictionary<(Creature Owner, bool Allies), List<Vector2>> _groupTargets = [];
    private Comparison<ArrowDeployment>? _compareDisplayOrder;
    private static readonly Comparison<Vector2> CompareTargetX = (left, right) => left.X.CompareTo(right.X);

    private void RefreshArrowSnapshots(NCombatRoom room)
    {
        var deployed = ArrowCombat.Deployed;
        var local = GetGlobalTransform().AffineInverse();
        _targetPositions.Clear();
        foreach (var node in room.CreatureNodes) _targetPositions[node.Entity] = local * node.VfxSpawnPosition;
        _targetOrder.Clear();
        if (deployed.Count > 0 && deployed[0].Card.CombatState is { } combat)
        {
            int index = 0;
            foreach (var creature in combat.Creatures) _targetOrder[creature] = index++;
        }
        _live.Clear();
        _waitingOrder.Clear();
        foreach (var arrow in deployed)
        {
            _live.Add(arrow);
            if (!arrow.Resolving && (!_views.TryGetValue(arrow, out var view) || !view.Releasing))
                _waitingOrder.Add(arrow);
        }
        _waitingOrder.Sort(_compareDisplayOrder ??= CompareDisplayOrder);
        RefreshArrowStacks();

        _removed.Clear();
        foreach (var arrow in _views.Keys)
            if (!_live.Contains(arrow) || StackLeader(arrow) != arrow) _removed.Add(arrow);
        foreach (var removed in _removed)
        {
            FinishDeployment(removed);
            var view = _views[removed];
            if (!view.Releasing)
            {
                view.Root.Hide();
                view.Root.QueueFree();
            }
            _views.Remove(removed);
        }
        if (_removed.Count > 0) _bodyOrder.RemoveAll(entry => !_views.ContainsKey(entry.Arrow));

        _removed.Clear();
        foreach (var arrow in _previewCache.Keys)
            if (!_live.Contains(arrow)) _removed.Add(arrow);
        foreach (var removed in _removed) RemovePreviewCard(removed);

        _groupTargets.Clear();
        foreach (var arrow in _displayOrder)
        {
            if (!_views.TryGetValue(arrow, out var view))
            {
                view = CreateArrow(arrow);
                _views.Add(arrow, view);
            }
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
        }
    }

    private void AddTargetPoint(ArrowDeployment arrow, ArrowView view, Creature? creature)
    {
        if (creature != null && (creature.IsAlive || ArrowCombat.AwaitingReturn(arrow))
            && _targetPositions.TryGetValue(creature, out var point)) view.Targets.Add(point);
    }

    private int CompareDisplayOrder(ArrowDeployment left, ArrowDeployment right)
    {
        int result = left.TargetCategory.CompareTo(right.TargetCategory);
        if (result == 0)
        {
            int leftTarget = left.Target is { } target ? _targetOrder.GetValueOrDefault(target, -1) : 0;
            int rightTarget = right.Target is { } other ? _targetOrder.GetValueOrDefault(other, -1) : 0;
            result = leftTarget.CompareTo(rightTarget);
        }
        return result == 0 ? left.Sequence.CompareTo(right.Sequence) : result;
    }
}
