using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>
/// Finds direct card damage instructions, including helpers and async state machines.
/// Does not use card type or DamageVar as proof. Generated/played cards are separate sources.
/// Register an adapter for opaque schedulers/native code; unknown models remain ineligible.
/// </summary>
public static class DamageCapability
{
    private static readonly ConcurrentDictionary<Type, bool> Cache = new();
    private static readonly Dictionary<Type, Func<CardModel, bool>> Adapters = [];

    public static void Register<T>(Func<T, bool> canDealDamage) where T : CardModel
    {
        Adapters[typeof(T)] = card => canDealDamage((T)card);
        Cache.Clear();
    }

    public static void Register(Type cardType, Func<CardModel, bool> canDealDamage)
    {
        if (!typeof(CardModel).IsAssignableFrom(cardType)) throw new ArgumentException("Expected a card model", nameof(cardType));
        Adapters[cardType] = canDealDamage;
        Cache.Clear();
    }

    public static bool CanDealDamage(CardModel card) => Adapters.TryGetValue(card.GetType(), out var adapter)
        ? adapter(card)
        : Cache.GetOrAdd(card.GetType(), type => ScanModel(type, new HashSet<MethodBase>(), new HashSet<Type>()));

    private static bool ScanModel(Type type, HashSet<MethodBase> visited, HashSet<Type> models)
    {
        if (!models.Add(type)) return false;
        // Inspect overrides and helpers in the concrete model's inheritance chain, stopping at engine model bases.
        for (var current = type; current != null && current != typeof(CardModel) && current != typeof(PowerModel)
             && current != typeof(OrbModel) && current != typeof(AbstractModel); current = current.BaseType)
            foreach (var method in current.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                      BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!method.IsSpecialName && ScanMethod(method, type.Assembly, visited, models)) return true;
        return false;
    }

    private static bool ScanMethod(MethodBase method, Assembly modelAssembly, HashSet<MethodBase> visited, HashSet<Type> models)
    {
        if (!visited.Add(method) || visited.Count > 512 || method.GetMethodBody() == null) return false;
        var asyncType = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (asyncType != null && ScanMethod(AccessTools.Method(asyncType, "MoveNext"), modelAssembly, visited, models)) return true;
        var iteratorType = method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
        if (iteratorType != null && ScanMethod(AccessTools.Method(iteratorType, "MoveNext"), modelAssembly, visited, models)) return true;
        try
        {
            foreach (var instruction in PatchProcessor.GetOriginalInstructions(method))
            {
                if (instruction.operand is not MethodBase called) continue;
                var declaring = called.DeclaringType;
                if ((declaring == typeof(CreatureCmd) && called.Name == nameof(CreatureCmd.Damage)) ||
                    (declaring == typeof(AttackCommand) && called.Name == nameof(AttackCommand.Execute))) return true;
                if (declaring == typeof(PowerCmd) || declaring == typeof(OrbCmd) || declaring == typeof(ModelDb) ||
                    declaring != null && (typeof(PowerModel).IsAssignableFrom(declaring) ||
                        typeof(OrbModel).IsAssignableFrom(declaring) || typeof(RelicModel).IsAssignableFrom(declaring))) continue;
                // Follow model helpers, delegates and mod utilities. Engine commands are integration boundaries.
                if (declaring == null || declaring == typeof(CardModel) || declaring == typeof(AbstractModel) ||
                    declaring == typeof(PowerModel) || declaring == typeof(OrbModel)) continue;
                bool helper = declaring.Assembly == modelAssembly &&
                    (!declaring.Namespace?.StartsWith("MegaCrit.Sts2.Core.") == true ||
                     declaring.Namespace?.Contains(".Models.") == true);
                if (helper && !typeof(CardModel).IsAssignableFrom(declaring) && called.Name is ".ctor" or ".cctor") continue;
                if (helper && ScanMethod(called, modelAssembly, visited, models)) return true;
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or InvalidOperationException)
        {
            JunnaMod.Log.Debug($"Damage capability unresolved: {method.DeclaringType?.FullName}.{method.Name}: {ex.Message}");
        }
        return false;
    }
}
