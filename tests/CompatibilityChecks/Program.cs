using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

bool nativeOnly = args.Length == 5 && args[0] == "--native-only";
if (nativeOnly) args = args[1..];
if (args.Length != 4)
    throw new ArgumentException("Usage: CompatibilityChecks <Junna DLL> <RitsuLib directory> <Core API DLL> <Core runtime DLL>");

string[] dependencies = Directory.GetFiles(Path.GetFullPath(args[1]), "*.dll", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}compat{Path.DirectorySeparatorChar}") ||
                   path.Contains($"{Path.DirectorySeparatorChar}{GameVersion()}{Path.DirectorySeparatorChar}"))
    .Append(Path.GetFullPath(args[2])).ToArray();
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string? path = dependencies.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == name.Name);
    return path == null ? null : context.LoadFromAssemblyPath(path);
};

var junna = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[3]));
if (nativeOnly)
{
    var attackMethod = MoveNext(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand), "Execute"));
    var original = PatchProcessor.GetOriginalInstructions(attackMethod);
    var rewritten = Rewrite(junna, "ShoujoKagekiJunna.Combat.AttackBatchPatch", original, attackMethod);
    Expect("native AttackCommand retains exactly one Junna damage boundary", Calls(rewritten, "ShoujoKagekiJunna.Combat.DamageExecution", "FromAttack") == 1);
    var patchType = junna.GetType("ShoujoKagekiJunna.Combat.DamagePatches", true)!;
    var damage = (MethodInfo)AccessTools.Property(patchType, "CanonicalDamage").GetValue(null)!;
    Expect("canonical damage method exists in the installed game", damage != null);
    var harmony = new Harmony("Junna.NativeDamageCompatibilityChecks");
    try
    {
        harmony.Patch(damage!, prefix: new HarmonyMethod(AccessTools.Method(patchType, "Prefix")));
        harmony.Patch(attackMethod, transpiler: new HarmonyMethod(AccessTools.Method(junna.GetType("ShoujoKagekiJunna.Combat.AttackBatchPatch", true)!, "Transpiler")));
        Expect("damage prefix and attack transpiler install against the real game", true);
    }
    finally { harmony.UnpatchAll(harmony.Id); }
    return;
}
var ritsu = Assembly.Load("STS2-RitsuLib.Runtime");
var cardWrapper = MoveNext(AccessTools.Method(typeof(CardModel), nameof(CardModel.OnPlayWrapper)));
var vanilla = PatchProcessor.GetOriginalInstructions(cardWrapper);
var direct = Rewrite(core, "ShoujoKageki.Core.Runtime.Modules.Execution.CardPlayRoutingModule+NativeBoundary", vanilla);
Expect("standalone card effect", Calls(direct, "ShoujoKageki.Core.Runtime.Modules.Execution.CardPlayRoutingModule", "Dispatch") == 1);
Expect("standalone arrow destination", Calls(direct, "ShoujoKageki.Core.Runtime.Modules.Execution.CardPlayRoutingModule", "Result") == 1);

var wrapped = Rewrite(ritsu, "STS2RitsuLib.Cards.Patches.CardOnPlayHookPatch", PatchProcessor.GetOriginalInstructions(cardWrapper));
Expect("real RitsuLib replaces OnPlay", Calls(wrapped, "STS2RitsuLib.Cards.CardOnPlayHook", "RunCardOnPlayHooks") == 1);
var combined = Rewrite(core, "ShoujoKageki.Core.Runtime.Modules.Execution.CardPlayRoutingModule+NativeBoundary", wrapped);
Expect("RitsuLib dispatcher retained", Calls(combined, "STS2RitsuLib.Cards.CardOnPlayHook", "RunCardOnPlayHooks") == 1);
Expect("combined arrow destination", Calls(combined, "ShoujoKageki.Core.Runtime.Modules.Execution.CardPlayRoutingModule", "Result") == 1);

var dispatcher = AccessTools.Method(ritsu.GetType("STS2RitsuLib.Cards.CardOnPlayHook", true)!, "RunCardOnPlayHooks");
var hookInstructions = PatchProcessor.GetOriginalInstructions(MoveNext(dispatcher));
var hookCombined = Rewrite(core, "ShoujoKageki.Core.Runtime.Modules.Execution.CardPlayRoutingModule+RitsuBoundary", hookInstructions);
Expect("arrow dispatch at RitsuLib effect boundary", Calls(hookCombined, "ShoujoKageki.Core.Runtime.Modules.Execution.CardPlayRoutingModule", "DispatchFromHook") == 1);
foreach (string hook in new[] { "BeforeCardOnPlay", "AfterCardOnPlay" })
    Expect($"{hook} retained", Calls(hookCombined, "STS2RitsuLib.Cards.CardOnPlayHook", hook) ==
        Calls(hookInstructions, "STS2RitsuLib.Cards.CardOnPlayHook", hook) &&
        Calls(hookCombined, "STS2RitsuLib.Cards.CardOnPlayHook", hook) > 0);
