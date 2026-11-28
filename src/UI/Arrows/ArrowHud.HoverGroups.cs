using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly record struct ArrowCluster(ArrowDeployment Leader, int Count);
    private readonly Dictionary<ArrowDeployment, ArrowCluster> _arrowClusters = [];
    private readonly Dictionary<ArrowDeployment, ArrowDeployment> _hoverRepresentatives = [];
    private readonly List<ArrowDeployment> _clusteredOrder = [];
    private readonly record struct ClusterInput(ArrowDeployment Arrow, Creature? Target,
        int Remaining, int PlayCount, int Upgrade);
    private readonly List<ClusterInput> _clusterInputs = [];
    private double _nextClusterRefresh;
    private readonly Dictionary<ArrowDeployment, int> _previewQuantities = [];
    private readonly Dictionary<ArrowDeployment, string> _arrowPreviewStates = [], _renderedPreviewStates = [];

    private const int StackThreshold = 32;
    private readonly record struct StackKey(ModelId Id, Player Owner, Creature? Target, TargetType TargetType,
        int Upgrade, int Remaining, int Repeats, decimal Damage);
    private readonly Dictionary<StackKey, ArrowDeployment> _stackLeaders = [];
    private readonly Dictionary<ArrowDeployment, ArrowDeployment> _stackMembers = [];
    private readonly Dictionary<ArrowDeployment, int> _stackSizes = [];
    private bool _stacking;

    private ArrowDeployment StackLeader(ArrowDeployment arrow) => _stackMembers.GetValueOrDefault(arrow, arrow);

    private void RefreshArrowStacks()
    {
        bool stacking = _waitingOrder.Count >= StackThreshold;
        if (_stacking != stacking)
        {
            _clusterInputs.Clear();
            _arrowClusters.Clear();
            _arrowPreviewStates.Clear();
        }
        _stacking = stacking;
        _stackLeaders.Clear();
        _stackMembers.Clear();
        _stackSizes.Clear();
        _displayOrder.Clear();
        if (!stacking)
        {
            _displayOrder.AddRange(_waitingOrder);
            RefreshArrowClusters(_displayOrder, reorder: true);
            return;
        }

        foreach (var arrow in _waitingOrder)
        {
            if (_deployments.ContainsKey(arrow))
            {
                _displayOrder.Add(arrow);
                continue;
            }
            var card = arrow.Card;
            var vars = card.DynamicVars;
            decimal damage = vars.ContainsKey("Damage") ? vars["Damage"].BaseValue : 0;
            var key = new StackKey(card.Id, card.Owner, arrow.Target, card.TargetType, card.CurrentUpgradeLevel,
                arrow.Remaining, arrow.PlayCount, damage);
            if (_stackLeaders.TryGetValue(key, out var leader))
            {
                _stackMembers.Add(arrow, leader);
                _stackSizes[leader]++;
                continue;
            }
            _stackLeaders.Add(key, arrow);
            _stackSizes.Add(arrow, 1);
            _displayOrder.Add(arrow);
        }
    }

    private void RefreshArrowClusters(List<ArrowDeployment> arrows, bool reorder)
    {
        bool dense = _stacking || arrows.Count >= BatchBodyThreshold;
        if (dense && _elapsed < _nextClusterRefresh && arrows.Count == _clusterInputs.Count)
        {
            int index = 0;
            while (index < arrows.Count && ClusterState(arrows[index]) == _clusterInputs[index]) index++;
            if (index == arrows.Count)
            {
                if (reorder)
                {
                    arrows.Clear();
                    arrows.AddRange(_clusteredOrder);
                }
                return;
            }
        }
        _clusterInputs.Clear();
        if (dense)
        {
            _nextClusterRefresh = _elapsed + .15;
            foreach (var arrow in arrows) _clusterInputs.Add(ClusterState(arrow));
        }
        _clusteredOrder.Clear();
        _arrowClusters.Clear();
        _arrowPreviewStates.Clear();
        if (arrows.Count < ArrowLayout.MinimumClusterSize) return;

        var clusters = arrows.GroupBy(arrow => (arrow.Card.Id, arrow.Card.Owner, arrow.Target, arrow.TargetCategory))
            .Where(group => group.Count() >= ArrowLayout.MinimumClusterSize)
            .SelectMany(group => group.GroupBy(arrow =>
            {
                var state = BuildArrowPreviewState(arrow, includeCombatModifiers: !dense);
                if (!dense) _arrowPreviewStates.Add(arrow, state);
                return (arrow.Payload.GetType(), state);
            }))
            .Where(group => group.Count() >= ArrowLayout.MinimumClusterSize)
            .ToDictionary(group => group.First());
        foreach (var (leader, members) in clusters)
        {
            var cluster = new ArrowCluster(leader, members.Count());
            foreach (var member in members) _arrowClusters.Add(member, cluster);
        }

        if (!reorder) return;

        // Gather each cluster at its first member, preserving order within the cluster and among other arrows.
        foreach (var arrow in arrows)
        {
            if (clusters.TryGetValue(arrow, out var members)) _clusteredOrder.AddRange(members);
            else if (!_arrowClusters.ContainsKey(arrow)) _clusteredOrder.Add(arrow);
        }
        arrows.Clear();
        arrows.AddRange(_clusteredOrder);
    }

    private static ClusterInput ClusterState(ArrowDeployment arrow)
        => new(arrow, arrow.Target, arrow.Remaining, arrow.PlayCount, arrow.Card.CurrentUpgradeLevel);

    private void GroupHoverCandidates()
    {
        if (_stacking) RefreshArrowClusters(_hoverCandidates, reorder: false);
        _hoverArrows.Clear();
        _hoverRepresentatives.Clear();
        _previewQuantities.Clear();
        foreach (var arrow in _hoverCandidates)
        {
            // Hovering an individual icon shows that copy; hovering its target includes the whole cluster.
            var representative = _hoverCandidates.Count > 1 && _arrowClusters.TryGetValue(arrow, out var cluster)
                ? cluster.Leader : arrow;
            _hoverRepresentatives.Add(arrow, representative);
            if (_previewQuantities.TryGetValue(representative, out int count)) _previewQuantities[representative] = count + 1;
            else { _previewQuantities.Add(representative, 1); _hoverArrows.Add(representative); }
        }
    }

    private static string ArrowPreviewState(ArrowDeployment arrow)
        => BuildArrowPreviewState(arrow, includeCombatModifiers: true);

    private static string BuildArrowPreviewState(ArrowDeployment arrow, bool includeCombatModifiers)
    {
        var card = arrow.Card;
        var saved = card.ToSerializable();
        saved.FloorAddedToDeck = null; // Acquisition history does not change the card's current state.
        var keywords = includeCombatModifiers ? card.Keywords : card.GetKeywordsWithSources(KeywordSources.Local);
        int localCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
        return JsonSerializer.Serialize(new {
            Saved = saved,
            Variables = card.DynamicVars.OrderBy(pair => pair.Key).Select(pair => new { pair.Key, pair.Value.BaseValue }),
            Keywords = keywords.OrderBy(keyword => keyword.ToString()),
            card.BaseReplayCount, card.EnergyCost.Canonical, card.EnergyCost.CostsX, CapturedX = card.EnergyCost.CostsX ? card.EnergyCost.CapturedXValue : 0,
            Cost = includeCombatModifiers ? card.EnergyCost.GetWithModifiers(CostModifiers.All) : localCost,
            LocalCost = localCost,
            card.CurrentStarCost, card.HasStarCostX,
            Affliction = card.Affliction == null ? null : new { card.Affliction.Id, card.Affliction.Amount },
            Shine = Shine.GetState(card),
            arrow.Remaining, arrow.PlayCount, arrow.DeployedRound, arrow.ExhaustAfterResolve,
            arrow.Play.IsAutoPlay, arrow.Play.ResultPile, arrow.Play.Resources, XValues = arrow.CopyXValues()
        });
    }
}
