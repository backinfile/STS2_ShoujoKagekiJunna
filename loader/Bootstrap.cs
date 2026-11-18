using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using ShoujoKageki.Core.Loader;

namespace ShoujoKagekiJunna.Loader;

[ModInitializer(nameof(Initialize))]
public static class Bootstrap
{
    private const string ModId = "ShoujoKagekiJunna";
    private static readonly Logger Log = new(ModId, LogType.Generic);
    public static void Initialize() => CharacterLoader.Initialize(
        ModId, "ShoujoKagekiJunna.JunnaMod",
        SelectVariant, Log);

    private static string SelectVariant(SemanticVersion version)
    {
        if (version.CompareTo(new SemanticVersion(0, 113, 0)) >= 0) return "Beta";
        if (version.CompareTo(new SemanticVersion(0, 111, 0)) >= 0) return "Main";
        return "Stable";
    }
}
