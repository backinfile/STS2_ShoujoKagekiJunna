using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    public override int DisplayAmount => _grants.Count == 0 ? Amount : StackAmount();
    private bool UsesNativeDescription => CardRules.Get(DesignId).PowerDescription == PowerDescriptionKind.Native;
    protected override string SmartDescriptionLocKey => UsesNativeDescription
        ? base.SmartDescriptionLocKey : "JUNNA_CATALOG.smartDescription";
    protected override IEnumerable<DynamicVar> CanonicalVars => UsesNativeDescription
        ? base.CanonicalVars : [new StringVar("Effects", Description.GetFormattedText())];

    protected virtual int StackAmount() => CardRules.Get(DesignId).PowerAmount switch
    {
        PowerAmountKind.Native => Amount,
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
        return (Math.Max(0, grant.V("Damage")), grant.BaseHits);
    }

    private void RefreshDescription()
    {
        if (!UsesNativeDescription) ((StringVar)DynamicVars["Effects"]).StringValue = RenderEffects();
    }

    protected void Synchronize()
    {
        RefreshDescription();
        InvokeDisplayAmountChanged();
    }

    protected virtual int? RemainingTriggers => null;

    protected virtual int CategoryCycleIndex(Grant grant) => grant.CycleIndex;

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
        int Block() => _grants.Sum(g => g.V("Block"));
        IEnumerable<string> Groups<TKey>(Func<Grant, TKey> key, Func<IGrouping<TKey, Grant>, string> render)
            => _grants.GroupBy(key).Select(render);
        IEnumerable<string> lines = CardRules.Get(DesignId).PowerDescription switch
        {
            PowerDescriptionKind.RemainingUses => [Format(("Amount", StackAmount()))],
            PowerDescriptionKind.Passive => Groups(g => g.Card.IsUpgraded, group => Format(("Upgraded", group.Key))),
            PowerDescriptionKind.Applications => [Format(("Amount", _grants.Count))],
            PowerDescriptionKind.ThresholdSum => [Format(("Amount", Sum("DisplayedAmount")))],
            PowerDescriptionKind.RepeatedApplications => [Format(("Amount", _grants.Count), ("Repetition", Repetition(_grants.Count)))],
            PowerDescriptionKind.BlockSum => [Format(("Amount", Block()))],
            PowerDescriptionKind.AmountWithThreshold => [Format(("Amount", Sum("Amount")), ("Threshold", _grants[0].V("Threshold")))],
            PowerDescriptionKind.MinimumThreshold => [Format(("Threshold", _grants.Min(g => g.V("Threshold"))))],
            PowerDescriptionKind.EnergyThresholdGroups => Groups(g => g.V("EnergyThreshold"), group => Format(("Threshold", group.Key), ("Draw", group.Sum(g => g.V("DrawCount"))), ("Energy", group.Sum(g => g.V("Energy"))))),
            PowerDescriptionKind.ArrowLimitGroups => Groups(g => g.V("Limit"), group => Format(("Limit", group.Key), ("Turns", group.Sum(g => g.V("Turns"))))),
            PowerDescriptionKind.BlockLimitGroups => Groups(g => (Limit: g.V("EnergyThreshold"), Threshold: g.V("EnergyThreshold2")), group => Format(("Limit", group.Key.Limit), ("Threshold", group.Key.Threshold), ("Amount", group.Sum(g => g.V("Block"))))),
            PowerDescriptionKind.TurnGroups => Groups(g => g.V("Turns"), group => Format(("Turns", group.Key), ("Repetition", Repetition(group.Count())))),
            PowerDescriptionKind.LimitGroups => Groups(g => g.V("Limit"), group => Format(("Limit", group.Key), ("Amount", group.Count()))),
            PowerDescriptionKind.GeneratedCardGroups => Groups(g => g.Card.IsUpgraded, group => Format(("Amount", group.Count()), ("Card", CardCatalog.Get("JK46B").Name + (group.Key ? "+" : "")))),
            PowerDescriptionKind.DamageGroups => Groups(g => DamagePayload(g).Damage,
                group => Format(("Damage", group.Key), ("Repeat", group.Sum(g => DamagePayload(g).Hits)))),
            PowerDescriptionKind.DamageSum => [Format(("Damage", StackAmount()))],
            PowerDescriptionKind.CategoryGroups => Groups(CategoryCycleIndex, group => Format(
                ("Category", new LocString("cards", new[] { "JUNNA_PREPARE_KNOWLEDGE.title", "JUNNA_PREPARE_ARROWS.title", "JUNNA_PREPARE_SHINE.title" }[group.Key]).GetFormattedText()),
                ("Amount", group.Sum(g => g.V("DrawCount"))))),
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
