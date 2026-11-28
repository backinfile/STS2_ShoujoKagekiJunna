using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaCardU_CARD_187
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [.. base.ExtraHoverTips, .. HoverTipFactory.FromEnchantment<Adroit>(Value("Adroit"))];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardActions.Fly(context, Owner, Value("Damage"));
        int amount = Value("Adroit");
        var enchantment = ModelDb.Enchantment<Adroit>();
        foreach (var meteor in ArrowCombat.WaitingFor(Owner).Where(arrow => arrow.Card is JunnaCardJA81B))
        {
            var card = meteor.Card;
            if (card.Enchantment is Adroit existing)
            {
                existing.Amount += amount;
                card.FinalizeUpgradeInternal();
            }
            else if (enchantment.CanEnchant(card))
                CardCmd.Enchant<Adroit>(card, amount);

            if (card.Enchantment is Adroit applied)
                for (int index = 0; index < meteor.PlayCount; index++)
                    meteor.DeferEnchantment(index, applied);
        }
    }

    protected override void OnUpgrade() => ApplyCatalogUpgrade();
}
