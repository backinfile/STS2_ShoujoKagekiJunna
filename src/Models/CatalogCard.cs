using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Models;

public abstract class CatalogCard(string id) : JunnaCard(CardCatalog.Get(id).Cost,
    CardCatalog.Get(id).CardType, CardCatalog.Get(id).CardRarity, CardCatalog.Get(id).TargetType, id), ShoujoKageki.Core.V1.IShineDepletionHandler
{
    public Task OnShineDepleted(ShoujoKageki.Core.V1.ShineDepletionContext context) => KnowledgeProgress.Depleted(this, context);
    public CardDefinition Definition => CardCatalog.Get(DesignId);
    public bool Converted { get; private set; }
    public override CardType Type => Converted ? Definition.InternalType : Definition.CardType;
    public override TargetType TargetType => DesignId == "JA119" ? TargetType.None : Definition.TargetType;
    public override bool GainsBlock => Definition.Values.Any(n => n.Name == "Block");
    protected override bool HasEnergyCostX => Definition.CostsX;
    public override int MaxUpgradeLevel => DesignId == "JK82" ? int.MaxValue : 1;
    public override HashSet<CardKeyword> CanonicalKeywords =>
        [..base.CanonicalKeywords, ..(Definition.Exhaust ? new[] { CardKeyword.Exhaust } : []), ..(Definition.Retain ? new[] { CardKeyword.Retain } : []),
         ..(Definition.Innate ? new[] { CardKeyword.Innate } : [])];
    protected override IEnumerable<DynamicVar> CanonicalVars => Definition.Values.Select(n => n.Name switch
    {
        "Damage" => (DynamicVar)new DamageVar(n.Base, ValueProp.Move),
        "Block" => new BlockVar(n.Base, ValueProp.Move),
        "Repeat" => new RepeatVar(n.Base),
        _ => new DynamicVar(n.Name, n.Base)
    }).Concat(DesignId is "JK48" or "JK206" ? new DynamicVar[]
    {
        new ExtraDamageVar(1),
        new KnowledgeDamageVar().WithMultiplier(static (card, _) =>
            card is CatalogCard { DesignId: "JK206" } ? card.Owner.Deck.Cards.Count :
                CountDeckKnowledge(card))
    } : []).Concat(DesignId == "JK48B" ? new DynamicVar[]
    {
        new KnowledgeBlockVar().WithMultiplier(static (card, _) => CountDeckKnowledge(card))
    } : []);

    internal static int CountDeckKnowledge(CardModel card) => card.IsMutable
        ? card.Owner?.Deck.Cards.Count(CardCatalog.IsKnowledge) ?? 0 : 0;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        switch (DesignId)
        {
            case "JK48": case "JK48B": description.Add("KnowledgeCount", CountDeckKnowledge(this)); break;
            case "JK206": description.Add("DeckCount", IsMutable ? Owner?.Deck.Cards.Count ?? 0 : 0); break;
            case "JK184": description.Add("HitCount", 1 + (IsMutable ? DamageModifiers.ExtraHits(DamageModifiers.CaptureDamageModifierSnapshot(this)) : 0)); break;
            case "JA82":
                description.Add("EnergyCount", !IsMutable ? 0 : Pile?.Type == PileType.Play || Pile?.Type == ArrowPile.PileId
                    ? this.ResolveEnergyXValue() : Owner?.PlayerCombatState?.Energy ?? 0);
                break;
        }
    }
    public int Number(int index) => DynamicVars[Definition.Values[index].Name].IntValue;
    public int Value(string name, int fallback = 0)
    {
        string field = CardValueNames.Resolve(DesignId, name);
        return Definition.Values.Any(value => value.Name == field) ? DynamicVars[field].IntValue : fallback;
    }

    public void ConvertArrow()
    {
        AssertMutable();
        Converted = true;
        RemoveKeyword(JunnaKeywords.Arrow);
        EnergyCost.AddThisCombat(1);
        InvokeEnergyCostChanged();
    }
    public void SetGeneratedDamage(int damage)
    {
        AssertMutable();
        DynamicVars.Damage.BaseValue = damage;
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => CardEffects.Resolve(this, context, play);
    internal Task ResolveArrow(PlayerChoiceContext context, CardPlay play) => CardEffects.Resolve(this, context, play);
    protected override void OnUpgrade()
    {
        if (DesignId == "JK82")
        {
            DynamicVars[CardValueNames.Resolve(DesignId, "Energy")].UpgradeValueBy(1);
            return;
        }
        foreach (var value in Definition.Values)
            if (value.Upgraded != value.Base) DynamicVars[value.Name].UpgradeValueBy(value.Upgraded - value.Base);
        if (Definition.UpgradedCost != Definition.Cost) EnergyCost.UpgradeBy(Definition.UpgradedCost - Definition.Cost);
        if (Definition.UpgradedExhaust) AddKeyword(CardKeyword.Exhaust); else RemoveKeyword(CardKeyword.Exhaust);
        if (Definition.UpgradedRetain) AddKeyword(CardKeyword.Retain); else RemoveKeyword(CardKeyword.Retain);
        if (Definition.UpgradedInnate) AddKeyword(CardKeyword.Innate); else RemoveKeyword(CardKeyword.Innate);
    }
}
