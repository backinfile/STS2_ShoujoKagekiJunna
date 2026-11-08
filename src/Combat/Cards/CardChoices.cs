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
        var own = (CardModel)ModelDb.GetById<JunnaArrowTargetSelf>(ModelDb.GetId<JunnaArrowTargetSelf>()).MutableClone();
        var enemies = (CardModel)ModelDb.GetById<JunnaArrowTargetEnemy>(ModelDb.GetId<JunnaArrowTargetEnemy>()).MutableClone();
        own.Owner = enemies.Owner = source.Owner;
        var selected = await CardSelectCmd.FromChooseACardScreen(context, [own, enemies], source.Owner);
        if (selected == null) return;
        bool self = selected == own;
        var arrows = ArrowCombat.DisplayOrder().Where(a => a.Card.Owner == source.Owner &&
            (self ? !a.TargetsGroup && a.Target == source.Owner.Creature : a.TargetsAllEnemies || a.Target?.IsEnemy == true)).ToArray();
        foreach (var arrow in arrows) await ArrowCombat.Accelerate(context, arrow, source.Number(1));
    }

    public static async Task Notes(PlayerChoiceContext context, CatalogCard source)
    {
        var draw = (JunnaNotesDraw)ModelDb.GetById<JunnaNotesDraw>(ModelDb.GetId<JunnaNotesDraw>()).MutableClone();
        var energy = (JunnaNotesEnergy)ModelDb.GetById<JunnaNotesEnergy>(ModelDb.GetId<JunnaNotesEnergy>()).MutableClone();
        draw.Owner = energy.Owner = source.Owner;
        draw.DynamicVars["Amount"].BaseValue = source.Number(0);
        energy.DynamicVars["Amount"].BaseValue = source.Number(1);
        var selected = await CardSelectCmd.FromChooseACardScreen(context, [draw, energy], source.Owner);
        if (selected == draw) await CardPileCmd.Draw(context, source.Number(0), source.Owner);
        else if (selected == energy) await PlayerCmd.GainEnergy(source.Number(1), source.Owner);
    }

    public static async Task EnergyTiming(PlayerChoiceContext context, CatalogCard source)
    {
        var now = (JunnaEnergyNow)ModelDb.GetById<JunnaEnergyNow>(ModelDb.GetId<JunnaEnergyNow>()).MutableClone();
        var later = (JunnaEnergyLater)ModelDb.GetById<JunnaEnergyLater>(ModelDb.GetId<JunnaEnergyLater>()).MutableClone();
        now.Owner = later.Owner = source.Owner;
        now.DynamicVars["Amount"].BaseValue = source.Number(0);
        later.DynamicVars["Amount"].BaseValue = source.Number(1);
        var selected = await CardSelectCmd.FromChooseACardScreen(context, [now, later], source.Owner);
        if (selected == now) await PlayerCmd.GainEnergy(source.Number(0), source.Owner);
        else if (selected == later) await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.EnergyNextTurnPower>(
            context, source.Owner.Creature, source.Number(1), source.Owner.Creature, source);
    }

    public static async Task<CardModel[]> ChooseTrainingPile(PlayerChoiceContext context, CatalogCard source)
    {
        var state = source.Owner.PlayerCombatState!;
        if (!state.DrawPile.Cards.Concat(state.DiscardPile.Cards).Any(DamageModifiers.CanDealDamage)) return [];
        CardModel Option(string id)
        {
            var option = (CardModel)CardCatalog.Model(id).MutableClone();
            option.Owner = source.Owner;
            if (source.IsUpgraded) { option.UpgradeInternal(); option.FinalizeUpgradeInternal(); }
            return option;
        }
        var choices = new[] { Option("JK78D"), Option("JK78E") };
        var selected = await CardSelectCmd.FromChooseACardScreen(context, choices, source.Owner);
        var pile = CardCatalog.DesignId(selected!) switch
        {
            "JK78D" => state.DrawPile,
            "JK78E" => state.DiscardPile,
            _ => null
        };
        return pile == null ? [] : await CardActions.Choose(context, source,
            pile.Cards.Where(DamageModifiers.CanDealDamage), source.Number(0), optional: true);
    }
}
