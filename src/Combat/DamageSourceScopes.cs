using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Card plays own one extra-hit budget. Effect scopes keep indirect damage outside it.</summary>
internal static class DamageSourceScopes
{
    private static readonly HashSet<MethodBase> Patched = [];
    private static readonly Harmony Patcher = new(JunnaMod.Id + ".EffectSources");

    // Install at combat start, after every mod assembly has loaded, including mods loaded after Junna.
    internal static void Install()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic &&
            (a == typeof(CardModel).Assembly || a.GetReferencedAssemblies().Any(r => r.Name == "sts2")));
        foreach (var assembly in assemblies)
        foreach (var type in assembly.GetTypes().Where(t => !t.ContainsGenericParameters &&
                     (typeof(CardModel).IsAssignableFrom(t) || typeof(PowerModel).IsAssignableFrom(t) || typeof(OrbModel).IsAssignableFrom(t) ||
                      typeof(RelicModel).IsAssignableFrom(t))))
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            bool isCard = typeof(CardModel).IsAssignableFrom(type);
            if (isCard && (method.Name != "OnPlay" || method.GetParameters().Length != 2 ||
                method.GetParameters()[1].ParameterType != typeof(CardPlay))) continue;
            if (method.IsAbstract || method.ContainsGenericParameters || !typeof(Task).IsAssignableFrom(method.ReturnType) ||
                method.GetMethodBody() == null || !Patched.Add(method)) continue;
            Patcher.Patch(method, prefix: new(typeof(DamageSourceScopes), isCard ? nameof(CardPrefix) : nameof(Prefix)),
                postfix: new(typeof(DamageSourceScopes), nameof(Postfix)),
                finalizer: new(typeof(DamageSourceScopes), nameof(Finalizer)));
        }
    }

    private static void CardPrefix(CardModel __instance, CardPlay __1, out IDisposable __state) =>
        __state = DamageExecution.BeginCardPlay(__instance, __1);

    private static void Prefix(AbstractModel __instance, out IDisposable __state) =>
        __state = DamageExecution.UseDamageSource(__instance);
    private static void Postfix(IDisposable __state) => __state.Dispose();
    private static Exception? Finalizer(Exception? __exception, IDisposable? __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
