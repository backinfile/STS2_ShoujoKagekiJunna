using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Damage investment stays separate from printed damage, Strength and enchantments.</summary>
public readonly record struct DamageModifierSnapshot(decimal DamageBonus, int ExtraHits, Player? Owner)
{
    public bool IsEmpty => DamageBonus == 0 && ExtraHits == 0;
}

public static class DamageModifiers
{
    private sealed class Investment
    {
        public decimal Bonus;
        public int Hits;
        public bool HasReceivedBonus;
        public bool FirstDamageBonusCaptured;
    }

    private static ConditionalWeakTable<CardModel, Investment> _cards = new();

    private static Investment InvestmentFor(CardModel card) => _cards.GetValue(card, c => new Investment { Bonus = KnowledgeProgress.PermanentDamageBonus(c) });

    public static bool CanDealDamage(CardModel card) => DamageCapability.CanDealDamage(card);

    public static bool Add(CardModel card, decimal damageBonus = 0, int extraHits = 0)
    {
        bool changed = Apply(card, damageBonus, extraHits);
        if (changed) card.Owner.PlayerCombatState!.RecalculateCardValues();
        return changed;
    }

    public static void AddMany(IEnumerable<CardModel> cards, decimal damageBonus = 0, int extraHits = 0)
    {
        var owners = new HashSet<Player>();
        try
        {
            foreach (var card in cards.Distinct().ToArray())
                if (Apply(card, damageBonus, extraHits)) owners.Add(card.Owner);
        }
        finally
        {
            foreach (var owner in owners) owner.PlayerCombatState?.RecalculateCardValues();
        }
    }

    private static bool Apply(CardModel card, decimal damageBonus, int extraHits)
    {
        if (!card.IsMutable || card.Owner?.PlayerCombatState == null || !CanDealDamage(card)) return false;
        if (damageBonus < 0 || extraHits < 0) throw new ArgumentOutOfRangeException(nameof(damageBonus));
        var state = InvestmentFor(card);
        if (damageBonus > 0 && !state.FirstDamageBonusCaptured)
        {
            state.FirstDamageBonusCaptured = true;
            KnowledgeProgress.PreserveFirstBonus(card, damageBonus);
        }
        state.Bonus += damageBonus;
        state.Hits = checked(state.Hits + extraHits);
        state.HasReceivedBonus |= damageBonus > 0 || extraHits > 0;
        return true;
    }

    public static DamageModifierSnapshot CaptureDamageModifierSnapshot(CardModel card) =>
        _cards.TryGetValue(card, out var state)
            ? new(state.Bonus, checked(state.Hits + KnowledgeProgress.PermanentHits(card)), card.Owner)
            : new(KnowledgeProgress.PermanentDamageBonus(card), KnowledgeProgress.PermanentHits(card), card.IsMutable ? card.Owner : null);

    internal static bool HasReceivedBonus(CardModel card) =>
        _cards.TryGetValue(card, out var state) && state.HasReceivedBonus;

    internal static (AbstractModel? Source, DamageModifierSnapshot Snapshot) Resolve(PlayerChoiceContext? context, CardModel? explicitCard)
    {
        // Effect damage stays on the effect's own numbers, even if it passes its originating card.
        var producingModel = DamageExecution.ExplicitSource ?? context?.LastInvolvedModel;
        if (producingModel is PowerModel or OrbModel or RelicModel) return (producingModel, default);
        var model = explicitCard ?? producingModel;
        return model is CardModel card
            ? (card, CaptureDamageModifierSnapshot(card))
            : (model, default);
    }

    internal static int ExtraHits(DamageModifierSnapshot snapshot)
    {
        var endurance = snapshot.Owner?.Creature.GetPower<JunnaEndurancePower>()?.Amount ?? 0;
        return checked(snapshot.ExtraHits + (int)Math.Floor(snapshot.DamageBonus / 6m) * endurance);
    }

    internal static void Copy(AbstractModel original, AbstractModel clone)
    {
        if (original is CardModel card && clone is CardModel copied && _cards.TryGetValue(card, out var investment))
            _cards.AddOrUpdate(copied, new Investment { Bonus = investment.Bonus, Hits = investment.Hits, HasReceivedBonus = investment.HasReceivedBonus, FirstDamageBonusCaptured = investment.FirstDamageBonusCaptured });
    }

    public static void TransferBonus(IEnumerable<CardModel> sources, CardModel target)
    {
        if (!CanDealDamage(target)) return;
        decimal bonus = 0;
        foreach (var card in sources.Distinct().Where(c => c != target))
            { var state = InvestmentFor(card); bonus += state.Bonus; state.Bonus = 0; }
        Add(target, bonus);
    }

    internal static void ClearCombat() => _cards = new();
}
