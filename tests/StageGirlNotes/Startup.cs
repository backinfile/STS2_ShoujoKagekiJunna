using MegaCrit.Sts2.Core.Modding;

namespace JunnaIntegration;

[ModInitializer(nameof(Initialize))]
public static class StageGirlNotesStartup
{
    public static void Initialize()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        Godot.RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), Godot.RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => Godot.RenderingServer.ForceDraw(false);
    }
}
