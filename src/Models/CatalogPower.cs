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

public abstract partial class CatalogPower(string designId) : CustomPowerModel
{
    protected sealed class Grant(CatalogCard card, CardPlay installedBy, int xValue)
    {
        public readonly CatalogCard Card = card;
        public readonly CardPlay InstalledBy = installedBy;
        public readonly int XValue = xValue;
        public int BaseHits => Card.Definition.CostsX ? XValue : 1;
        public int Turn = -1;
        public int Used;
        public int CycleIndex;
        internal Grant Copy() => new(ShoujoKageki.Core.V1.CardCopies.Snapshot(Card), InstalledBy, XValue)
        { Turn = Turn, Used = Used, CycleIndex = CycleIndex };
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
            Card = Card,
            Target = null,
            ResultPile = PileType.None,
#if STS2_MAIN || STS2_BETA
            Player = Card.Owner,
#endif
            Resources = InstalledBy.Resources,
            IsAutoPlay = true,
            PlayIndex = 0,
            PlayCount = 1
        };
    }
    protected List<Grant> _grants = [];
    public string DesignId => designId;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => CardRules.Get(DesignId).SinglePower ? PowerStackType.Single : PowerStackType.Counter;
    protected virtual string IconId => DesignId;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + $"images/powers/{IconId}.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _grants = _grants.Select(grant => grant.Copy()).ToList();
    }


    public static bool Has(Player player, string id) => player.Creature.Powers.OfType<CatalogPower>().Any(p => p.DesignId == id);
    public static async Task Install<T>(CatalogCard card, PlayerChoiceContext context, CardPlay play) where T : CatalogPower
    {
        var canonical = ModelDb.Power<T>();
        await canonical.BeforeInstall(card, context, play);
        await Apply();
        canonical.AfterInstallAttempt(card);

        async Task Apply()
        {
            var existing = card.Owner.Creature.GetPower<T>();
            if (canonical.StackType == PowerStackType.Single && existing != null)
            {
                existing.MergeSingle(card, play);
                return;
            }
            var power = existing ?? (T)canonical.ToMutable();
            int previous = power.Amount;
            int xValue = card.Definition.CostsX ? card.ResolveEnergyXValue() : 0;
            var snapshot = ShoujoKageki.Core.V1.CardCopies.Snapshot(card);
            var grant = new Grant(snapshot, play, xValue);
            power._grants.Add(grant);
            if (existing != null) power.RefreshDescription();
            try
            {
                await PowerCmd.Apply(context, power, card.Owner.Creature, power.AmountPerGrant(grant), card.Owner.Creature, card);
                if (card.Owner.Creature.GetPower<T>() == power && power.Amount > previous)
                    power.Installed(card);
            }
            finally
            {
                if (power.Amount <= previous) power._grants.Remove(grant);
                if (card.Owner.Creature.GetPower<T>() == power) power.Synchronize();
            }
        }
    }
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (_grants.Count > 0) Synchronize();
        return Task.CompletedTask;
    }
    protected virtual Task BeforeInstall(CatalogCard card, PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected virtual int AmountPerGrant(Grant grant) => 1;
    protected virtual void MergeSingle(CatalogCard card, CardPlay play) { }
    protected virtual void Installed(CatalogCard card) { }
    protected virtual void AfterInstallAttempt(CatalogCard card) { }

    protected async Task Block(int amount)
        => await CreatureCmd.GainBlock(Owner, amount, ValueProp.Unpowered, null);
    protected async Task AccelerateChoice(Grant grant, PlayerChoiceContext context, int count, int turns)
    {
        var cards = ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player && !a.Resolving).Select(a => a.Card).ToArray();
        foreach (var card in await CardActions.Choose(context, grant.Card, cards, count))
            if (ArrowCombat.Find(card) is { } arrow) await ArrowCombat.Accelerate(context, arrow, turns);
    }

}
