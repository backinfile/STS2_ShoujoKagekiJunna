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
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (DesignId == "JG31" && play.Card.Owner == Owner.Player && play.IsFirstInSeries)
            foreach (var grant in _grants.ToArray())
                if (!ReferenceEquals(play, grant.InstalledBy)) DamageModifiers.Add(play.Card, grant.V("Bonus"));
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.Owner != Owner.Player) return;
        // Native Replay repeats the effect; paid replays enter a new series and count as another play.
        if (!play.IsFirstInSeries) return;
        bool immediateConsumed = false;
        foreach (var grant in _grants.ToArray())
        {
            if (ReferenceEquals(play, grant.InstalledBy)) continue;
            switch (DesignId)
            {
                case "JA04" when CardCatalog.IsArrow(play.Card) && !immediateConsumed:
                    immediateConsumed = true;
                    grant.Used++;
                    if (grant.Used >= grant.V("Limit")) _grants.Remove(grant);
                    if (ArrowCombat.Find(play.Card) is { } immediate) await ArrowCombat.Accelerate(context, immediate, int.MaxValue);
                    break;
                case "JK10" when play.Resources.EnergyValue >= grant.V("EnergyThreshold") && grant.Take(1):
                    await CardPileCmd.Draw(context, grant.V("DrawCount"), play.Card.Owner);
                    await PlayerCmd.GainEnergy(grant.V("Energy"), play.Card.Owner); break;
                case "JK15" when CardCatalog.IsKnowledge(play.Card) && grant.Take(1):
                    using (KnowledgeCombat.SingleCard())
                    {
                        var clone = play.Card.CreateCloneForPlayer(play.Card.Owner);
                        await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Draw, play.Card.Owner);
                    }
                    break;
                case "JK37C" when play.Resources.EnergyValue == 0:
                    var eligibleArrows = ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player && !a.Resolving && a.Remaining > 0).ToArray();
                    if (eligibleArrows.Length > 0)
                        await ArrowCombat.Accelerate(context, Owner.Player.RunState.Rng.CombatCardSelection.NextItem(eligibleArrows)!, grant.V("Turns"));
                    break;
                case "JA45" when CardCatalog.IsArrow(play.Card) && grant.Take(grant.V("Limit")):
                    if (ArrowCombat.Find(play.Card) is { } arrow) await ArrowCombat.Accelerate(context, arrow, grant.V("Turns"));
                    break;
                case "JK64" when CardCatalog.IsKnowledge(play.Card):
                    await AccelerateChoice(grant, context, 1, grant.V("Turns")); break;
                case "JK75" when CardCatalog.IsKnowledge(play.Card):
                    var eligible = play.Card.Owner.PlayerCombatState!.Hand.Cards.Where(DamageModifiers.CanDealDamage).ToArray();
                    if (eligible.Length > 0) DamageModifiers.Add(play.Card.Owner.RunState.Rng.CombatCardSelection.NextItem(eligible)!, grant.V("Bonus"));
                    break;
                case "JH23" when play.Resources.EnergyValue >= grant.V("EnergyThreshold2") && grant.Take(grant.V("EnergyThreshold")):
                    await Block(grant, grant.V("Block")); break;
                case "JA118" when CardCatalog.IsArrow(play.Card):
                case "JA70" when CardCatalog.IsArrow(play.Card): await Block(grant, grant.V("Block")); break;
                case "JK80" when CardCatalog.IsKnowledge(play.Card):
                    _grants.Remove(grant);
                    await Block(grant, grant.V("Amount")); break;
                case "JH31" when play.Resources.EnergyValue >= grant.V("EnergyThreshold"):
                    await CardActions.DrawMatching(context, play.Card.Owner, c => CardActions.HasNumericCost(c) && CardActions.Cost(c) == 0, grant.V("Amount")); break;
            }
        }
        if (_grants.Count == 0) await PowerCmd.Remove(this);
        else Synchronize();
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        if (DesignId == "JK27" && card.Owner == Owner.Player && CardCatalog.IsKnowledge(card))
            await KnowledgeCombat.DrawPartners(context, card);
    }


}
