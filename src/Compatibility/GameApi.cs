using MegaCrit.Sts2.Core.Hooks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShoujoKagekiJunna;

internal static class GameApi
{
    internal static bool HasCardSelector(Player player)
    {
#if STS2_BETA
        var selectors = MegaCrit.Sts2.Core.Runs.RunManager.Instance.CardSelectorStack;
        return selectors.GetSelector(player) != null || selectors.LocalSelector != null;
#elif STS2_MAIN
        return CardSelectCmd.Selector != null || CardSelectCmd.LocalSelector != null;
#else
        return CardSelectCmd.Selector != null;
#endif
    }

    internal static CardPlay WithTarget(CardPlay play, Creature target) => new()
    {
        Card = play.Card, Target = target, ResultPile = play.ResultPile,
#if STS2_MAIN || STS2_BETA
        Player = play.Card.Owner,
#endif
        Resources = play.Resources, IsAutoPlay = play.IsAutoPlay, PlayIndex = play.PlayIndex, PlayCount = play.PlayCount
    };
    internal static StringName ConfirmHotkey =>
#if STS2_MAIN || STS2_BETA
        MegaInput.confirm;
#else
        MegaInput.accept;
#endif
    internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext context, IEnumerable<Creature>? targets,
        decimal amount, ValueProp props, Creature? dealer, CardModel? card, CardPlay? play) =>
#if STS2_MAIN || STS2_BETA
        CreatureCmd.Damage(context, targets!, amount, props, dealer, card, play);
#else
        CreatureCmd.Damage(context, targets!, amount, props, dealer, card);
#endif
}
