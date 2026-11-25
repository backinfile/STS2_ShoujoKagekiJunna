using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
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
    internal bool MakesArrowsExhaust => _grants.Count > 0;
    internal int AccelerationTurns => _grants.Sum(grant => Math.Max(0, grant.V("Turns")));

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

public sealed partial class JunnaPowerJA205
{
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource
#if STS2_MAIN || STS2_BETA
        , CardPlay? cardPlay
#endif
        )
    {
        if (dealer != Owner || cardSource == null || !CardCatalog.IsArrow(cardSource) || _grants.Count == 0)
            return 1m;
        return 1m + _grants[0].V("DamagePercent") / 100m;
    }
}

public sealed partial class JunnaPowerJA87
{
    protected override int AmountPerGrant(Grant grant) => grant.V("Cards");

    protected override Task BeforeHandFlush(PlayerChoiceContext context, Player player)
        => _grants.Count > 0 && Amount > 0
            ? HandArrow.Select(context, _grants[0].Card, Amount, optional: true)
            : Task.CompletedTask;
}

public sealed partial class JunnaPowerJA22
{
    internal ArrowDestination Redirect(ArrowDeployment arrow) => arrow.Destination.Pile == PileType.Discard
        ? new(arrow.Card.Owner, PileType.Hand, CardPilePosition.Bottom)
        : arrow.Destination;
}

public sealed partial class JunnaPowerJH19
{
    internal async Task<int[]> BuyReplays(CardModel card, int count)
    {
        int grants = _grants.Count;
        await PowerCmd.Remove(this);
        int originalX = card.EnergyCost.CostsX ? card.EnergyCost.CapturedXValue : 0;
        var values = Enumerable.Repeat(originalX, count).ToList();
        for (int i = 0; i < grants; i++)
        {
            if (!card.Owner.PlayerCombatState!.HasEnoughResourcesFor(card, out _)) break;
            var (energy, _) = await card.SpendResources();
            values.Add(card.EnergyCost.CostsX ? energy : originalX);
        }
        if (card.EnergyCost.CostsX) card.EnergyCost.CapturedXValue = originalX;
        return values.ToArray();
    }

    protected override bool EndsWithTurn => true;

    protected override Task BeforeInstall(CatalogCard card, PlayerChoiceContext context, CardPlay play)
        => CreatureCmd.GainBlock(card.Owner.Creature, card.Value("Block"), ValueProp.Move, play);
}

public sealed partial class JunnaPowerJK127
{
    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        var player = Owner.Player!;
        foreach (var grant in _grants)
            room.AddExtraReward(player, new KnowledgeCopyReward(player, 1));
        return Task.CompletedTask;
    }
}

public sealed partial class JunnaPowerJK139
{
    protected override async Task TurnStarted(Grant grant, PlayerChoiceContext context)
    {
        await PlayerCmd.GainEnergy(grant.V("Energy"), Owner.Player!);
    }
}

public sealed partial class JunnaPowerJK235
{
    protected override int AmountPerGrant(Grant grant) => grant.V("Damage");
    protected override int StackAmount() => Amount;

    protected override async Task AfterPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!CardCatalog.IsKnowledge(play.Card)) return;
        var source = _grants.Find(grant => !ReferenceEquals(play, grant.InstalledBy));
        if (source == null) return;
        int damage = Amount - _grants.Where(grant => ReferenceEquals(play, grant.InstalledBy)).Sum(AmountPerGrant);
        if (damage <= 0) return;
        var targets = Owner.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
        if (targets.Length == 0) return;
        var target = play.Card.Owner.RunState.Rng.CombatTargets.NextItem(targets)!;
        await GameApi.Damage(context, [target], damage, ValueProp.Unpowered, Owner, source.Card, source.Play);
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
    protected override async Task AfterPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.EnergyCost.CostsX || play.Resources.EnergyValue != 0) return;
        int amount = _grants.Where(grant => !ReferenceEquals(play, grant.InstalledBy)).Sum(grant => grant.V("Amount"));
        if (amount <= 0) return;
        await PowerCmd.Apply<JunnaDistantStarsStrengthPower>(context, Owner, amount, Owner, play.Card);
        await PowerCmd.Apply<JunnaDistantStarsDexterityPower>(context, Owner, amount, Owner, play.Card);
    }
}

public sealed partial class JunnaPowerU_CARD_008
{
    protected override void Installed(CatalogCard card) => KnowledgeProgress.RegisterStory(card.Owner);
}

public sealed partial class JunnaPowerU_CARD_010
{
    private static readonly int[] Categories = [1, 0, 2];
    private static readonly string[] CategoryTitles = ["JUNNA_PREPARE_KNOWLEDGE.title", "JUNNA_PREPARE_ARROWS.title", "JUNNA_PREPARE_SHINE.title"];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner.Player) return;
        var combat = player.PlayerCombatState!;
        foreach (var phase in _grants.GroupBy(grant => grant.CycleIndex))
        {
            var grants = phase.Where(grant => grant.Take(1)).ToArray();
            if (grants.Length == 0) continue;

            foreach (var grant in grants) grant.CycleIndex = (phase.Key + 1) % Categories.Length;
            Synchronize();

            int room = CardPile.MaxCardsInHand - combat.Hand.Cards.Count;
            if (room <= 0) continue;
            var candidates = combat.DrawPile.Cards.Where(card => CardTutors.Matches(card, Categories[phase.Key])).ToArray();
            int count = Math.Min(grants.Sum(grant => grant.V("DrawCount")), Math.Min(room, candidates.Length));
            if (count <= 0) continue;

            var source = grants[0].Card;
            var prompt = CardChoices.Prompt(source, count);
            prompt.Add("Category", new LocString("cards", CategoryTitles[phase.Key]));
            foreach (var selected in await CardActions.Choose(context, source, candidates, count, prompt: prompt))
                await CardPileCmd.Add(selected, PileType.Hand);
        }
    }
}
