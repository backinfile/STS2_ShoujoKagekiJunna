using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace JunnaIntegration;

// Native API differences only; rule assertions remain in the probes.
internal static class TestGameApi
{
    internal static Task LoseBlock(PlayerChoiceContext context, Creature creature, decimal amount, Creature? dealer) =>
#if STS2_BETA
        CreatureCmd.LoseBlock(context, creature, amount, dealer);
#else
        CreatureCmd.LoseBlock(creature, amount);
#endif
    internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext context, IEnumerable<Creature> targets,
        decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play) =>
#if STS2_BETA
        CreatureCmd.Damage(context, targets, amount, props, dealer, source, play);
#else
        CreatureCmd.Damage(context, targets, amount, props, dealer, source);
#endif
    internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext context, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? source = null, CardPlay? play = null)
        => Damage(context, [target], amount, props, dealer, source, play);
#if !STS2_BETA
    internal static AttackCommand FromCard(this AttackCommand attack, CardModel card, CardPlay? play) => attack.FromCard(card);
    internal static CardModel CreateCloneForPlayer(this CardModel card, Player owner)
    {
        var copy = card.CreateClone(); copy.Owner = null!; copy.Owner = owner; return copy;
    }
#endif
}
