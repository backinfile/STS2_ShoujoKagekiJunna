using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract class CatalogCard(string id) : JunnaCard(CardCatalog.Get(id).Cost,
    CardCatalog.Get(id).CardType, CardCatalog.Get(id).CardRarity, CardCatalog.Get(id).TargetType, id), ShoujoKageki.Core.V1.IShineDepletionHandler
{
    public Task OnShineDepleted(ShoujoKageki.Core.V1.ShineDepletionContext context) => KnowledgeProgress.Depleted(this, context);
    internal virtual Task OnPermanentShineDepleted(ShoujoKageki.Core.V1.ShineDepletionContext context) => Task.CompletedTask;
    public CardDefinition Definition => CardCatalog.Get(DesignId);
    public bool Converted { get; private set; }
    public override CardType Type => Converted ? Definition.InternalType : Definition.CardType;
    public override TargetType TargetType => Definition.TargetType;
    public override bool GainsBlock => Definition.CardType != CardType.Power && Definition.Values.Any(n => n.Name == "Block");
    protected override bool HasEnergyCostX => Definition.CostsX;
    public override HashSet<CardKeyword> CanonicalKeywords =>
        [..base.CanonicalKeywords, ..(Definition.Exhaust ? new[] { CardKeyword.Exhaust } : []), ..(Definition.Retain ? new[] { CardKeyword.Retain } : []),
         ..(Definition.Innate ? new[] { CardKeyword.Innate } : []),
         ..(Definition.Ethereal ? new[] { CardKeyword.Ethereal } : [])];
    protected override IEnumerable<DynamicVar> CanonicalVars => Definition.Values.Select(n => n.Name switch
    {
        "Damage" => (DynamicVar)new DamageVar(n.Base,
            Definition.CardType == CardType.Power || !Definition.DamageCapable ? ValueProp.Unpowered : ValueProp.Move),
        "Block" => new BlockVar(n.Base,
            Definition.CardType == CardType.Power ? ValueProp.Unpowered : ValueProp.Move),
        "Repeat" when Definition.DamageCapable && Definition.TargetType is TargetType.AnyEnemy or TargetType.AllEnemies
            => AttackHitCount.DeclarePrinted(n.Base),
        "Repeat" => new RepeatVar(n.Base),
        _ => new DynamicVar(n.Name, n.Base)
    });

    internal static int CountDeckKnowledge(CardModel card) => card.IsMutable
        ? card.Owner?.Deck.Cards.Count(CardCatalog.IsKnowledge) ?? 0 : 0;

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
    protected Task<DamageResult[]> Attack(PlayerChoiceContext context, CardPlay play)
    {
        var attack = DynamicVars.TryGetValue(CalculatedDamageVar.defaultName, out var damage)
            ? DamageCmd.Attack((CalculatedDamageVar)damage)
            : DamageCmd.Attack(Value("Damage"));
        return CardEffects.DealDamage(this, context, play, attack, AttackHitCount.Read(this, play.Target));
    }

    protected Task Block(CardPlay play, int? amount = null)
        => CreatureCmd.GainBlock(Owner.Creature, amount ?? Value("Block"), ValueProp.Move, play);

    protected Task<CardModel[]> ChooseCards(PlayerChoiceContext context, IEnumerable<CardModel> cards, int count = 1, bool optional = false)
        => CardActions.Choose(context, this, cards, count, optional);

    protected async Task GainRetainedEnergy(PlayerChoiceContext context, int amount)
    {
        var combat = Owner.PlayerCombatState!;
        int before = combat.Energy;
        await PlayerCmd.GainEnergy(amount, Owner);
        await EnergyReserve.Add(context, this, Math.Max(0, combat.Energy - before));
    }

    protected void ApplyCatalogUpgrade()
    {
        foreach (var value in Definition.Values)
            if (value.Upgraded != value.Base) DynamicVars[value.Name].UpgradeValueBy(value.Upgraded - value.Base);
        if (Definition.UpgradedCost != Definition.Cost) EnergyCost.UpgradeBy(Definition.UpgradedCost - Definition.Cost);
        if (Definition.UpgradedExhaust) AddKeyword(CardKeyword.Exhaust); else RemoveKeyword(CardKeyword.Exhaust);
        if (Definition.UpgradedRetain) AddKeyword(CardKeyword.Retain); else RemoveKeyword(CardKeyword.Retain);
        if (Definition.UpgradedInnate) AddKeyword(CardKeyword.Innate); else RemoveKeyword(CardKeyword.Innate);
        if (Definition.UpgradedEthereal) AddKeyword(CardKeyword.Ethereal); else RemoveKeyword(CardKeyword.Ethereal);
    }
}
