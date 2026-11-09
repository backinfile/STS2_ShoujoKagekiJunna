using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna;

internal static class GameApi
{
    internal static CardPlay WithTarget(CardPlay play, Creature target) => new()
    {
        Card = play.Card, Target = target, ResultPile = play.ResultPile,
#if STS2_BETA
        Player = play.Card.Owner,
#endif
        Resources = play.Resources, IsAutoPlay = play.IsAutoPlay, PlayIndex = play.PlayIndex, PlayCount = play.PlayCount
    };
    internal static StringName ConfirmHotkey =>
#if STS2_BETA
        MegaInput.confirm;
#else
        MegaInput.accept;
#endif
    internal static CardPlay? AttackCardPlay(AttackCommand attack) =>
#if STS2_BETA
        attack.CardPlay;
#else
        null;
#endif
    internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext context, IEnumerable<Creature>? targets,
        decimal amount, ValueProp props, Creature? dealer, CardModel? card, CardPlay? play) =>
#if STS2_BETA
        CreatureCmd.Damage(context, targets!, amount, props, dealer, card, play);
#else
        CreatureCmd.Damage(context, targets!, amount, props, dealer, card);
#endif
}
