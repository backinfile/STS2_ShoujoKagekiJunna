using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

// Use the native Perfected Strike calculation while retaining the printed Damage variable.
internal sealed class CountedDamageVar : CalculatedDamageVar
{
    private readonly Func<CardModel, int> _count;
    private readonly string? _extraValue;

    internal CountedDamageVar(Func<CardModel, int> count, string? extraValue = null) : base(ValueProp.Move)
    {
        _count = count;
        _extraValue = extraValue;
        WithMultiplier((card, _) => count(card));
    }

    protected override DynamicVar GetBaseVar() => ((CardModel)_owner!).DynamicVars.Damage;
    protected override DynamicVar GetExtraVar() => _extraValue == null
        ? base.GetExtraVar()
        : ((CardModel)_owner!).DynamicVars[_extraValue];

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        if (card.CombatState == null && card.IsMutable && card.Owner != null)
        {
            var damage = new DamageVar(card.DynamicVars.Damage.BaseValue + _count(card) * GetExtraVar().BaseValue, Props);
            damage.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
            PreviewValue = damage.PreviewValue;
        }
        else base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        if (_extraValue == null) card.DynamicVars.Damage.PreviewValue = PreviewValue;
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
