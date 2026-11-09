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
        if (card.CombatState == null && card.IsMutable && card.Owner != null)
        {
            int count = card is CatalogCard { DesignId: "JK206" } ? card.Owner.Deck.Cards.Count : CatalogCard.CountDeckKnowledge(card);
            var damage = new DamageVar(card.DynamicVars.Damage.BaseValue + count, Props);
            damage.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
            PreviewValue = damage.PreviewValue;
        }
        else base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        card.DynamicVars.Damage.PreviewValue = PreviewValue;
    }
}

internal sealed class KnowledgeBlockVar() : CalculatedBlockVar(ValueProp.Move)
{
    protected override DynamicVar GetBaseVar() => ((CardModel)_owner!).DynamicVars.Block;
    protected override DynamicVar GetExtraVar() => ((CardModel)_owner!).DynamicVars["N1"];

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        if (card.CombatState == null && card.IsMutable && card.Owner != null)
        {
            var block = new BlockVar(card.DynamicVars.Block.BaseValue +
                CatalogCard.CountDeckKnowledge(card) * card.DynamicVars["N1"].BaseValue, Props);
            block.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
            PreviewValue = block.PreviewValue;
        }
        else base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        card.DynamicVars.Block.PreviewValue = PreviewValue;
    }
}
