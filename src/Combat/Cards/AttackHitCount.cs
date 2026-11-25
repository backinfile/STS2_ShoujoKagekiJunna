using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
#if STS2_BETA
using NativeHitCountVar = MegaCrit.Sts2.Core.Localization.DynamicVars.HitCountVar;
using NativeCalculatedHitCountVar = MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedHitCountVar;
#else
using NativeHitCountVar = MegaCrit.Sts2.Core.Localization.DynamicVars.RepeatVar;
using NativeCalculatedHitCountVar = MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedVar;
#endif

namespace ShoujoKagekiJunna.Combat;

internal static class AttackHitCount
{
    private const string CalculatedHits = "CalculatedHits";

    internal static int Read(CardModel card, Creature? target)
    {
        if (card.DynamicVars.TryGetValue(CalculatedHits, out var variable) && variable is CalculatedVar calculated)
            return (int)calculated.Calculate(target);
        return card.DynamicVars.Select(pair => pair.Value).OfType<NativeHitCountVar>().FirstOrDefault()?.IntValue ?? 1;
    }

    internal static DynamicVar DeclarePrinted(int hits) => new NativeHitCountVar(RepeatVar.defaultName, hits);

    internal static DynamicVar[] DeclareCalculated(Func<CardModel, Creature?, decimal> extraHits)
        => [new CalculationBaseVar(1), new CalculationExtraVar(1),
            new NativeCalculatedHitCountVar(CalculatedHits).WithMultiplier(extraHits)];
}
