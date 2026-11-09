using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna.Models;

// Use the native Perfected Strike calculation while retaining the printed Damage variable.
internal sealed class KnowledgeDamageVar() : CalculatedDamageVar(ValueProp.Move)
{
    protected override DynamicVar GetBaseVar() => ((CardModel)_owner!).DynamicVars.Damage;

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        card.DynamicVars.Damage.PreviewValue = PreviewValue;
    }
}
