using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK126
{
    internal override CardKeyword KnowledgeKeyword => JunnaKeywords.ChainKnowledge;
    public override HashSet<CardKeyword> CanonicalKeywords => [.. base.CanonicalKeywords, KnowledgeKeyword];

    protected override IHoverTip? UnlinkedKnowledgeGroupTip
        => DynamicKnowledge.IsLinked(this) ? null : new HoverTip(new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_GROUP"),
            $"[gold]{((JunnaCardJK126)CanonicalInstance).Title}[/gold]")
        {
#if !STS2_MAIN && !STS2_BETA
            Id = "junna_knowledge_reward_others",
#endif
        };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        await KnowledgeCombat.DrawPartners(context, this);
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
