namespace ShoujoKagekiJunna.Models;

internal enum CardTip { Knowledge, Arrow, DamageBonus, Vulnerable, Weak, Retain, Energy, Meteor }
internal enum SimpleCardEffect { Custom, Noop, Attack, Block, Draw, Energy, AttackAndBlock, BlockAndDraw, BlockAndEnergy, DrawAndEnergy, AttackAndDraw }
internal enum PowerAmountKind { Applications, RemainingLimit, Threshold, Bonus, Block, Amount }
internal enum PowerDescriptionKind
{
    None, RemainingUses, Passive, Applications, ThresholdSum, BonusSum, RepeatedApplications,
    BlockSum, AmountWithThreshold, MinimumThreshold, EnergyThresholdGroups, ArrowLimitGroups,
    BlockLimitGroups, TurnGroups, BonusGroups, LimitGroups, GeneratedCardGroups, DamageGroups
}
internal sealed class CardRuleDefinition
{
    internal IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();
    internal CardTip[] Tips { get; init; } = [];
    internal CardTip[] UpgradedTips { get; init; } = [];
    internal string[] RelatedCards { get; init; } = [];
    internal SimpleCardEffect Effect { get; init; }
    internal PowerAmountKind PowerAmount { get; init; }
    internal PowerDescriptionKind PowerDescription { get; init; }
    internal bool SinglePower { get; init; }
    internal string Field(string name) => Fields.GetValueOrDefault(name, name);
    internal bool HasTip(CardTip tip, bool upgraded) => (upgraded ? UpgradedTips : Tips).Contains(tip);
}
internal static partial class CardRules
{
    internal static CardRuleDefinition Get(string id) => Definitions[id];
}
