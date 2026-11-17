using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKagekiJunna.Models;

// Only the run's listener inventory is substituted; preview and power hooks are native.
var run = DispatchProxy.Create<IRunState, PreviewRun>();
var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
var creature = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
SetField(player, "_runState", run);
SetField(player, "_runPiles", Array.Empty<CardPile>());
SetField(player, "<Creature>k__BackingField", creature);
var vigor = new VigorPower();
typeof(AbstractModel).GetProperty(nameof(AbstractModel.IsMutable))!.SetValue(vigor, true);
SetField(vigor, "_owner", creature);
SetField(vigor, "_amount", 10);
SetField(vigor, "_internalData", typeof(VigorPower).GetMethod("InitInternalData", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vigor, null));
((PreviewRun)(object)run).Listeners = [vigor];
int failures = 0, checks = 0;
foreach (var (make, baseDamage, upgradedDamage, expectedType) in new (Func<CatalogCard>, int, int, CardType)[]
{
    (() => new JunnaCardU_CARD_030(), 8, 12, CardType.Skill),
    (() => new JunnaCardJA82(), 14, 18, CardType.Skill),
    (() => new JunnaCardJK400B(), 8, 8, CardType.Power),
    (() => new JunnaCardJK235(), 3, 4, CardType.Power),
    (() => new JunnaCardJG34(), 3, 4, CardType.Power)
})
foreach (bool upgraded in new[] { false, true })
{
    var card = make();
    typeof(AbstractModel).GetProperty(nameof(AbstractModel.IsMutable))!.SetValue(card, true);
    if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
    SetField(card, "_owner", player);
    Check($"{card.DesignId} upgraded={upgraded} keeps its card type", card.Type, expectedType);
    var damage = card.DynamicVars.Damage;
    damage.UpdateCardPreview(card, CardPreviewMode.Normal, null, runGlobalHooks: true);
    Check($"{card.DesignId} upgraded={upgraded} damage preview", damage.PreviewValue, (decimal)(upgraded ? upgradedDamage : baseDamage));
    Check($"{card.DesignId} upgraded={upgraded} base damage", card.Value("Damage"), upgraded ? upgradedDamage : baseDamage);
}
// These cards deal their own damage before generating another attack.
foreach (var make in new Func<CatalogCard>[]
{
    () => new JunnaCardJA76(), () => new JunnaCardJA81(), () => new JunnaCardU_CARD_005()
})
foreach (bool upgraded in new[] { false, true })
{
    var card = make();
    typeof(AbstractModel).GetProperty(nameof(AbstractModel.IsMutable))!.SetValue(card, true);
    if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
    SetField(card, "_owner", player);
    card.DynamicVars.Damage.UpdateCardPreview(card, CardPreviewMode.Normal, null, runGlobalHooks: true);
    Check($"{card.DesignId} upgraded={upgraded} direct damage still receives Vigor",
        card.DynamicVars.Damage.PreviewValue, card.DynamicVars.Damage.BaseValue + 10m);
}
var meteor = new JunnaCardJA81B();
typeof(AbstractModel).GetProperty(nameof(AbstractModel.IsMutable))!.SetValue(meteor, true);
SetField(meteor, "_owner", player);
meteor.SetGeneratedDamage(8);
meteor.DynamicVars.Damage.UpdateCardPreview(meteor, CardPreviewMode.Normal, null, runGlobalHooks: true);
Check("generated Meteor still receives native Vigor", meteor.DynamicVars.Damage.PreviewValue, 18m);
Check("preview does not consume Vigor", vigor.Amount, 10);
Console.WriteLine($"{checks - failures}/{checks} checks passed");
return failures == 0 ? 0 : 1;

void Check<T>(string name, T actual, T expected)
{
    checks++;
    bool pass = Equals(actual, expected);
    if (!pass) failures++;
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")}: {name}: actual={actual}, expected={expected}");
}
static void SetField(object target, string name, object? value)
{
    for (var type = target.GetType(); type != null; type = type.BaseType)
        if (type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } field)
        { field.SetValue(target, value); return; }
    throw new MissingFieldException(target.GetType().FullName, name);
}
public class PreviewRun : DispatchProxy
{
    public IEnumerable<AbstractModel> Listeners = [];
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name == "IterateHookListeners"
        ? Listeners : throw new NotSupportedException(targetMethod?.Name);
}
