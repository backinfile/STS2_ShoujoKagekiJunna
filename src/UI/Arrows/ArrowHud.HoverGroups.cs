using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly record struct ArrowCluster(ArrowDeployment Leader, int Count);
    private readonly Dictionary<ArrowDeployment, ArrowCluster> _arrowClusters = [];
    private readonly Dictionary<ArrowDeployment, ArrowDeployment> _hoverRepresentatives = [];
    private readonly List<ArrowDeployment> _clusteredOrder = [];
    private readonly Dictionary<ArrowDeployment, int> _previewQuantities = [];
    private readonly Dictionary<ArrowDeployment, string> _arrowPreviewStates = [], _renderedPreviewStates = [];

    private void RefreshArrowClusters()
    {
        _arrowClusters.Clear();
        _arrowPreviewStates.Clear();
        if (_displayOrder.Count < ArrowLayout.MinimumClusterSize) return;

        var clusters = _displayOrder.GroupBy(arrow => (arrow.Card.Id, arrow.Card.Owner, arrow.Target, arrow.TargetCategory))
            .Where(group => group.Count() >= ArrowLayout.MinimumClusterSize)
            .SelectMany(group => group.GroupBy(arrow =>
            {
                var state = ArrowPreviewState(arrow);
                _arrowPreviewStates.Add(arrow, state);
                return (arrow.Payload.GetType(), state);
            }))
            .Where(group => group.Count() >= ArrowLayout.MinimumClusterSize)
            .ToDictionary(group => group.First());
        if (clusters.Count == 0) return;

        foreach (var (leader, members) in clusters)
        {
            var cluster = new ArrowCluster(leader, members.Count());
            foreach (var member in members) _arrowClusters.Add(member, cluster);
        }

        // Gather each cluster at its first member, preserving order within the cluster and among other arrows.
        _clusteredOrder.Clear();
        foreach (var arrow in _displayOrder)
        {
            if (clusters.TryGetValue(arrow, out var members)) _clusteredOrder.AddRange(members);
            else if (!_arrowClusters.ContainsKey(arrow)) _clusteredOrder.Add(arrow);
        }
        _displayOrder.Clear();
        _displayOrder.AddRange(_clusteredOrder);
        _clusteredOrder.Clear();
    }

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
    {
        var card = arrow.Card;
        var saved = card.ToSerializable();
        saved.FloorAddedToDeck = null; // Acquisition history does not change the card's current state.
        var investment = DamageModifiers.CaptureDamageModifierSnapshot(card);
        return JsonSerializer.Serialize(new {
            Saved = saved,
            Variables = card.DynamicVars.OrderBy(pair => pair.Key).Select(pair => new { pair.Key, pair.Value.BaseValue }),
            Keywords = card.Keywords.OrderBy(keyword => keyword.ToString()),
            card.BaseReplayCount, card.EnergyCost.Canonical, card.EnergyCost.CostsX, CapturedX = card.EnergyCost.CostsX ? card.EnergyCost.CapturedXValue : 0,
            Cost = card.EnergyCost.GetWithModifiers(CostModifiers.All),
            LocalCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local),
            card.CurrentStarCost, card.HasStarCostX,
            Affliction = card.Affliction == null ? null : new { card.Affliction.Id, card.Affliction.Amount },
            investment.DamageBonus, investment.ExtraHits, ReceivedBonus = DamageModifiers.HasReceivedBonus(card),
            Shine = Shine.GetState(card),
            arrow.Remaining, arrow.PlayCount, arrow.DeployedRound, arrow.ExhaustAfterResolve,
            WaitingHits = CardCatalog.DesignId(card) == "JT03" ? arrow.AttacksWhileWaiting : 0,
            arrow.Play.IsAutoPlay, arrow.Play.ResultPile, arrow.Play.Resources, XValues = arrow.CopyXValues()
        });
    }
}