var attack = MoveNext(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand), "Execute"));
var originalAttack = PatchProcessor.GetOriginalInstructions(attack);
var hookAttack = Rewrite(ritsu, "STS2RitsuLib.Combat.AttackHits.Patches.AttackCommandExecuteHitHookPatch", originalAttack, attack);
Expect("real RitsuLib per-hit damage hook", Calls(hookAttack, "STS2RitsuLib.Combat.AttackHits.AttackHitHook", "DamageWithAttackHitHooks") == 1);
var combinedAttack = Rewrite(junna, "ShoujoKagekiJunna.Combat.AttackBatchPatch", hookAttack, attack);
var originalHitHook = (MethodInfo)hookAttack.Single(instruction => instruction.operand is MethodInfo method &&
    method.DeclaringType?.FullName == "STS2RitsuLib.Combat.AttackHits.AttackHitHook" && method.Name == "DamageWithAttackHitHooks").operand;
var hitBridge = (MethodInfo)combinedAttack.Single(instruction => instruction.operand is MethodInfo method &&
    method.DeclaringType?.FullName == "ShoujoKagekiJunna.Combat.DamageExecution" &&
    method.Name is "FromAttackHitHooks" or "FromAttackTargetHitHooks").operand;
Expect("Junna preserves the actual per-hit hook signature", hitBridge.ReturnType == originalHitHook.ReturnType &&
    hitBridge.GetParameters().Select(parameter => parameter.ParameterType)
        .SequenceEqual(originalHitHook.GetParameters().Select(parameter => parameter.ParameterType)));
#if STS2_BETA
var legacyHitHook = originalHitHook.DeclaringType!.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
    method.Name == "DamageWithAttackHitHooks" && method.GetParameters()[1].ParameterType ==
    typeof(IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature>));
var legacyAttack = Rewrite(junna, "ShoujoKagekiJunna.Combat.AttackBatchPatch",
    [new CodeInstruction(System.Reflection.Emit.OpCodes.Call, legacyHitHook)], attack);
Expect("Junna retains the legacy creature-based RitsuLib hit bridge",
    Calls(legacyAttack, "ShoujoKagekiJunna.Combat.DamageExecution", "FromAttackHitHooks") == 1);
#endif
var attackPatcher = new Harmony("Junna.AttackHitCompatibilityChecks");
try
{
    attackPatcher.Patch(attack, transpiler: new HarmonyMethod(AccessTools.Method(
        ritsu.GetType("STS2RitsuLib.Combat.AttackHits.Patches.AttackCommandExecuteHitHookPatch", true)!, "Transpiler")));
    attackPatcher.Patch(attack, transpiler: new HarmonyMethod(AccessTools.Method(
        junna.GetType("ShoujoKagekiJunna.Combat.AttackBatchPatch", true)!, "Transpiler")));
    Expect("RitsuLib and Junna attack transpilers install together against the real game", true);
}
finally { attackPatcher.UnpatchAll(attackPatcher.Id); }
Console.WriteLine("PASS: standalone and RitsuLib card and attack boundaries compose correctly.");

static MethodInfo MoveNext(MethodInfo method) => AccessTools.Method(method.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");
static List<CodeInstruction> Rewrite(Assembly assembly, string type, IEnumerable<CodeInstruction> instructions, MethodBase? original = null)
{
    var transpiler = AccessTools.Method(assembly.GetType(type, true)!, "Transpiler");
    return ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, transpiler.GetParameters().Length == 1 ? [instructions] : [instructions, original])!).ToList();
}
static int Calls(IEnumerable<CodeInstruction> instructions, string type, string method) => instructions.Count(instruction =>
    instruction.operand is MethodInfo info && info.DeclaringType?.FullName == type && info.Name == method);
static void Expect(string name, bool pass)
{
    if (!pass) throw new InvalidOperationException($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
}

static string GameVersion() =>
#if STS2_BETA
    "0.113.0";
#else
    "0.107.1";
#endif
