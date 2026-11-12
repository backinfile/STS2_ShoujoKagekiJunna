using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    protected override string SmartDescriptionLocKey => "JUNNA_CATALOG.smartDescription";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new StringVar("Effects", Description.GetFormattedText())];

    private int StackAmount() => CardRules.Get(DesignId).PowerAmount switch
    {
        PowerAmountKind.RemainingLimit => _grants.Sum(g => Math.Max(0, g.V("Limit") - g.Used)),
        PowerAmountKind.Threshold => _grants.Sum(g => g.V("Threshold")),
        PowerAmountKind.Bonus => _grants.Sum(g => g.V("Bonus")),
        PowerAmountKind.Block => _grants.Sum(g => g.V("Block")),
        PowerAmountKind.Amount => _grants.Sum(g => g.V("Amount")),
        PowerAmountKind.Damage => _grants.Sum(g =>
        {
            var (damage, hits) = DamagePayload(g);
            return checked((int)Math.Floor(damage) * hits);
        }),
        _ => _grants.Count
    };

    private static (decimal Damage, int Hits) DamagePayload(Grant grant)
    {
        var modifiers = DamageModifiers.CaptureDamageModifierSnapshot(grant.Card);
        return (Math.Max(0, grant.V("Damage") + modifiers.DamageBonus),
            grant.BaseHits * (1 + DamageModifiers.ExtraHits(modifiers)));
    }

    protected void Synchronize()
    {
        SetAmount(StackType == PowerStackType.Single ? 1 : StackAmount());
        ((StringVar)DynamicVars["Effects"]).StringValue = RenderEffects();
        InvokeDisplayAmountChanged();
    }

    protected virtual int? RemainingTriggers => null;

    private string RenderEffects()
    {
        string Format(params (string Name, object Value)[] values)
        {
            var text = new LocString("powers", Id.Entry + ".smartDescription");
            foreach (var (name, value) in values) text.AddObj(name, value);
            return text.GetFormattedText();
        }
        string Repetition(int count)
        {
            if (count == 1) return "";
            var text = new LocString("powers", "JUNNA_CATALOG.repetition");
            text.Add("Amount", count);
            return text.GetFormattedText();
        }
        int Sum(string field) => _grants.Sum(g => g.V(field));
        int Bonus() => _grants.Sum(g => g.V("Bonus"));
        int Block() => _grants.Sum(g => g.V("Block"));
        IEnumerable<string> Groups<TKey>(Func<Grant, TKey> key, Func<IGrouping<TKey, Grant>, string> render)
            => _grants.GroupBy(key).Select(render);
        IEnumerable<string> lines = CardRules.Get(DesignId).PowerDescription switch
        {
            PowerDescriptionKind.RemainingUses => [Format(("Amount", StackAmount()))],
            PowerDescriptionKind.Passive => Groups(g => g.Card.IsUpgraded, group => Format(("Upgraded", group.Key))),
            PowerDescriptionKind.Applications => [Format(("Amount", _grants.Count))],
            PowerDescriptionKind.ThresholdSum => [Format(("Amount", Sum("DisplayedAmount")))],
            PowerDescriptionKind.BonusSum => [Format(("Amount", Bonus()))],
            PowerDescriptionKind.RepeatedApplications => [Format(("Amount", _grants.Count), ("Repetition", Repetition(_grants.Count)))],
            PowerDescriptionKind.BlockSum => [Format(("Amount", Block()))],
            PowerDescriptionKind.AmountWithThreshold => [Format(("Amount", Sum("Amount")), ("Threshold", _grants[0].V("Threshold")))],
            PowerDescriptionKind.MinimumThreshold => [Format(("Threshold", _grants.Min(g => g.V("Threshold"))))],
            PowerDescriptionKind.EnergyThresholdGroups => Groups(g => g.V("EnergyThreshold"), group => Format(("Threshold", group.Key), ("Draw", group.Sum(g => g.V("DrawCount"))), ("Energy", group.Sum(g => g.V("Energy"))))),
            PowerDescriptionKind.ArrowLimitGroups => Groups(g => g.V("Limit"), group => Format(("Limit", group.Key), ("Turns", group.Sum(g => g.V("Turns"))))),
            PowerDescriptionKind.BlockLimitGroups => Groups(g => (Limit: g.V("EnergyThreshold"), Threshold: g.V("EnergyThreshold2")), group => Format(("Limit", group.Key.Limit), ("Threshold", group.Key.Threshold), ("Amount", group.Sum(g => g.V("Block"))))),
            PowerDescriptionKind.TurnGroups => Groups(g => g.V("Turns"), group => Format(("Turns", group.Key), ("Repetition", Repetition(group.Count())))),
            PowerDescriptionKind.BonusGroups => Groups(g => g.V("Bonus"), group => Format(("Bonus", group.Key), ("Repetition", Repetition(group.Count())))),
            PowerDescriptionKind.LimitGroups => Groups(g => g.V("Limit"), group => Format(("Limit", group.Key), ("Amount", group.Count()))),
            PowerDescriptionKind.GeneratedCardGroups => Groups(g => g.Card.IsUpgraded, group => Format(("Amount", group.Count()), ("Card", CardCatalog.Get("JK46B").Name + (group.Key ? "+" : "")))),
            PowerDescriptionKind.DamageGroups => Groups(g => DamagePayload(g).Damage,
                group => Format(("Damage", group.Key), ("Repeat", group.Sum(g => DamagePayload(g).Hits)))),
            _ => throw new InvalidOperationException("Missing power description: " + DesignId)
        };
        if (RemainingTriggers is { } remaining)
        {
            var progress = new LocString("powers", "JUNNA_CATALOG.remainingTriggers");
            progress.Add("Amount", remaining);
            lines = lines.Append(progress.GetFormattedText());
        }
        return string.Join("\n", lines);
    }
}
