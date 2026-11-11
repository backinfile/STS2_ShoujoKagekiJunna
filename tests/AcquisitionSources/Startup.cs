using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace JunnaIntegration;

[ModInitializer(nameof(Initialize))]
public static class AcquisitionChecksStartup
{
    public static void Initialize()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => RenderingServer.ForceDraw(false);
    }
}
