using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public abstract partial class CatalogPower
{
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner == Owner.Player && play.IsFirstInSeries) BeforePlay(play);
        return Task.CompletedTask;
    }
    protected virtual void BeforePlay(CardPlay play) { }
    protected virtual Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected virtual async Task AfterPlay(PlayerChoiceContext context, CardPlay play)
    {
        foreach (var grant in _grants.ToArray())
            if (!ReferenceEquals(play, grant.InstalledBy)) await CardPlayed(grant, context, play);
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || !play.IsFirstInSeries) return;
        await AfterPlay(context, play);
        if (_grants.Count == 0) await PowerCmd.Remove(this);
        else Synchronize();
    }
}
