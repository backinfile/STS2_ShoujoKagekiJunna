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
