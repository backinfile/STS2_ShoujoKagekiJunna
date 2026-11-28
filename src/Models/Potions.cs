using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_POTION_POOL")]
public sealed class JunnaPotionPool : CustomPotionPoolModel
{
    public override string BigEnergyIconPath => JunnaMod.ResourceRoot + "images/energy_counter/energy_icon.png";
    public override string TextEnergyIconPath => JunnaMod.ResourceRoot + "images/energy_counter/energy_icon_small.png";
}

[Pool(typeof(JunnaPotionPool))]
public abstract class JunnaPotion(string image) : CustomPotionModel
{
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;
    public override string CustomPackedImagePath => JunnaMod.ResourceRoot + "images/potions/" + image + ".png";
    public override string CustomPackedOutlinePath => JunnaMod.ResourceRoot + "images/potions/" + image + "_outline.png";
    public override string CustomLargeImagePath => CustomPackedImagePath;
}

[CustomID("JUNNA_KNOWLEDGE_POTION")]
public sealed class JunnaKnowledgePotion() : JunnaPotion("knowledge_potion")
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(JunnaKeywords.Knowledge)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var combat = Owner.Creature.CombatState;
        if (combat == null || CombatManager.Instance.IsOverOrEnding) return;
        var roots = CardFactory.GetDistinctForCombat(Owner, KnowledgeOptions(), 3, Owner.RunState.Rng.CombatCardGeneration).ToArray();
        if (roots.Length == 0) return;
        IReadOnlyList<CardModel> selectedCards = [];
        try
        {
            var selected = await CardSelectCmd.FromChooseACardScreen(choiceContext, roots, Owner, canSkip: false);
            if (selected == null || Owner.Creature.CombatState != combat || CombatManager.Instance.IsOverOrEnding) return;
            await DynamicKnowledge.Prepare(choiceContext, selected, roots);
            if (DynamicKnowledge.HasPreparedSelection(selected))
            {
                foreach (var card in roots) card.SetToFreeThisTurn();
                await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner);
            }
            else
            {
                selectedCards = CreateBundle(selected);
                using var single = KnowledgeCombat.SingleCard();
                await CardPileCmd.AddGeneratedCardsToCombat(selectedCards, PileType.Hand, Owner);
            }
        }
        finally
        {
            foreach (var card in roots.Concat(selectedCards).Distinct())
                if (card.Pile == null) card.RemoveFromState();
        }
    }

    private IEnumerable<CardModel> KnowledgeOptions()
    {
        var seen = new HashSet<string>();
        foreach (var model in ModelDb.CardPool<JunnaCardPool>()
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
        {
            if (!model.CanBeGeneratedInCombat) continue;
            // Current run relations can also turn an otherwise ordinary card into knowledge.
            var preview = (CardModel)model.MutableClone();
            preview.Owner = Owner;
            if (CardCatalog.IsKnowledge(preview) && seen.Add(GroupKey(preview))) yield return model;
        }
    }

    private static string GroupKey(CardModel root) => string.Join("|",
        (KnowledgeMembers.Definition(root, forCombatGeneration: true) ?? [(root, 1)])
        .OrderBy(member => member.Item1.Id.ToString())
        .Select(member => $"{member.Item1.Id}:{member.Item2}"));

    private IReadOnlyList<CardModel> CreateBundle(CardModel root)
    {
        var cards = new List<CardModel>();
        foreach (var (model, count) in KnowledgeMembers.Definition(root, forCombatGeneration: true) ?? [(root, 1)])
            for (int i = 0; i < count; i++)
                cards.Add(model.Id == root.Id && i == 0 ? root : Owner.Creature.CombatState!.CreateCard(model, Owner));
        foreach (var card in cards) card.SetToFreeThisTurn();
        return cards;
    }
}

[CustomID("JUNNA_SPORTS_DRINK")]
public sealed class JunnaSportsDrink() : JunnaPotion("sports_drink")
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Turns", 3)];
    public override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(JunnaKeywords.Arrow)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var hand = Owner.PlayerCombatState!.Hand;
        int turns = DynamicVars["Turns"].IntValue;
        var prompt = SelectionScreenPrompt;
        prompt.Add("Turns", turns);
        var selected = await CardPicking.Pick(choiceContext, Owner, hand.Cards, new CardPickRequest
        {
            Prompt = prompt,
            Minimum = 0,
            Maximum = hand.Cards.Count,
            Source = this
        });
        await HandArrow.PlaySelected(choiceContext, hand, selected, turns);
    }
}

[CustomID("JUNNA_ARROW_RAIN_POTION")]
public sealed class JunnaArrowRainPotion() : JunnaPotion("arrow_rain_potion")
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(JunnaKeywords.Arrow)];

    protected override Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
        => CardActions.PlayAllArrows(choiceContext, Owner);
}
