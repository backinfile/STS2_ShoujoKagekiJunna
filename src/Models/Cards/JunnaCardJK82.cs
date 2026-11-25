using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.UI;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardJK82 : ICardUpgradeReward
{
    private const string RewardCardId = "JK82B";

    async Task ICardUpgradeReward.GrantUpgradeReward(int previous, int current)
    {
        using var single = KnowledgeCombat.SingleCard();
        var added = new List<CardPileAddResult>();
        for (int i = previous; i < current; i++)
        {
            if (Pile?.Type == PileType.Deck)
                added.Add(await CardPileCmd.Add(Owner.RunState.CreateCard(CardCatalog.Model(RewardCardId), Owner), PileType.Deck));
            else if (Owner.PlayerCombatState != null && !HasBeenRemovedFromState)
                await CardActions.Generate(Owner, RewardCardId, 1, PileType.Draw, CardPilePosition.Random);
        }
        if (!KnowledgeUpgradeAnimation.DeferPreview(this, added))
            await CardActions.PreviewAdded(added, PileType.Deck);
    }

    public override int MaxUpgradeLevel => int.MaxValue;
    protected override void OnUpgrade() => DynamicVars[CardValueNames.Resolve(DesignId, "Energy")].UpgradeValueBy(1);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await PlayerCmd.GainEnergy(Value("Energy"), Owner);
    }
}
