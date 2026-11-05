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

public abstract class CatalogPower(string designId) : CustomPowerModel
{
    private sealed class Grant(CatalogCard card, CardPlay installedBy, int xValue)
    {
        public readonly CatalogCard Card = card;
        public readonly CardPlay InstalledBy = installedBy;
        public readonly int XValue = xValue;
        public int Turn = -1;
        public int Used;
        public int N(int index) => Card.Number(index);
        public int V(string name) => Card.Value(name);
        public bool Take(int limit)
        {
            int turn = Card.Owner.PlayerCombatState!.TurnNumber;
            if (Turn != turn) { Turn = turn; Used = 0; }
            if (Used >= limit) return false;
            Used++;
            return true;
        }
        public CardPlay Play => new()
        {
            Card = Card, Target = null, ResultPile = PileType.None,
#if STS2_BETA
            Player = Card.Owner,
#endif
            Resources = InstalledBy.Resources, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1
        };
    }
    private List<Grant> _grants = [];
    public string DesignId => designId;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + $"images/powers/{DesignId}.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
    public override LocString Description
    {
        get
        {
            var description = new LocString("powers", "JUNNA_CATALOG.description");
            description.Add("Effects", string.Join("\n", _grants.Count == 0 ? [CardCatalog.Get(DesignId).Text] :
                _grants.Select(g => g.Card.IsUpgraded ? g.Card.Definition.UpgradedText : g.Card.Definition.Text)));
            return description;
        }
    }
    protected override void DeepCloneFields() { base.DeepCloneFields(); _grants = new(_grants); }

    public static bool Has(Player player, string id) => player.Creature.Powers.OfType<CatalogPower>().Any(p => p.DesignId == id);
    public static async Task Install(CatalogCard card, PlayerChoiceContext context, CardPlay play)
    {
        var canonical = ModelDb.AllPowers.OfType<CatalogPower>().Single(p => p.DesignId == card.DesignId);
        await PowerCmd.Apply(context, canonical.ToMutable(), card.Owner.Creature, 1, card.Owner.Creature, card);
        var power = card.Owner.Creature.Powers.OfType<CatalogPower>().FirstOrDefault(p => p.DesignId == card.DesignId);
        if (power == null) return;
        int xValue = card.Definition.CostsX ? card.ResolveEnergyXValue() : 0;
        var snapshot = (CatalogCard)card.MutableClone();
        power._grants.Add(new(snapshot, play, xValue));
    }

