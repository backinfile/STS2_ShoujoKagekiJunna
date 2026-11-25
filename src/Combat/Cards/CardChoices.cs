using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static class CardChoices
{
    public static LocString Prompt(CardModel source, int count = 1)
    {
        string modelId = source is JunnaCard junna ? CardCatalog.Get(junna.DesignId).ModelId : source.Id.Entry;
        var prompt = new LocString("cards", modelId + ".selectionScreenPrompt");
        source.DynamicVars.AddTo(prompt);
        prompt.Add("Cards", count);
        return prompt;
    }

    public static async Task AccelerateTargets(PlayerChoiceContext context, CatalogCard source)
    {
        var own = (JunnaArrowTargetSelf)ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaArrowTargetSelf>(ModelDb.GetId<JunnaArrowTargetSelf>()), source.Owner);
        var enemies = (JunnaArrowTargetEnemy)ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaArrowTargetEnemy>(ModelDb.GetId<JunnaArrowTargetEnemy>()), source.Owner);
        int turns = source.Value("Turns");
        own.Acceleration = new(ArrowAccelerationTargets.Self, turns);
        enemies.Acceleration = new(ArrowAccelerationTargets.Enemies, turns);
        var selected = await ShoujoKageki.Core.V1.CardOptions.Choose(context, [own, enemies], source.Owner);
        if (selected is JunnaArrowTargetChoice choice)
            await ArrowCombat.ApplyAcceleration(context, source.Owner, choice.Acceleration, snapshotTargets: true);
    }

    public static async Task Notes(PlayerChoiceContext context, CatalogCard source)
    {
        var draw = (JunnaNotesDraw)ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaNotesDraw>(ModelDb.GetId<JunnaNotesDraw>()), source.Owner);
        var energy = (JunnaNotesEnergy)ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaNotesEnergy>(ModelDb.GetId<JunnaNotesEnergy>()), source.Owner);
        draw.DynamicVars["Amount"].BaseValue = source.Value("DrawCount");
        energy.DynamicVars["Amount"].BaseValue = source.Value("Energy");
        var selected = await ShoujoKageki.Core.V1.CardOptions.Choose(context, [draw, energy], source.Owner);
        if (selected == draw) await CardPileCmd.Draw(context, source.Value("DrawCount"), source.Owner);
        else if (selected == energy) await PlayerCmd.GainEnergy(source.Value("Energy"), source.Owner);
    }
}
