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
    private sealed record EffectData(CardModel Source, IReadOnlyDictionary<string, int> Values,
        bool IsUpgraded, bool CostsX, int XValue, ResourceInfo Resources)
    {
        internal static EffectData Capture(CatalogCard card, CardPlay play)
        {
            var values = card.Definition.Values.SelectMany(value => value.Aliases.Prepend(value.Name)
                .Select(name => new KeyValuePair<string, int>(name, card.DynamicVars[value.Name].IntValue)))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return new(ShoujoKageki.Core.V1.CardCopies.Snapshot<CardModel>(card), values,
                card.IsUpgraded, card.Definition.CostsX,
                card.Definition.CostsX ? card.ResolveEnergyXValue() : 0, play.Resources);
        }
    }
    private sealed class Grant(EffectData effect, CardPlay installedBy)
    {
        public CardModel Source => effect.Source;
        public bool IsUpgraded => effect.IsUpgraded;
        public readonly CardPlay InstalledBy = installedBy;
        public int BaseHits => effect.CostsX ? effect.XValue : 1;
        public int Turn = -1;
        public int Used;
        public int V(string name) => effect.Values.TryGetValue(name, out var value) ? value
            : throw new InvalidDataException($"Power {Source.Id} has no field {name}.");
        public Grant Copy() => new(effect, InstalledBy) { Turn = Turn, Used = Used };
        public bool Take(int limit)
        {
            int turn = Source.Owner.PlayerCombatState!.TurnNumber;
            if (Turn != turn) { Turn = turn; Used = 0; }
            if (Used >= limit) return false;
            Used++;
            return true;
        }
        public CardPlay Play => new()
        {
            Card = Source, Target = null, ResultPile = PileType.None,
#if STS2_BETA
            Player = Source.Owner,
#endif
            Resources = effect.Resources, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1
        };
    }
    private List<Grant> _grants = [];
    internal int ReplayGrants => _grants.Count;
    public string DesignId => designId;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => DesignId is "JA22" or "JK27" or "JK81" ? PowerStackType.Single : PowerStackType.Counter;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + $"images/powers/{DesignId}.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
    protected override void DeepCloneFields() { base.DeepCloneFields(); _grants = _grants.Select(grant => grant.Copy()).ToList(); }

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
                existing._grants = [new(EffectData.Capture(card, play), play)];
                existing.Synchronize();
            }
            return;
        }
        await PowerCmd.Apply(context, canonical.ToMutable(), card.Owner.Creature, 1, card.Owner.Creature, card);
        var power = card.Owner.Creature.Powers.OfType<CatalogPower>().FirstOrDefault(p => p.DesignId == card.DesignId);
        if (power == null) return;
        power._grants.Add(new(EffectData.Capture(card, play), play));
        if (card.DesignId == "JK127") KnowledgeProgress.RegisterCopy(card.Owner);
        power.Synchronize();
    }

    private async Task Block(Grant grant, int amount)
        => await CreatureCmd.GainBlock(Owner, amount, ValueProp.Move, grant.Play);
    private async Task AccelerateChoice(Grant grant, PlayerChoiceContext context, int count, int turns)
    {
        var cards = ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player && !a.Resolving).Select(a => a.Card).ToArray();
        foreach (var card in await CardActions.Choose(context, grant.Source, cards, count))
            if (ArrowCombat.Find(card) is { } arrow) await ArrowCombat.Accelerate(context, arrow, turns);
    }

}
