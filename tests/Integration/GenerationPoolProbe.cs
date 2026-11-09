using System.Text.Json;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class GenerationPoolProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_generation_pool";
    public override string Args => "check";
    public override string Description => "Check the unlocked card pool used by card-generating potions.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        var cards = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).ToArray();
        var skills = cards.Where(card => card.Type == CardType.Skill).ToArray();
        var choices = new CardModel[] {
            ModelDb.Card<JunnaArrowTargetSelf>(), ModelDb.Card<JunnaArrowTargetEnemy>(),
            ModelDb.Card<JunnaNotesDraw>(), ModelDb.Card<JunnaNotesEnergy>(),
            ModelDb.Card<JunnaPrepareArrows>(), ModelDb.Card<JunnaPrepareKnowledge>(),
            ModelDb.Card<JunnaEnergyNow>(), ModelDb.Card<JunnaEnergyLater>()
        };
        var leaked = skills.Where(card => choices.Any(choice => choice.Id == card.Id)).Select(card => card.Id.Entry).ToArray();
        var nonReward = cards.Where(card => card.Rarity is not (CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)).Select(card => card.Id.Entry).ToArray();
        bool legitimateSkills = skills.Any(card => card.Id.Entry == "JUNNA_JA53") && skills.Any(card => card.Id.Entry == "JUNNA_JX01");
        bool choicesRegistered = choices.All(card => card.Rarity == CardRarity.Token);
        bool pass = leaked.Length == 0 && nonReward.Length == 0 && legitimateSkills && choicesRegistered;
        return new(pass, JsonSerializer.Serialize(new { pass, candidates = cards.Length, skills = skills.Length, leaked, nonReward, legitimateSkills, choicesRegistered }));
    }
}
