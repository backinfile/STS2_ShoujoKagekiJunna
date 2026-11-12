using System.Runtime.CompilerServices;
using ShoujoKageki.Core.V1.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

/// <summary>New effect-generated arrows enter their own flight without the native center-card pause.</summary>
internal static class GeneratedArrowPresentation
{
    private static readonly ConditionalWeakTable<CardModel, object> Cards = new();

    internal static async Task Play(CardModel card, Func<Task> play)
    {
        Cards.Add(card, new object());
        var scope = new Scope(card);
        Exception? failure = null;
        try
        {
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Play, card.Owner);
            if (CoreEffects.Enabled && card.Pile?.Type == PileType.Play && NCombatRoom.Instance is { } room)
            {
                var view = NCard.Create(card);
                if (view != null)
                {
                    scope.View = view;
                    room.Ui.AddToPlayContainer(view);
                    scope.Container = view.GetParent();
                    view.UpdateVisuals(PileType.Play, CardPreviewMode.Normal);
                    view.MouseFilter = Control.MouseFilterEnum.Ignore;
                    view.Scale = Vector2.One * .45f;
                    var origin = room.GetCreatureNode(card.Owner.Creature)?.VfxSpawnPosition
                        ?? room.GetViewport().GetVisibleRect().Size * new Vector2(.3f, .6f);
                    var frame = view.GetNode<Control>("%Frame");
                    view.GlobalPosition += origin + new Vector2(110, -140)
                        - frame.GetGlobalTransform() * (frame.Size * .5f);
                }
            }
            await play();
        }
        catch (Exception error) { failure = error; }
        try { await scope.DisposeAsync(); }
        catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class Scope(CardModel card) : IAsyncDisposable
    {
        internal NCard? View;
        internal Node? Container;

        public async ValueTask DisposeAsync()
        {
            Cards.Remove(card);
            // Successful deployment reparents the visual into its independent animation.
            // Rejected or interrupted plays must not leave a card in the native container.
            if (View != null && GodotObject.IsInstanceValid(View) && View.GetParent() == Container)
                View.QueueFreeSafely();
            if (card.Pile?.Type == PileType.Play) await CardPileCmd.RemoveFromCombat(card);
            else if (card.Pile == null && !card.HasBeenRemovedFromState) card.RemoveFromState();
        }
    }

    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
    private static class NativeAutoPlay
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(CardModel card, ref bool skipCardPileVisuals)
        {
            if (Cards.TryGetValue(card, out _)) skipCardPileVisuals = true;
        }
    }
}
