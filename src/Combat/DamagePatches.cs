using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class InvestmentHoverTipPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (DamageModifiers.InvestmentTip(__instance) is { } tip) __result = __result.Append(tip);
    }
}

[HarmonyPatch]
internal static class DamagePatches
{
    internal static readonly AsyncLocal<bool> Bypass = new();
    internal static MethodInfo CanonicalDamage => AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
        [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp),
         typeof(Creature), typeof(CardModel), typeof(CardPlay)]);
    static MethodBase TargetMethod() => CanonicalDamage;

    [HarmonyPrefix]
    static bool Prefix(PlayerChoiceContext choiceContext, IEnumerable<Creature>? targets, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, ref Task<IEnumerable<DamageResult>> __result)
    {
        if (Bypass.Value) { Bypass.Value = false; return true; }
        __result = DamageExecution.Resolve(choiceContext, targets, amount, props, dealer, cardSource, cardPlay);
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class DamageBonusPatch
{
    [HarmonyPrefix]
    static void Prefix(Creature? target, CardModel? cardSource, ModifyDamageHookType modifyDamageHookType, ref decimal damage)
    {
        if (!modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive)) return;
        var frame = DamageExecution.Current.Value;
        var snapshot = frame != null && (cardSource == null || frame.Source is not CardModel || ReferenceEquals(frame.Source, cardSource))
            ? frame.Snapshot
            : cardSource == null ? default : DamageModifiers.CaptureDamageModifierSnapshot(cardSource);
        if (snapshot.Owner == null || (target != null && target.Side == snapshot.Owner.Creature.Side)) return;
        damage += snapshot.DamageBonus;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))]
internal static class AttackHitCountPatch
{
    [HarmonyPostfix]
    static void Postfix(AttackCommand attackCommand, ref decimal __result)
    {
        var (source, snapshot) = DamageModifiers.Resolve(null, attackCommand.ModelSource as CardModel);
        if (source is not CardModel card || __result <= 0 || snapshot.Owner == null ||
            attackCommand.TargetSide == snapshot.Owner.Creature.Side) return;
        DamageExecution.PrepareAttack(attackCommand, __result);
        __result += DamageExecution.ClaimExtraHits(card, attackCommand.CardPlay, snapshot);
    }
}

[HarmonyPatch]
internal static class AttackBatchPatch
{
    static MethodBase TargetMethod() => AccessTools.Method(
        AccessTools.Method(typeof(AttackCommand), nameof(AttackCommand.Execute))
            .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var commandField = __originalMethod.DeclaringType!.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(f => f.FieldType == typeof(AttackCommand));
        int replacements = 0;
        foreach (var instruction in instructions)
        {
            bool isDamage = instruction.Calls(DamagePatches.CanonicalDamage);
            bool isCallback = instruction.Calls(AccessTools.Method(typeof(Func<Task>), nameof(Func<Task>.Invoke)));
            if (isDamage || isCallback)
            {
                var load = new CodeInstruction(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                yield return load;
                yield return new CodeInstruction(OpCodes.Ldfld, commandField);
                yield return CodeInstruction.Call(typeof(DamageExecution), isDamage
                    ? nameof(DamageExecution.FromAttack) : nameof(DamageExecution.RunOriginalHitCallback));
                if (isDamage) replacements++;
            }
            else yield return instruction;
        }
        if (replacements != 1) throw new InvalidOperationException($"Junna: expected one AttackCommand damage boundary, found {replacements}.");
    }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
internal static class DamageClonePatch
{
    static void Postfix(AbstractModel __instance, AbstractModel __result) => DamageModifiers.Copy(__instance, __result);
}

