using BaseLib.Abstracts;
using System.Runtime.CompilerServices;
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
    private sealed class Grant(CatalogCard card, CardPlay installedBy, int xValue)
    {
        public readonly CatalogCard Card = card;
        public readonly CardPlay InstalledBy = installedBy;
        public readonly int XValue = xValue;
        public int BaseHits => Card.Definition.CostsX ? XValue : 1;
        public int Turn = -1;
        public int Used;
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
    private ConditionalWeakTable<CardPlay, CardModel[]> _partnerHands = new();
    internal int ReplayGrants => _grants.Count;
    public string DesignId => designId;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => CardRules.Get(DesignId).SinglePower ? PowerStackType.Single : PowerStackType.Counter;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + $"images/powers/{DesignId}.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _grants = new(_grants);
        _partnerHands = new();
    }

    public static int ArrowHaste(Player player) => player.Creature.Powers.OfType<CatalogPower>()
        .Where(p => p.DesignId == "JA105").Sum(p => p._grants.Count);

    public static bool Has(Player player, string id) => player.Creature.Powers.OfType<CatalogPower>().Any(p => p.DesignId == id);
    public static async Task Install(CatalogCard card, PlayerChoiceContext context, CardPlay play)
    {
        var canonical = ModelDb.AllPowers.OfType<CatalogPower>().Single(p => p.DesignId == card.DesignId);
        var existing = card.Owner.Creature.Powers.OfType<CatalogPower>().FirstOrDefault(p => p.DesignId == card.DesignId);
        if (canonical.StackType == PowerStackType.Single && existing != null)
        {
            if (card.DesignId == "JK81" && card.Value("Threshold") < existing._grants.Min(g => g.V("Threshold")))
            {
                existing._grants = [new(ShoujoKageki.Core.V1.CardCopies.Snapshot(card), play, 0)];
                existing.Synchronize();
            }
            return;
        }
        await PowerCmd.Apply(context, canonical.ToMutable(), card.Owner.Creature, 1, card.Owner.Creature, card);
        var power = card.Owner.Creature.Powers.OfType<CatalogPower>().FirstOrDefault(p => p.DesignId == card.DesignId);
        if (power == null) return;
        int xValue = card.Definition.CostsX ? card.ResolveEnergyXValue() : 0;
        var snapshot = ShoujoKageki.Core.V1.CardCopies.Snapshot(card);
        power._grants.Add(new(snapshot, play, xValue));
        if (card.DesignId == "JK127") KnowledgeProgress.RegisterCopy(card.Owner);
        power.Synchronize();
    }

    private async Task Block(Grant grant, int amount)
        => await CreatureCmd.GainBlock(Owner, amount, ValueProp.Move, grant.Play);
    private async Task AccelerateChoice(Grant grant, PlayerChoiceContext context, int count, int turns)
    {
        var cards = ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player && !a.Resolving).Select(a => a.Card).ToArray();
        foreach (var card in await CardActions.Choose(context, grant.Card, cards, count))
            if (ArrowCombat.Find(card) is { } arrow) await ArrowCombat.Accelerate(context, arrow, turns);
    }

}
