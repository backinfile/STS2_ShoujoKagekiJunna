using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Carry the individual arrow's position through native hidden-pile transitions.</summary>
[HarmonyPatch(typeof(PileTypeExtensions), nameof(PileTypeExtensions.GetTargetPosition))]
internal sealed class ArrowPileExit : IDisposable
{
    private static readonly AsyncLocal<Vector2?> Position = new();
    private readonly Vector2? _previous = Position.Value;

    internal ArrowPileExit(Vector2? position) => Position.Value = position;
    public void Dispose() => Position.Value = _previous;

    private static void Postfix(PileType pileType, ref Vector2 __result)
    {
        // The native card-back flight asks for the old pile position without supplying an NCard.
        if (pileType == ArrowPile.PileId && Position.Value is { } position) __result = position;
    }
}

public partial class ArrowHud
{
    internal Vector2? PileExitPosition(CardModel card)
    {
        var view = _views.FirstOrDefault(pair => pair.Key.Card == card).Value;
        if (view?.ExitPosition is { } impact) return impact;
        return view != null && GodotObject.IsInstanceValid(view.Root)
            ? view.Root.GetGlobalTransform() * (IconSize * .5f) : null;
    }
}
