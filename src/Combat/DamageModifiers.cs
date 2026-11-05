using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Combat-only investment. Values are never written into base damage, Strength or enchantments.</summary>
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
    }

    private static ConditionalWeakTable<CardModel, Investment> _cards = new();

    public static bool CanDealDamage(CardModel card) => DamageCapability.CanDealDamage(card);

    public static bool Add(CardModel card, decimal damageBonus = 0, int extraHits = 0)
    {
        if (!card.IsMutable || card.Owner?.PlayerCombatState == null || !CanDealDamage(card)) return false;
        if (damageBonus < 0 || extraHits < 0) throw new ArgumentOutOfRangeException(nameof(damageBonus));
        var state = _cards.GetOrCreateValue(card);
        state.Bonus += damageBonus;
        state.Hits = checked(state.Hits + extraHits);
        card.Owner.PlayerCombatState.RecalculateCardValues();
        return true;
    }

    public static DamageModifierSnapshot CaptureDamageModifierSnapshot(CardModel card) =>
        _cards.TryGetValue(card, out var state)
            ? new(state.Bonus, state.Hits, card.Owner)
            : new(0, 0, card.IsMutable ? card.Owner : null);

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

    internal static HoverTip? InvestmentTip(CardModel card)
    {
        var snapshot = CaptureDamageModifierSnapshot(card);
        int hits = ExtraHits(snapshot);
        if (snapshot.DamageBonus == 0 && hits == 0) return null;
        var lines = new List<string>();
        if (snapshot.DamageBonus != 0)
        {
            var text = new LocString("gameplay_ui", "JUNNA.INVESTMENT_BONUS");
            text.Add("Amount", snapshot.DamageBonus);
            lines.Add(text.GetFormattedText());
        }
        if (hits != 0)
        {
            var text = new LocString("gameplay_ui", "JUNNA.INVESTMENT_HITS");
            text.Add("Amount", hits);
            lines.Add(text.GetFormattedText());
        }
        return new HoverTip(new LocString("gameplay_ui", "JUNNA.INVESTMENT"), string.Join("\n", lines)) { Id = "junna_investment" };
    }

    internal static void Copy(AbstractModel original, AbstractModel clone)
    {
        if (original is CardModel card && clone is CardModel copied && _cards.TryGetValue(card, out var investment))
            _cards.AddOrUpdate(copied, new Investment { Bonus = investment.Bonus, Hits = investment.Hits });
    }

    public static void TransferBonus(IEnumerable<CardModel> sources, CardModel target)
    {
        if (!CanDealDamage(target)) return;
        decimal bonus = 0;
        foreach (var card in sources.Distinct().Where(c => c != target))
            if (_cards.TryGetValue(card, out var state)) { bonus += state.Bonus; state.Bonus = 0; }
        Add(target, bonus);
    }

    internal static void ClearCombat() => _cards = new();

    internal static void Describe(CardModel card, Creature? target, ref string description)
    {
        var state = CaptureDamageModifierSnapshot(card);
        if (state.IsEmpty) return;
        var parts = new List<string>();
        if (state.DamageBonus != 0) parts.Add($"伤害加成 +{state.DamageBonus}");
        if (state.ExtraHits != 0) parts.Add($"额外伤害 +{state.ExtraHits}次");
        description += "\n[gold]" + string.Join(" · ", parts) + "[/gold]";
    }
}
