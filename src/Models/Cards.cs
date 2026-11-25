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
    : CustomCardModel(cost, type, rarity, target, showInCardLibrary: CardCatalog.Cards.Any(c => c.Id == designId && !c.ChoiceOnly))
{
    public virtual string DesignId => designId;
    internal virtual ArrowAcceleration Acceleration => default;
    internal virtual float? AncientPortraitFocusX => null;
    internal virtual CardKeyword KnowledgeKeyword => JunnaKeywords.Knowledge;
    protected virtual IHoverTip? UnlinkedKnowledgeGroupTip => null;
    internal virtual bool CanBeGeneratedAsKnowledgeMember => true;
    public override bool CanBeGeneratedInCombat => CardCatalog.IsRewardCard(this) && CardCatalog.CanGenerateAsKnowledgeMember(this);
    public override string CustomPortraitPath => JunnaMod.ResourceRoot + "images/cards/" + CardCatalog.Get(DesignId).Portrait;
    public override HashSet<CardKeyword> CanonicalKeywords =>
        [..(CardCatalog.IsKnowledge(this) ? new[] { JunnaKeywords.Knowledge } : []),
         ..(CardCatalog.IsArrow(this) ? new[] { JunnaKeywords.Arrow } : [])];
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var rules = CardRules.Get(DesignId);
            // Extra tips precede native keyword tips; adding this first also fixes the group ordering.
            if (CardCatalog.IsKnowledge(this) || rules.HasTip(CardTip.Knowledge, IsUpgraded))
                yield return HoverTipFactory.FromKeyword(KnowledgeKeyword);
            if (UnlinkedKnowledgeGroupTip is { } tip)
                yield return tip;
            var group = KnowledgeCombat.Group(this);
            if (group != null && !DynamicKnowledge.IsLinked(this))
            {
                var members = group.Members.SelectMany(member =>
                    Enumerable.Repeat($"[gold]{CardCatalog.Model(member.Id).Title}[/gold]", member.Count)).ToArray();
                if (members.Length > 0)
                    yield return new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_GROUP"), string.Join("\n", members))
                        {
#if !STS2_MAIN && !STS2_BETA
                            Id = "junna_knowledge_group_" + group.MainCardId,
#endif
                        };
            }
            var background = LocString.GetIfExists("gameplay_ui", "JUNNA.KNOWLEDGE_BACKGROUND." + DesignId);
            if (background != null)
                yield return new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_BACKGROUND"), background)
                    {
#if !STS2_MAIN && !STS2_BETA
                        Id = "junna_knowledge_background_" + DesignId,
#endif
                    };
            foreach (var related in rules.RelatedCards)
                yield return new CardHoverTip(CardCatalog.Model(related));
            if (rules.HasTip(CardTip.Meteor, IsUpgraded))
            {
                var meteor = (CatalogCard)ShoujoKageki.Core.V1.CardCopies.Snapshot(CardCatalog.Model("JA81B"));
                meteor.SetGeneratedDamage((int)DynamicVars.Damage.PreviewValue);
                yield return new CardHoverTip(meteor);
            }
            if (CardCatalog.IsArrow(this) || rules.HasTip(CardTip.Arrow, IsUpgraded))
                yield return HoverTipFactory.FromKeyword(JunnaKeywords.Arrow);
            if (rules.HasTip(CardTip.Vulnerable, IsUpgraded)) yield return HoverTipFactory.FromPower<VulnerablePower>();
            if (rules.HasTip(CardTip.Weak, IsUpgraded)) yield return HoverTipFactory.FromPower<WeakPower>();
            if (rules.HasTip(CardTip.Retain, IsUpgraded) && !Keywords.Contains(CardKeyword.Retain))
                yield return HoverTipFactory.FromKeyword(CardKeyword.Retain);
            if (rules.HasTip(CardTip.Energy, IsUpgraded))
                yield return EnergyHoverTip;
        }
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
