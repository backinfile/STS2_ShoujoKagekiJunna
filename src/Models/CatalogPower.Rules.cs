using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed partial class JunnaPowerA
{
    protected override async Task TurnEnding(Grant grant, PlayerChoiceContext context)
    {
        if (Owner.IsDead) return;
        using var single = KnowledgeCombat.SingleCard();
        var added = new List<CardPileAddResult>();
        for (int i = 0; i < grant.V("Cards"); i++)
        {
            var card = Owner.CombatState!.CreateCard<JunnaCardB>(Owner.Player!);
            added.Add(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, Owner.Player, CardPilePosition.Random));
        }
        CardCmd.PreviewCardPileAdd(added, style: added.Count > 5 ? CardPreviewStyle.MessyLayout : CardPreviewStyle.HorizontalLayout);
    }
}

public sealed partial class JunnaPowerJA105
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(CardCatalog.IsArrow(play.Card))) return;
        if (ArrowCombat.Find(play.Card) is { } accelerated)
            await ArrowCombat.Accelerate(context, accelerated, grant.V("Turns"));
    }
}

public sealed partial class JunnaPowerJA118
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(CardCatalog.IsArrow(play.Card))) return;
        await Block(grant.V("Block"));
    }
}

public sealed partial class JunnaPowerJA144
{
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner.Player) return;
        foreach (var grant in _grants.ToArray())
            await CardActions.Generate(player, "JA119", grant.V("Cards"), PileType.Hand, CardPilePosition.Bottom);
    }
}

public sealed partial class JunnaPowerJA35
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        await ArrowCombat.AccelerateAll(context, Owner.Player!, grant.V("Turns"));
    }
}

public sealed partial class JunnaPowerJA87
{
    protected override async Task BeforeHandFlush(PlayerChoiceContext context, Player player)
    {
        foreach (var grant in _grants.ToArray())
            await HandArrow.Select(context, grant.Card, grant.V("Cards"), optional: true);
    }
}

public sealed partial class JunnaPowerJA96
{
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 || !props.IsPoweredAttack()) return;
        foreach (var grant in _grants.ToArray())
            await ArrowCombat.AccelerateEnemies(context, Owner.Player!, grant.V("Turns"));
    }
}

public sealed partial class JunnaPowerJG34
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        using (DamageExecution.BeginCardResolution(grant.Card))
            for (int hit = 0; hit < grant.BaseHits; hit++)
            {
                var targets = Owner.Player!.Creature.CombatState!.HittableEnemies.Where(target => target.IsAlive).ToArray();
                if (targets.Length == 0) break;
                await GameApi.Damage(context, targets, grant.V("Damage"), ValueProp.Unpowered, Owner, grant.Card, grant.Play);
            }
    }
}

public sealed partial class JunnaPowerJH19
{
    protected override bool EndsWithTurn => true;

    protected override Task BeforeInstall(CatalogCard card, PlayerChoiceContext context, CardPlay play)
        => CreatureCmd.GainBlock(card.Owner.Creature, card.Value("Block"), ValueProp.Move, play);
}

public sealed partial class JunnaPowerJH31
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(play.Resources.EnergyValue >= grant.V("EnergyThreshold"))) return;
        await CardActions.DrawMatching(context, play.Card.Owner, c => CardActions.HasNumericCost(c) && CardActions.Cost(c) == 0, grant.V("Amount"));
    }
}

public sealed partial class JunnaPowerJH59
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        await PlayerCmd.GainEnergy(grant.V("Energy"), Owner.Player!);
    }
}

public sealed partial class JunnaPowerJK10
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(play.Resources.EnergyValue >= grant.V("EnergyThreshold") && grant.Take(1))) return;
        await CardPileCmd.Draw(context, grant.V("DrawCount"), play.Card.Owner);
        await PlayerCmd.GainEnergy(grant.V("Energy"), play.Card.Owner);
    }
}

public sealed partial class JunnaPowerJK127
{
    protected override void Installed(CatalogCard card) => KnowledgeProgress.RegisterCopy(card.Owner);
}

public sealed partial class JunnaPowerJK139
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        await PlayerCmd.GainEnergy(grant.V("Energy"), Owner.Player!);
    }
}

public sealed partial class JunnaPowerJK15
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(CardCatalog.IsKnowledge(play.Card) && grant.Take(1))) return;
        using (KnowledgeCombat.SingleCard())
        {
            var clone = play.Card.CreateCloneForPlayer(play.Card.Owner);
            var result = await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Draw, play.Card.Owner, CardPilePosition.Random);
            await CardActions.PreviewAdded([result], PileType.Draw);
        }
    }
}

public sealed partial class JunnaPowerJK235
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(CardCatalog.IsKnowledge(play.Card))) return;
        var targets = Owner.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
        if (targets.Length > 0)
        {
            var target = play.Card.Owner.RunState.Rng.CombatTargets.NextItem(targets)!;
            using (DamageExecution.BeginCardResolution(grant.Card))
                await GameApi.Damage(context, [target], grant.V("Damage"), ValueProp.Unpowered, Owner, grant.Card, grant.Play);
        }
    }
}

