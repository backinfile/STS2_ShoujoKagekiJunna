using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class StageGirlNotesProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_notes";
    public override string Args => "check";
    public override string Description => "Verify the retired event cards are removed when an old deck is loaded.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");

        var before = player.ToSerializable();
        var expected = before.Deck.ToArray();
        var removedIds = Enumerable.Range(359, 9).Select(i => new ModelId("CARD", "JUNNA_JK" + i)).ToArray();
        foreach (var id in removedIds)
            before.Deck.Insert(0, new SerializableCard { Id = id });

        var restored = Player.FromSerializable(before);
        bool deckMatches = restored.Deck.Cards.Select(card => card.Id).SequenceEqual(expected.Select(card => card.Id))
            && restored.Deck.Cards.Select(card => card.ToSerializable().CurrentUpgradeLevel)
                .SequenceEqual(expected.Select(card => card.CurrentUpgradeLevel));
        bool absentFromPool = !CardCatalog.Cards.Any(card => removedIds.Any(id => id.Entry == card.ModelId));
        bool absentFromRegistry = removedIds.All(id => ModelDb.GetByIdOrNull<CardModel>(id) == null);
        bool eventCompatibility = !ModelDb.GetById<JunnaStageGirlNotes>(ModelDb.GetId<JunnaStageGirlNotes>()).IsAllowed(player.RunState);
        return new(deckMatches && absentFromPool && absentFromRegistry && eventCompatibility,
            $"deck={deckMatches} pool={absentFromPool} registry={absentFromRegistry} oldEvent={eventCompatibility}");
    }
}
