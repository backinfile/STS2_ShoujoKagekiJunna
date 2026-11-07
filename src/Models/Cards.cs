using System.Text.RegularExpressions;
using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

[Pool(typeof(JunnaCardPool))]
public abstract class JunnaCard(int cost, CardType type, CardRarity rarity, TargetType target, string designId)
    : CustomCardModel(cost, type, rarity, target, showInCardLibrary: CardCatalog.Cards.Any(c => c.Id == designId))
{
    public virtual string DesignId => designId;
    public override string CustomPortraitPath => JunnaMod.ResourceRoot + "images/cards/" + CardCatalog.Get(DesignId).Portrait;
    public override HashSet<CardKeyword> CanonicalKeywords =>
        [..(CardCatalog.IsKnowledge(this) ? new[] { JunnaKeywords.Knowledge } : []),
         ..(CardCatalog.IsArrow(this) ? new[] { JunnaKeywords.Arrow } : [])];
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var definition = CardCatalog.Get(DesignId);
            string text = IsUpgraded ? definition.UpgradedText : definition.Text;
            // Extra tips precede native keyword tips; adding this first also fixes the group ordering.
            if (CardCatalog.IsKnowledge(this) || text.Contains("知识"))
                yield return HoverTipFactory.FromKeyword(JunnaKeywords.Knowledge);
            var group = KnowledgeCombat.Group(this);
            if (group != null)
            {
                var members = group.Members.Select(member =>
                {
                    var model = (CardModel)CardCatalog.Model(member.Id).MutableClone();
                    if (IsUpgraded && model.IsUpgradable) model.UpgradeInternal();
                    return $"[gold]{model.Title}[/gold]" + (member.Count > 1 ? $" ×{member.Count}" : "");
                });
                yield return new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_GROUP"), string.Join("\n", members))
                    { Id = "junna_knowledge_group_" + group.MainCardId };
                foreach (var member in group.Members.Where(member => member.Id != DesignId))
                    yield return HoverTipFactory.FromCard(CardCatalog.Model(member.Id));
            }
            var background = LocString.GetIfExists("gameplay_ui", "JUNNA.KNOWLEDGE_BACKGROUND." + DesignId);
            if (background != null)
                yield return new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_BACKGROUND"), background)
                    { Id = "junna_knowledge_background_" + DesignId };
            if (CardCatalog.IsArrow(this) || text.Contains("箭矢"))
                yield return HoverTipFactory.FromKeyword(JunnaKeywords.Arrow);
            if (text.Contains("伤害加成") || DamageModifiers.CaptureDamageModifierSnapshot(this).DamageBonus > 0)
                yield return HoverTipFactory.FromKeyword(JunnaKeywords.DamageBonus);
            if (text.Contains("易伤")) yield return HoverTipFactory.FromPower<VulnerablePower>();
            if (text.Contains("虚弱")) yield return HoverTipFactory.FromPower<WeakPower>();
            if (text.Contains("保留") && text.Contains("手牌") && !Keywords.Contains(CardKeyword.Retain))
                yield return HoverTipFactory.FromKeyword(CardKeyword.Retain);
            if (text.Contains("能量") || text.Contains("耗能") || Regex.IsMatch(text, @"\d+费"))
                yield return EnergyHoverTip;
        }
    }

    protected async Task ImproveHand(PlayerChoiceContext context, decimal bonus, int hits = 0)
    {
        if (!Owner.PlayerCombatState!.Hand.Cards.Any(c => c != this && DamageModifiers.CanDealDamage(c))) return;
        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var selected = await CardSelectCmd.FromHand(context, Owner, prefs, c => c != this && DamageModifiers.CanDealDamage(c), this);
        foreach (var card in selected) DamageModifiers.Add(card, bonus, hits);
    }
}

[CustomID("JUNNA_STRIKE")]
public sealed class JunnaStrike() : JunnaCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, "JB01")
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target!)
            .WithHitFx(VfxCmd.slashPath).Execute(context);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[CustomID("JUNNA_DEFEND")]
public sealed class JunnaDefend() : JunnaCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self, "JB02")
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[CustomID("JUNNA_WARM_UP")]
public sealed class JunnaWarmUp() : JunnaCard(1, CardType.Skill, CardRarity.Common, TargetType.Self, "JG09")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7, ValueProp.Move), new DynamicVar("Bonus", 2)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await PowerCmd.Apply<JunnaNextCardBonus>(context, Owner.Creature, DynamicVars["Bonus"].BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade() { DynamicVars.Block.UpgradeValueBy(2); DynamicVars["Bonus"].UpgradeValueBy(1); }
}

[CustomID("JUNNA_REPEATED_PRACTICE")]
public sealed class JunnaRepeatedPractice() : JunnaCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self, "JG03")
{
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) => await ImproveHand(context, 0, 1);
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

[CustomID("JUNNA_GUIDING_STAR")]
public sealed class JunnaGuidingStar() : JunnaCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, "JA16")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target!)
            .WithHitFx(VfxCmd.slashPath).Execute(context);
        await PowerCmd.Apply<EnergyNextTurnPower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[CustomID("JUNNA_BREAK_ENCIRCLEMENT")]
public sealed class JunnaBreakEncirclement() : JunnaCard(2, CardType.Attack, CardRarity.Common, TargetType.AllEnemies, "JA17")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).TargetingAllOpponents(CombatState!)
            .WithHitFx(VfxCmd.slashPath).Execute(context);
        await PowerCmd.Apply<EnergyNextTurnPower>(context, Owner.Creature, 2, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

[CustomID("JUNNA_TURNING_SLASH")]
public sealed class JunnaTurningSlash() : JunnaCard(3, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, "JH10")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9, ValueProp.Move), new RepeatVar(3)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target!)
            .WithHitCount(DynamicVars.Repeat.IntValue).WithHitFx(VfxCmd.slashPath).Execute(context);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}
