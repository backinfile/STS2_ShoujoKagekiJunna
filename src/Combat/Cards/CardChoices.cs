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
        var own = ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaArrowTargetSelf>(ModelDb.GetId<JunnaArrowTargetSelf>()), source.Owner);
        var enemies = ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaArrowTargetEnemy>(ModelDb.GetId<JunnaArrowTargetEnemy>()), source.Owner);
        var selected = await ShoujoKageki.Core.V1.CardOptions.Choose(context, [own, enemies], source.Owner);
        if (selected == null) return;
        bool self = selected == own;
        var arrows = ArrowCombat.DisplayOrder().Where(a => a.Card.Owner == source.Owner &&
            (self ? !a.TargetsGroup && a.Target == source.Owner.Creature : a.TargetsAllEnemies || a.Target?.IsEnemy == true)).ToArray();
        foreach (var arrow in arrows) await ArrowCombat.Accelerate(context, arrow, source.Value("Turns"));
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

    public static async Task EnergyTiming(PlayerChoiceContext context, CatalogCard source)
    {
        var now = (JunnaEnergyNow)ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaEnergyNow>(ModelDb.GetId<JunnaEnergyNow>()), source.Owner);
        var later = (JunnaEnergyLater)ShoujoKageki.Core.V1.CardOptions.Create(ModelDb.GetById<JunnaEnergyLater>(ModelDb.GetId<JunnaEnergyLater>()), source.Owner);
        now.DynamicVars["Amount"].BaseValue = source.Value("EnergyNow");
        later.DynamicVars["Amount"].BaseValue = source.Value("EnergyLater");
        var selected = await ShoujoKageki.Core.V1.CardOptions.Choose(context, [now, later], source.Owner);
        if (selected == now) await PlayerCmd.GainEnergy(source.Value("EnergyNow"), source.Owner);
        else if (selected == later) await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.EnergyNextTurnPower>(
            context, source.Owner.Creature, source.Value("EnergyLater"), source.Owner.Creature, source);
    }

}
