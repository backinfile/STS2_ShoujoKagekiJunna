using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

    private void RefreshArrowClusters()
    {
        bool dense = _displayOrder.Count >= BatchBodyThreshold;
        if (dense && _elapsed < _nextClusterRefresh && _displayOrder.Count == _clusterInputs.Count)
        {
            int index = 0;
            while (index < _displayOrder.Count && ClusterState(_displayOrder[index]) == _clusterInputs[index]) index++;
            if (index == _displayOrder.Count)
            {
                _displayOrder.Clear();
                _displayOrder.AddRange(_clusteredOrder);
                return;
            }
        }
        _clusterInputs.Clear();
        if (dense)
        {
            _nextClusterRefresh = _elapsed + .15;
            foreach (var arrow in _displayOrder) _clusterInputs.Add(ClusterState(arrow));
        }
        _clusteredOrder.Clear();
        _arrowClusters.Clear();
        _arrowPreviewStates.Clear();
        if (_displayOrder.Count < ArrowLayout.MinimumClusterSize) return;

        var clusters = _displayOrder.GroupBy(arrow => (arrow.Card.Id, arrow.Card.Owner, arrow.Target, arrow.TargetCategory))
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

        // Gather each cluster at its first member, preserving order within the cluster and among other arrows.
        foreach (var arrow in _displayOrder)
        {
            if (clusters.TryGetValue(arrow, out var members)) _clusteredOrder.AddRange(members);
            else if (!_arrowClusters.ContainsKey(arrow)) _clusteredOrder.Add(arrow);
        }
        _displayOrder.Clear();
        _displayOrder.AddRange(_clusteredOrder);
    }

    private static ClusterInput ClusterState(ArrowDeployment arrow)
        => new(arrow, arrow.Target, arrow.Remaining, arrow.PlayCount, arrow.Card.CurrentUpgradeLevel);

    private void GroupHoverCandidates()
    {
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
