using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public partial class ArrowHud
{
    private static readonly FieldInfo? CardPreviewTarget = AccessTools.Field(typeof(NCard), "_previewTarget");

    private void UpdateAccelerationPreview(NCombatRoom room)
    {
        var (card, target) = AccelerationPreviewSource(room);
        var acceleration = card switch
        {
            JunnaCard junna => junna.Acceleration,
            JunnaArrowTargetChoice option => option.Acceleration,
            _ => default
        };
        if (card is JunnaCard && acceleration.Turns > 0 && CardCatalog.IsArrow(card)
            && HandArrow.Turns(card) > (card.Owner.Creature.GetPower<JunnaPowerJA105>()?.AccelerationTurns ?? 0))
            acceleration = default;

        foreach (var arrow in _displayOrder)
        {
            var view = _views[arrow];
            var cue = AccelerationCueKind.None;
            if (card != null && acceleration.Turns > 0 && arrow.Remaining > 0
                && arrow.Card.Pile?.Type == ArrowPile.PileId && view.Root.Visible
                && acceleration.Matches(arrow, card.Owner, target))
                cue = acceleration.Turns >= arrow.Remaining ? AccelerationCueKind.Completes : AccelerationCueKind.Advances;
            if (cue != AccelerationCueKind.None && view.AccelerationCue == null)
            {
                view.AccelerationCue = new ArrowAccelerationCue { Name = "AccelerationCue" };
                view.Frame.AddChild(view.AccelerationCue);
            }
            view.AccelerationCue?.SetCue(cue);
        }
    }

    private (CardModel? Card, Creature? Target) AccelerationPreviewSource(NCombatRoom room)
    {
        if (!IsVisibleInTree()) return default;
        var screen = ActiveScreenContext.Instance;
        if (NOverlayStack.Instance?.Peek() is NChooseACardSelectionScreen choice && screen.IsCurrent(choice))
        {
            var viewport = GetViewport();
#if STS2_MAIN || STS2_BETA
            bool directional = NControllerManager.Instance?.IsUsingDirectionalNavigation == true;
#else
            bool directional = NControllerManager.Instance?.IsUsingController == true;
#endif
            var control = directional ? viewport.GuiGetFocusOwner() : viewport.GuiGetHoveredControl();
            var holder = FindCardHolder(control);
            if (holder != null && choice.IsAncestorOf(holder) && holder.CardModel is JunnaArrowTargetChoice option
                && LocalContext.IsMe(option.Owner))
                return (option, null);
            return default;
        }
        if (!screen.IsCurrent(room)) return default;
        var hand = room.Ui.Hand;
        var manager = NTargetManager.Instance;
        if (hand.InCardPlay)
        {
            // Native play temporarily takes its holder out of the visible hand.
            if (hand.Get(NPlayerHand.PropertyName._currentCardPlay).AsGodotObject() is not NCardPlay play
                || play.IsQueuedForDeletion()) return default;
            var target = manager.IsInSelection
                ? (manager.Get(NTargetManager.PropertyName.HoveredNode).AsGodotObject() as NCreature)?.Entity : null;
            return HandCard(play.Holder.CardModel, target);
        }
        if (hand.CurrentMode != NPlayerHand.Mode.Play || manager.IsInSelection) return default;
        var focused = hand.FocusedHolder;
        if (focused == null || !IsInstanceValid(focused) || focused.IsQueuedForDeletion()
            || !focused.IsVisibleInTree() || hand.IsAwaitingPlay(focused)
            || focused.CardNode is not { Visibility: ModelVisibility.Visible, Model: { } card } cardNode) return default;
        return HandCard(card, CardPreviewTarget?.GetValue(cardNode) as Creature);
    }

    private static (CardModel? Card, Creature? Target) HandCard(CardModel? card, Creature? target)
    {
        if (card?.Pile?.Type != PileType.Hand || !LocalContext.IsMe(card.Owner)) return default;
        return (card, target != null && card.IsValidTarget(target) ? target : null);
    }

    private static NCardHolder? FindCardHolder(Control? control)
    {
        for (Node? node = control; node != null; node = node.GetParent())
            if (node is NCardHolder holder) return holder;
        return null;
    }
}