    private async Task Block(Grant grant, int amount)
        => await CreatureCmd.GainBlock(Owner, amount, ValueProp.Move, grant.Play);
    private async Task AccelerateChoice(Grant grant, PlayerChoiceContext context, int count, int turns)
    {
        var cards = ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player && !a.Resolving).Select(a => a.Card).ToArray();
        foreach (var card in await CardActions.Choose(context, grant.Card, cards, count))
            if (ArrowCombat.Find(card) is { } arrow) await ArrowCombat.Accelerate(context, arrow, turns);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner.Player) return;
        foreach (var grant in _grants.ToArray())
        {
            switch (DesignId)
            {
                case "JA35": await ArrowCombat.AccelerateAll(context, player, grant.N(0)); break;
                case "JK32": await CardPileCmd.Draw(context, grant.N(0), player); break;
                case "JK46": await CardActions.Generate(player, "JK46B", grant.N(0), PileType.Hand, grant.Card.IsUpgraded); break;
                case "JG34":
                    using (DamageExecution.BeginCardResolution(grant.Card))
                        await CardEffects.DealDamage(grant.Card, context, grant.Play, grant.V("Damage"), grant.XValue);
                    break;
                case "JH24": await PlayerCmd.GainEnergy(grant.N(1), player); break;
                case "JZ06": await PlayerCmd.GainEnergy(player.PlayerCombatState!.Hand.Cards.Where(CardActions.HasNumericCost).Select(CardActions.Cost).DefaultIfEmpty().Max(), player); break;
            }
        }
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        DesignId == "JH19" && card.Owner.Creature == Owner && card.Type != CardType.Power
            ? playCount + _grants.Count : playCount;

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (DesignId != "JH19" || card.Owner.Creature != Owner || card.Type == CardType.Power)
            return Task.CompletedTask;
        _grants.Clear();
        return PowerCmd.Remove(this);
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
                    if (grant.Used >= grant.N(0)) _grants.Remove(grant);
                    if (ArrowCombat.Find(play.Card) is { } immediate) await ArrowCombat.Accelerate(context, immediate, int.MaxValue);
                    break;
                case "JK10" when play.Resources.EnergyValue >= grant.N(1) && grant.Take(grant.N(0)):
                    await CardPileCmd.Draw(context, grant.N(2), play.Card.Owner);
                    await PlayerCmd.GainEnergy(grant.N(3), play.Card.Owner); break;
                case "JK15" when CardCatalog.IsKnowledge(play.Card) && grant.Take(grant.N(0)):
                    using (KnowledgeCombat.SingleCard())
                    {
                        var clone = play.Card.CreateCloneForPlayer(play.Card.Owner);
                        await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Draw, play.Card.Owner);
                    }
                    break;
                case "JK37C" when play.Resources.EnergyValue == 0:
                    await AccelerateChoice(grant, context, 1, grant.N(1)); break;
                case "JA45" when CardCatalog.IsArrow(play.Card) && grant.Take(grant.N(0)):
                    if (ArrowCombat.Find(play.Card) is { } arrow) await ArrowCombat.Accelerate(context, arrow, grant.N(1));
                    break;
                case "JK64" when CardCatalog.IsKnowledge(play.Card):
                    await AccelerateChoice(grant, context, 1, grant.N(0)); break;
                case "JG31": DamageModifiers.Add(play.Card, grant.V("Bonus")); break;
                case "JK75" when CardCatalog.IsKnowledge(play.Card):
                    var eligible = play.Card.Owner.PlayerCombatState!.Hand.Cards.Where(DamageModifiers.CanDealDamage).ToArray();
                    if (eligible.Length > 0) DamageModifiers.Add(play.Card.Owner.RunState.Rng.CombatCardSelection.NextItem(eligible)!, grant.V("Bonus"));
                    break;
                case "JH23" when play.Resources.EnergyValue >= grant.N(1) && grant.Take(grant.N(0)):
                    await Block(grant, grant.V("Block")); break;
                case "JA70" when CardCatalog.IsArrow(play.Card): await Block(grant, grant.V("Block")); break;
                case "JK80" when CardCatalog.IsKnowledge(play.Card):
                    _grants.Remove(grant);
                    await Block(grant, grant.N(1)); break;
            }
        }
        if (_grants.Count == 0) await PowerCmd.Remove(this);
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        if (DesignId == "JK27" && card.Owner == Owner.Player && CardCatalog.IsKnowledge(card))
            await KnowledgeCombat.DrawPartners(context, card);
    }

    public override Task BeforeFlush(PlayerChoiceContext context, Player player)
    {
        if (DesignId == "JK81" && player == Owner.Player)
            foreach (var card in player.PlayerCombatState!.Hand.Cards)
                if (_grants.Any(g => CardActions.Cost(card) >= g.N(0))) card.GiveSingleTurnRetain();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;
        foreach (var grant in _grants.ToArray())
        {
            switch (DesignId)
            {
                case "A": await CardActions.Generate(Owner.Player!, "B", grant.N(0), PileType.Draw); break;
                case "JK67":
                    foreach (var card in CardActions.All(Owner.Player!).Where(CardCatalog.IsArrow)) DamageModifiers.Add(card, grant.V("Bonus"));
                    break;
                case "JH21" when Owner.Player!.PlayerCombatState!.Energy > 0: await Block(grant, grant.V("Block")); break;
            }
        }
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner) && DesignId is "JK64" or "JG31")
            await PowerCmd.Remove(this);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (DesignId != "JZ05" || dealer != Owner || target.Side == Owner.Side || result.TotalDamage <= 0 || !props.IsPoweredAttack()) return;
        int ordinal = AttackEventHistory.Ordinal(result);
        foreach (var grant in _grants.ToArray())
            if (ordinal > 0 && ordinal <= grant.N(0))
                await CardActions.Fly(context, Owner.Player!, result.TotalDamage, target, grant.Card.IsUpgraded);
    }

    public override Task AfterEnergySpent(CardModel card, int amount) => card.Owner == Owner.Player && amount > 0 && DesignId == "JH30"
        ? EnergyDamage(new ThrowingPlayerChoiceContext(), amount) : Task.CompletedTask;

    public static async Task EnergyChanged(Player player, int amount)
    {
        if (amount <= 0) return;
        foreach (var power in player.Creature.Powers.OfType<CatalogPower>().Where(p => p.DesignId == "JH30").ToArray())
            await power.EnergyDamage(new ThrowingPlayerChoiceContext(), amount);
    }
    private async Task EnergyDamage(PlayerChoiceContext context, int amount)
    {
        foreach (var grant in _grants.ToArray())
        {
            var targets = Owner.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
            if (targets.Length == 0) return;
            var target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(targets);
            using (DamageExecution.BeginCardResolution(grant.Card))
                await GameApi.Damage(context, [target!], amount, ValueProp.Move, Owner, grant.Card, grant.Play);
        }
    }
}
