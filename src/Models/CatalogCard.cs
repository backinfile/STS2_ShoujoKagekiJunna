using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract class CatalogCard(string id) : JunnaCard(CardCatalog.Get(id).Cost,
    CardCatalog.Get(id).CardType, CardCatalog.Get(id).CardRarity, CardCatalog.Get(id).TargetType, id), ShoujoKageki.Core.V1.IShineDepletionHandler
{
    public Task OnShineDepleted(ShoujoKageki.Core.V1.ShineDepletionContext context) => KnowledgeProgress.Depleted(this, context);
    private string? _formId;
    public override string DesignId => _formId ?? base.DesignId;
    internal bool HasChangedForm => _formId != null;
    internal bool HasAlternateForm => base.DesignId is "JK49" or "JK49B";
    public CardDefinition Definition => CardCatalog.Get(DesignId);
    public bool Converted { get; private set; }
    public override CardType Type => Converted ? Definition.InternalType : Definition.CardType;
    public override TargetType TargetType => Definition.TargetType;
    public override bool GainsBlock => Definition.Values.Any(n => n.Name == "Block");
    protected override bool HasEnergyCostX => Definition.CostsX;
    public override int MaxUpgradeLevel => DesignId == "JK82" ? int.MaxValue : 1;
    public override HashSet<CardKeyword> CanonicalKeywords =>
        [..base.CanonicalKeywords, ..(Definition.Exhaust ? new[] { CardKeyword.Exhaust } : []), ..(Definition.Retain ? new[] { CardKeyword.Retain } : []),
         ..(Definition.Innate ? new[] { CardKeyword.Innate } : [])];
    private IEnumerable<CardNumber> FormValues => HasAlternateForm
        ? CardCatalog.Get("JK49").Values.Concat(CardCatalog.Get("JK49B").Values) : Definition.Values;
    protected override IEnumerable<DynamicVar> CanonicalVars => FormValues.Select(n => n.Name switch
    {
        "Damage" => (DynamicVar)new DamageVar(n.Base, ValueProp.Move),
        "Block" => new BlockVar(n.Base, ValueProp.Move),
        "Repeat" => new RepeatVar(n.Base),
        _ => new DynamicVar(n.Name, n.Base)
    });
    public int Number(int index) => DynamicVars[Definition.Values[index].Name].IntValue;
    public int Value(string name, int fallback = 0) => Definition.Values.Any(n => n.Name == name) ? DynamicVars[name].IntValue : fallback;

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
    internal void ChangeForm(string id)
    {
        AssertMutable();
        _formId = id;
        EnergyCost.SetCustomBaseCost(IsUpgraded ? Definition.UpgradedCost : Definition.Cost);
        Owner.PlayerCombatState?.RecalculateCardValues();
    }
#if STS2_BETA
    protected override CardLocation GetResultLocationForCardPlay() => CardCatalog.IsArrow(this)
        ? new(Owner, ArrowPile.PileId, CardPilePosition.Bottom) : base.GetResultLocationForCardPlay();
#else
    protected override PileType GetResultPileTypeForCardPlay() => CardCatalog.IsArrow(this)
        ? ArrowPile.PileId : base.GetResultPileTypeForCardPlay();
#endif
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (CardCatalog.IsArrow(this))
        {
            if (!play.IsFirstInSeries) return;
            int turns = Math.Max(0, Value("Turns") - (DesignId == "JA59" ? ResolveEnergyXValue() : 0));
            ArrowCombat.Deploy(this, play, turns, ResolveArrow);
            await CardPileCmd.Add(this, ArrowPile.PileId);
        }
        else await CardEffects.Resolve(this, context, play);
    }
    internal Task ResolveArrow(PlayerChoiceContext context, CardPlay play) => CardEffects.Resolve(this, context, play);
    protected override void OnUpgrade()
    {
        if (DesignId == "JK82")
        {
            DynamicVars[Definition.Values[0].Name].UpgradeValueBy(1);
            return;
        }
        foreach (var value in FormValues)
            if (value.Upgraded != value.Base) DynamicVars[value.Name].UpgradeValueBy(value.Upgraded - value.Base);
        if (Definition.UpgradedCost != Definition.Cost) EnergyCost.UpgradeBy(Definition.UpgradedCost - Definition.Cost);
        if (Definition.UpgradedExhaust) AddKeyword(CardKeyword.Exhaust); else RemoveKeyword(CardKeyword.Exhaust);
        if (Definition.UpgradedRetain) AddKeyword(CardKeyword.Retain); else RemoveKeyword(CardKeyword.Retain);
        if (Definition.UpgradedInnate) AddKeyword(CardKeyword.Innate); else RemoveKeyword(CardKeyword.Innate);
    }
}
