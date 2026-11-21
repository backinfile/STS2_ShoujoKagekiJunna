using MegaCrit.Sts2.Core.Debug;
using System.Text.Json;
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
        using var stream = typeof(Bootstrap).Assembly.GetManifestResourceStream("Junna.GameVariants")!;
        using var manifest = JsonDocument.Parse(stream);
        foreach (var variant in manifest.RootElement.EnumerateArray().Reverse())
        {
            var minimum = Version.Parse(variant.GetProperty("minimum").GetString()!);
            if (version.CompareTo(new SemanticVersion(minimum.Major, minimum.Minor, minimum.Build)) >= 0)
                return variant.GetProperty("name").GetString()!;
        }
        return "Stable";
    }
}
