using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private readonly record struct StardustPreviewKey(object Owner, object? Target, Type PayloadType, string State);
    private readonly Dictionary<StardustPreviewKey, ArrowDeployment> _stardustGroups = [];
    private readonly Dictionary<ArrowDeployment, ArrowDeployment> _hoverRepresentatives = [];
    private readonly Dictionary<ArrowDeployment, int> _previewQuantities = [];
    private readonly Dictionary<ArrowDeployment, string> _previewStates = [], _renderedPreviewStates = [];

    private void GroupHoverCandidates()
    {
        _hoverArrows.Clear();
        _stardustGroups.Clear();
        _hoverRepresentatives.Clear();
        _previewQuantities.Clear();
        _previewStates.Clear();
        foreach (var arrow in _hoverCandidates)
        {
            var representative = arrow;
            if (CardCatalog.DesignId(arrow.Card) == "U_CARD_005")
            {
                var state = StardustPreviewState(arrow);
                var key = new StardustPreviewKey(arrow.Card.Owner, arrow.Target, arrow.Payload.GetType(), state);
                if (!_stardustGroups.TryGetValue(key, out representative))
                {
                    representative = arrow;
                    _stardustGroups.Add(key, arrow);
                    _previewStates.Add(arrow, state);
                }
            }
            _hoverRepresentatives.Add(arrow, representative);
            if (_previewQuantities.TryGetValue(representative, out int count)) _previewQuantities[representative] = count + 1;
            else { _previewQuantities.Add(representative, 1); _hoverArrows.Add(representative); }
        }
    }

    private static string StardustPreviewState(ArrowDeployment arrow)
    {
        var card = arrow.Card;
        var saved = card.ToSerializable();
        saved.FloorAddedToDeck = null; // Acquisition history does not change the card's current state.
        var investment = DamageModifiers.CaptureDamageModifierSnapshot(card);
        // AttacksWhileWaiting is bookkeeping for other arrow effects; Stardust does not use it.
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
            arrow.Play.IsAutoPlay, arrow.Play.ResultPile, arrow.Play.Resources, XValues = arrow.CopyXValues()
        });
    }
}
