using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    protected override string SmartDescriptionLocKey => "JUNNA_CATALOG.smartDescription";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new StringVar("Effects", Description.GetFormattedText())];

    private int StackAmount() => DesignId switch
    {
        "JA04" => _grants.Sum(g => Math.Max(0, g.N(0) - g.Used)),
        "A" or "JA35" or "JK32" or "JK139" or "JA87" or "JA96" => _grants.Sum(g => g.N(0)),
        "JG31" or "JK67" => _grants.Sum(g => g.V("Bonus")),
        "JH21" or "JH23" or "JA70" => _grants.Sum(g => g.V("Block")),
        "JK80" or "JH31" => _grants.Sum(g => g.N(1)),
        _ => _grants.Count
    };

    private void Synchronize()
    {
        SetAmount(StackType == PowerStackType.Single ? 1 : StackAmount());
        ((StringVar)DynamicVars["Effects"]).StringValue = RenderEffects();
        InvokeDisplayAmountChanged();
    }

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
        int Sum(int index) => _grants.Sum(g => g.N(index));
        int Bonus() => _grants.Sum(g => g.V("Bonus"));
        int Block() => _grants.Sum(g => g.V("Block"));
        IEnumerable<string> Groups<TKey>(Func<Grant, TKey> key, Func<IGrouping<TKey, Grant>, string> render)
            => _grants.GroupBy(key).Select(render);
        IEnumerable<string> lines = DesignId switch
        {
            "JA04" => [Format(("Amount", StackAmount()))],
            "JA22" or "JK27" => [Format()],
            "JA105" => [Format(("Amount", _grants.Count))],
            "A" or "JA35" or "JK32" or "JK139" or "JA87" or "JA96" => [Format(("Amount", Sum(0)))],
            "JG31" or "JK67" => [Format(("Amount", Bonus()))],
            "JH19" or "JZ06" or "JH30" => [Format(("Amount", _grants.Count), ("Repetition", Repetition(_grants.Count)))],
            "JH21" or "JA70" => [Format(("Amount", Block()))],
            "JK80" or "JH31" => [Format(("Amount", Sum(1)), ("Threshold", _grants[0].N(0)))],
            "JK127" => [Format(("Amount", _grants.Count), ("Repetition", Repetition(_grants.Count)))],
            "JK15" => [Format(("Amount", _grants.Count))],
            "JK81" => [Format(("Threshold", _grants.Min(g => g.N(0))))],
            "JK10" => Groups(g => g.N(0), group => Format(("Threshold", group.Key), ("Draw", group.Sum(g => g.N(1))), ("Energy", group.Sum(g => g.N(2))))),
            "JA45" => Groups(g => g.N(0), group => Format(("Limit", group.Key), ("Turns", group.Sum(g => g.N(1))))),
            "JH23" => Groups(g => (Limit: g.N(0), Threshold: g.N(1)), group => Format(("Limit", group.Key.Limit), ("Threshold", group.Key.Threshold), ("Amount", group.Sum(g => g.V("Block"))))),
            "JK37C" => Groups(g => g.N(1), group => Format(("Turns", group.Key), ("Repetition", Repetition(group.Count())))),
            "JK64" => Groups(g => g.N(0), group => Format(("Turns", group.Key), ("Repetition", Repetition(group.Count())))),
            "JK75" => Groups(g => g.V("Bonus"), group => Format(("Bonus", group.Key), ("Repetition", Repetition(group.Count())))),
            "JZ05" => Groups(g => g.N(0), group => Format(("Limit", group.Key), ("Amount", group.Count()))),
            "JK46" => Groups(g => g.Card.IsUpgraded, group => Format(("Amount", group.Count()), ("Card", CardCatalog.Get("JK46B").Name + (group.Key ? "+" : "")))),
            "JG34" => Groups(g => g.V("Damage") + DamageModifiers.CaptureDamageModifierSnapshot(g.Card).DamageBonus,
                group => Format(("Damage", group.Key), ("Repeat", group.Sum(g => g.BaseHits * (1 + DamageModifiers.ExtraHits(DamageModifiers.CaptureDamageModifierSnapshot(g.Card))))))),
            _ => throw new InvalidOperationException("Missing power description: " + DesignId)
        };
        return string.Join("\n", lines);
    }
}