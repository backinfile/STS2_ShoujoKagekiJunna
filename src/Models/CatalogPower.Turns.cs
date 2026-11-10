using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner.Player) return;
        foreach (var grant in _grants.ToArray())
        {
            switch (DesignId)
            {
                case "JA35": await ArrowCombat.AccelerateAll(context, player, grant.V("Turns")); break;
                case "JA144": await CardActions.Generate(player, "JA119", grant.V("Cards"), PileType.Hand); break;
                case "JH59": case "JK139": await PlayerCmd.GainEnergy(grant.V("Energy"), player); break;
                case "JK32": await CardPileCmd.Draw(context, grant.V("DrawCount"), player); break;
                case "JK46": await CardActions.Generate(player, "JK46B", grant.V("Cards"), PileType.Hand, grant.Card.IsUpgraded); break;
                case "JG34":
                    using (DamageExecution.BeginCardResolution(grant.Card))
                        await CardEffects.DealDamage(grant.Card, context, grant.Play, grant.V("Damage"), grant.BaseHits);
                    break;
                case "JZ06": await PlayerCmd.GainEnergy(player.PlayerCombatState!.Hand.Cards.Where(CardActions.HasNumericCost).Select(CardActions.Cost).DefaultIfEmpty().Max(), player); break;
            }
        }
    }

    public override async Task BeforeFlush(PlayerChoiceContext context, Player player)
    {
        if (DesignId == "JK81" && player == Owner.Player)
            foreach (var card in player.PlayerCombatState!.Hand.Cards)
                if (_grants.Any(g => CardActions.Cost(card) >= g.V("Threshold"))) card.GiveSingleTurnRetain();
        if (DesignId == "JA87" && player == Owner.Player)
            foreach (var grant in _grants.ToArray())
                await HandArrow.Select(context, grant.Card, grant.V("Cards"), optional: true);
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;
        foreach (var grant in _grants.ToArray())
        {
            switch (DesignId)
            {
                case "A": await CardActions.Generate(Owner.Player!, "B", grant.V("Cards"), PileType.Draw); break;
                case "JK67":
                    DamageModifiers.AddMany(ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player).Select(a => a.Card), grant.V("Bonus"));
                    break;
                case "JH21" when Owner.Player!.PlayerCombatState!.Energy > 0: await Block(grant, grant.V("Block")); break;
            }
        }
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner) && DesignId is "JK64" or "JG31" or "JH19")
            await PowerCmd.Remove(this);
    }


}
