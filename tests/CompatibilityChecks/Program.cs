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
                   path.Contains($"{Path.DirectorySeparatorChar}{RitsuApiVersion()}{Path.DirectorySeparatorChar}"))
    .Append(Path.GetFullPath(args[2])).ToArray();
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string? path = dependencies.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == name.Name);
    return path == null ? null : context.LoadFromAssemblyPath(path);
};

var junna = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[3]));
Expect("retired Junna attack patches removed",
    junna.GetType("ShoujoKagekiJunna.Combat.AttackBatchPatch") == null &&
    junna.GetType("ShoujoKagekiJunna.Combat.DamagePatches") == null);
if (nativeOnly) return;
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
Console.WriteLine("PASS: standalone and RitsuLib card boundaries compose correctly.");

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

static string RitsuApiVersion() =>
#if STS2_BETA
    "0.113.0";
#elif STS2_MAIN
    "0.111.0";
#else
    "0.107.1";
#endif
