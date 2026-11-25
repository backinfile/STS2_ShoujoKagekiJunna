using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaPowerU_CARD_016
{
    // CardTutors orders the categories as Arrow, Knowledge, Shine.
    private static readonly int[] CategoryOrder = [1, 0, 2];
    private static readonly string[] CategoryIcons =
        ["U_CARD_016_Knowledge", "U_CARD_016_Arrow", "U_CARD_016_Shine"];

    private int _cycleIndex;

    public override string CustomPackedIconPath =>
        JunnaMod.ResourceRoot + $"images/response_markers/{CategoryIcons[_cycleIndex]}.svg";
    protected override int CategoryCycleIndex(Grant grant) => _cycleIndex;
    protected override int StackAmount() => _grants.Sum(grant => grant.V("DrawCount"));

    internal bool Matches(CardModel card) => CardTutors.Matches(card, CategoryOrder[_cycleIndex]);

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || !play.IsFirstInSeries) return;
        if (_grants.Count == 0)
        {
            await PowerCmd.Remove(this);
            return;
        }
        if (!Matches(play.Card) || _grants.Any(grant => ReferenceEquals(play, grant.InstalledBy))) return;

        int draw = Amount;
        // Refresh the next category before the draw animates, even when drawing is blocked.
        _cycleIndex = (_cycleIndex + 1) % CategoryOrder.Length;
        Synchronize();
        Flash();
        await CardPileCmd.Draw(context, draw, Owner.Player!);
    }
}
