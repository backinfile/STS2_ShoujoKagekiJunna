using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace ShoujoKagekiJunna.Loader;

[ModInitializer(nameof(Initialize))]
public static class Bootstrap
{
    private const string ModId = "ShoujoKagekiJunna";
    private static readonly Logger Log = new(ModId, LogType.Generic);
    private static Assembly? _implementation;
    internal static bool UsesStableApi { get; private set; }
    internal static Type[] ImplementationTypes => _implementation?.GetTypes() ?? [];

    public static void Initialize()
    {
        if (_implementation != null) return;
        var version = ReleaseInfoManager.Instance.SemVer ?? throw new InvalidOperationException("Game version is unavailable.");
        string variant = version.CompareTo(new SemanticVersion(0, 111, 0)) >= 0 ? "Beta" : "Stable";
        UsesStableApi = variant == "Stable";
        var loader = typeof(Bootstrap).Assembly;
        string directory = Path.GetDirectoryName(loader.Location)!;
        string path = Path.Combine(directory, "lib", $"{ModId}.{variant}.dll");
        var context = AssemblyLoadContext.GetLoadContext(loader) ?? AssemblyLoadContext.Default;
        _implementation = context.LoadFromAssemblyPath(Path.GetFullPath(path));
        typeof(ModManager).GetMethod("AssociateAssemblyWithMod", BindingFlags.Public | BindingFlags.Static,
            null, [typeof(string), typeof(Assembly)], null)?.Invoke(null, [ModId, _implementation]);
        new Harmony(ModId + ".Loader").PatchAll(loader);
        _implementation.GetType("ShoujoKagekiJunna.JunnaMod", true)!
            .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
        Log.Info($"Junna loader selected {variant} API for game {version}.");
    }
}

[HarmonyPatch(typeof(ReflectionHelper), nameof(ReflectionHelper.ModTypes), MethodType.Getter)]
internal static class ImplementationTypesPatch
{
    private static void Postfix(ref Type[] __result)
    {
        var seen = new HashSet<Type>(__result);
        __result = [.. __result, .. Bootstrap.ImplementationTypes.Where(seen.Add)];
    }
}

[HarmonyPatch(typeof(ReflectionHelper), nameof(ReflectionHelper.GetSubtypesFromAssembly))]
internal static class ImplementationSubtypesPatch
{
    private static void Postfix(Assembly assembly, Type parentType, ref IEnumerable<Type> __result)
    {
        if (!Bootstrap.UsesStableApi || assembly != typeof(Bootstrap).Assembly) return;
        __result = __result.Concat(Bootstrap.ImplementationTypes
            .Where(type => !type.IsAbstract && !type.IsInterface && parentType.IsAssignableFrom(type)));
    }
}

// Keep the scan as a runtime call even when another mod patched Init before the loaders ran.
[HarmonyPatch(typeof(ModelIdSerializationCache), nameof(ModelIdSerializationCache.Init))]
internal static class StableModelTypesPatch
{
    private static bool Prepare() => Bootstrap.UsesStableApi;
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var scan = AccessTools.Method(typeof(ReflectionHelper), nameof(ReflectionHelper.GetSubtypesFromAssembly));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(scan))
                instruction.operand = AccessTools.Method(typeof(StableModelTypesPatch), nameof(GetSubtypes));
            yield return instruction;
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<Type> GetSubtypes(Assembly assembly, Type parentType) =>
        ReflectionHelper.GetSubtypesFromAssembly(assembly, parentType);
}
