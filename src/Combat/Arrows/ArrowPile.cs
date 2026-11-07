using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShoujoKagekiJunna.Combat;

public sealed class ArrowPile() : CustomPile(PileId)
{
    [CustomEnum] public static PileType PileId;
    [CustomEnum] public static CardType ArrowType;
    public override bool CardShouldBeVisible(CardModel card) => false;
    public override bool NeedsCustomTransitionVisual => true;
    public override bool CustomTween(Tween tween, CardModel card, NCard cardNode, CardPile? oldPile)
        => NCombatRoom.Instance?.GetNodeOrNull<ArrowHud>("JunnaArrows")?.AnimateDeployment(tween, card, cardNode) == true;
    public override Vector2 GetTargetPosition(CardModel model, Vector2 size) => new(size.X * .5f, 90);
}
