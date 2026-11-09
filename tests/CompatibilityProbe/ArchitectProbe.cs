using System.Text.Json;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace JunnaCompatibilityProbe;

public sealed class ArchitectCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_architect";
    public override string Args => "info | enter";
    public override string Description => "Inspect and enter the native final Architect event.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        var model = ModelDb.Event<TheArchitect>();
        if (args.Contains("enter")) return new(RunManager.Instance.EnterRoom(new EventRoom(model)), true, "Entering the Architect");
        return new(true, JsonSerializer.Serialize(new { character = player.Character.Id.Entry,
            visits = new[] { 0, 1, 2, 10 }.Select(visit => new { visit,
                dialogues = model.DialogueSet.GetValidDialogues(player.Character.Id, visit, 20, false).Count() }),
            attackVfx = player.Character.GetArchitectAttackVfx().Count }));
    }
}