public sealed partial class JunnaPowerJK254
{
    protected override void BeforePlay(CardPlay play)
    {
        if (!CardCatalog.IsKnowledge(play.Card) || !_grants.Any(grant => !ReferenceEquals(play, grant.InstalledBy))) return;
        foreach (var partner in KnowledgeCombat.CapturePartners(play.Card))
        {
            partner.EnergyCost.SetUntilPlayed(0);
            partner.InvokeEnergyCostChanged();
        }
    }
}

public sealed partial class JunnaPowerJK260
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(CardCatalog.IsKnowledge(play.Card) && grant.Take(1))) return;
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(
                                context, Owner, grant.V("Amount"), Owner, grant.Card);
    }

    protected override int? RemainingTriggers => _grants.Count(grant =>
        grant.Turn != Owner.Player!.PlayerCombatState!.TurnNumber || grant.Used == 0);
}

public sealed partial class JunnaPowerJK27
{
    public override Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
        => card.Owner == Owner.Player && CardCatalog.IsKnowledge(card)
            ? KnowledgeCombat.DrawPartners(context, card) : Task.CompletedTask;
}

public sealed partial class JunnaPowerJK32
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        await CardPileCmd.Draw(context, grant.V("DrawCount"), Owner.Player!);
    }

    protected override void AfterInstallAttempt(CatalogCard card) => CardEffects.SetNamedFree(card.Owner, "JK32B", "JK32C");
}

public sealed partial class JunnaPowerJK37C
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(play.Resources.EnergyValue == 0)) return;
        var eligibleArrows = ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player && !a.Resolving && a.Remaining > 0).ToArray();
        if (eligibleArrows.Length > 0)
            await ArrowCombat.Accelerate(context, Owner.Player!.RunState.Rng.CombatCardSelection.NextItem(eligibleArrows)!, grant.V("Turns"));
    }
}

public sealed partial class JunnaPowerJK46
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        await CardActions.Generate(Owner.Player!, "JK46B", grant.V("Cards"), PileType.Hand, CardPilePosition.Bottom, grant.Card.IsUpgraded);
    }
}

public sealed partial class JunnaPowerJK67
{
    protected override async Task TurnEnding(Grant grant, PlayerChoiceContext context)
    {
        DamageModifiers.AddMany(ArrowCombat.Deployed.Where(a => a.Card.Owner == Owner.Player).Select(a => a.Card), grant.V("Bonus"));
    }
}

public sealed partial class JunnaPowerJK75
{
    protected override async Task CardPlayed(Grant grant, PlayerChoiceContext context, CardPlay play)
    {
        if (!(CardCatalog.IsKnowledge(play.Card))) return;
        var eligible = play.Card.Owner.PlayerCombatState!.Hand.Cards.Where(DamageModifiers.CanDealDamage).ToArray();
        if (eligible.Length > 0) DamageModifiers.Add(play.Card.Owner.RunState.Rng.CombatCardSelection.NextItem(eligible)!, grant.V("Bonus"));
    }
}

public sealed partial class JunnaPowerJZ05
{
    protected override int? RemainingTriggers => Math.Max(0,
        _grants.Sum(grant => grant.V("Limit")) - AttackEventHistory.Count(Owner.Player!));
    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || target.Side == Owner.Side || result.TotalDamage <= 0 || !props.IsPoweredAttack()) return;
        int ordinal = AttackEventHistory.Ordinal(result);
        Synchronize();
        if (ordinal > 0 && ordinal <= _grants.Sum(grant => grant.V("Limit")))
            await CardActions.Fly(context, Owner.Player!, result.TotalDamage, target);
    }
}

public sealed partial class JunnaPowerJZ06
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        await PlayerCmd.GainEnergy(Owner.Player!.PlayerCombatState!.Hand.Cards.Where(CardActions.HasNumericCost).Select(CardActions.Cost).DefaultIfEmpty().Max(), Owner.Player!);
    }
}

public sealed partial class JunnaPowerU_CARD_008
{
    protected override string IconId => "JK127";
    protected override void Installed(CatalogCard card) => KnowledgeProgress.RegisterStory(card.Owner);
}

public sealed partial class JunnaPowerU_CARD_010
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        if (!grant.Take(1)) return;
        int category = new[] { 1, 0, 2 }[grant.CycleIndex];
        grant.CycleIndex = (grant.CycleIndex + 1) % 3;
        if (Owner.Player!.PlayerCombatState!.Hand.Cards.Count >= CardPile.MaxCardsInHand) return;
        var candidates = Owner.Player!.PlayerCombatState.DrawPile.Cards.Where(c => CardTutors.Matches(c, category));
        foreach (var selected in await CardActions.Choose(context, grant.Card, candidates))
            await CardPileCmd.Add(selected, PileType.Hand);
    }

    protected override string IconId => "JK127";
}
