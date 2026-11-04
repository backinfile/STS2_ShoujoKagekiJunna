using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using BaseLib.Utils.Attributes;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public sealed class ArrowPile() : CustomPile(PileId)
{
    [CustomEnum] public static PileType PileId;
    [CustomEnum] public static CardType ArrowType;
    public override bool CardShouldBeVisible(CardModel card) => false;
    public override bool NeedsCustomTransitionVisual => true;
    public override bool CustomTween(Tween tween, CardModel card, NCard cardNode, CardPile? oldPile)
        => NCombatRoom.Instance?.GetNodeOrNull<ArrowHud>("JunnaArrows")?.AnimateDeployment(tween, card, cardNode) == true;
    public override Vector2 GetTargetPosition(CardModel model, Vector2 size) => new(size.X * .5f, 90);
}

public sealed class ArrowDeployment(CardModel card, CardPlay play, int remaining, int round,
    Func<PlayerChoiceContext, CardPlay, Task> resolve)
{
    public CardModel Card { get; } = card;
    public CardPlay Play { get; } = play;
    public bool TargetsAllEnemies => Card.TargetType == TargetType.AllEnemies;
    public int Remaining { get; internal set; } = remaining;
    public int DeployedRound { get; } = round;
    public int AttacksWhileWaiting { get; internal set; }
    public int PlayCount { get; internal set; } = play.PlayCount;
    internal async Task Resolve(PlayerChoiceContext context)
    {
        using var resolution = DamageExecution.BeginCardResolution(Card);
        await resolve(context, Play);
    }
    internal bool Resolving;
}

public static class ArrowCombat
{
    private static readonly List<ArrowDeployment> Arrows = [];
    private static readonly Dictionary<ulong, int> LastTick = [];
    public static IReadOnlyList<ArrowDeployment> Deployed => Arrows;
    public static ArrowDeployment[] DisplayOrder() => Arrows.OrderBy(a => a.TargetsAllEnemies ? float.NegativeInfinity
        : NCombatRoom.Instance?.GetCreatureNode(a.Play.Target)?.VfxSpawnPosition.X ?? float.PositiveInfinity).ToArray();
    public static ArrowDeployment? Find(CardModel card) => Arrows.FirstOrDefault(a => a.Card == card);
    public static void Cancel(CardModel card) => Arrows.RemoveAll(a => a.Card == card);

    public static void Deploy(JunnaFullDraw card, CardPlay play, int turns)
        => Deploy(card, play, turns, card.ResolveArrow);

    public static void Deploy(CardModel card, CardPlay play, int turns,
        Func<PlayerChoiceContext, CardPlay, Task> resolve)
    {
        if (Find(card) is { } existing && !existing.Resolving)
            existing.PlayCount += play.PlayCount;
        else
        {
            Cancel(card);
            Arrows.Add(new(card, play, turns, card.Owner.PlayerCombatState!.TurnNumber, resolve));
        }
        var room = NCombatRoom.Instance;
        if (room != null && room.GetNodeOrNull<ArrowHud>("JunnaArrows") == null)
            room.AddChild(new ArrowHud { Name = "JunnaArrows" });
    }

    internal static async Task Advance(PlayerChoiceContext context, Player player)
    {
        int round = player.PlayerCombatState!.TurnNumber;
        if (LastTick.TryGetValue(player.NetId, out var previous) && previous == round) return;
        LastTick[player.NetId] = round;
        foreach (var arrow in DisplayOrder().Where(a => a.Card.Owner == player && a.DeployedRound < round))
        {
            await Accelerate(context, arrow, 1);
        }
    }

    public static async Task AccelerateAll(PlayerChoiceContext context, Player player, int turns)
    {
        foreach (var arrow in DisplayOrder().Where(a => a.Card.Owner == player))
            await Accelerate(context, arrow, turns);
    }

    public static async Task Accelerate(PlayerChoiceContext context, ArrowDeployment arrow, int turns)
    {
        if (arrow.Resolving || !Arrows.Contains(arrow)) return;
        if (arrow.Card.Pile?.Type != ArrowPile.PileId) { Arrows.Remove(arrow); return; }
        arrow.Remaining = Math.Max(0, arrow.Remaining - Math.Max(0, turns));
        if (arrow.Remaining > 0) return;
        arrow.Resolving = true;
        context.PushModel(arrow.Card);
        try
        {
            for (int replay = 0; replay < arrow.PlayCount; replay++)
                await arrow.Resolve(context);
            // A resolving arrow may have deployed itself again. Keep that new deployment intact.
            Arrows.Remove(arrow);
            if (Find(arrow.Card) != null) return;
            if (arrow.Card.Pile?.Type != ArrowPile.PileId) return;
            if (arrow.Card.Keywords.Contains(CardKeyword.Exhaust)) await CardCmd.Exhaust(context, arrow.Card);
            else if (CatalogPower.Has(arrow.Card.Owner, "JA22")) await CardPileCmd.Add(arrow.Card, PileType.Hand);
            else await CardPileCmd.Add(arrow.Card, PileType.Discard);
        }
        finally { context.PopModel(arrow.Card); arrow.Resolving = false; }
    }

    internal static async Task AfterPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsLastInSeries && Find(play.Card) is { Remaining: 0 } ready) await Accelerate(context, ready, 0);
    }

    internal static void Clear() { Arrows.Clear(); LastTick.Clear(); }
}

[CustomID("JUNNA_COMBAT_HOOKS")]
public sealed class JunnaCombatHooks : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;
    public override Task BeforeCombatStart()
    {
        DamageSourceScopes.Install();
        ArrowCombat.Clear();
        DamageModifiers.ClearCombat();
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext context, Player player) => ArrowCombat.Advance(context, player);
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.IsFirstInSeries && (play.Card.Type == CardType.Attack || CardCatalog.IsArrow(play.Card)))
            foreach (var arrow in ArrowCombat.Deployed.Where(a => a.Card.Owner == play.Player && a.Card != play.Card && !a.Resolving))
                arrow.AttacksWhileWaiting++;
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play) => ArrowCombat.AfterPlayed(context, play);
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        bool free = CardCatalog.DesignId(card) == "JK45B" && ArrowCombat.Deployed.Any(a => a.Card.Owner == card.Owner && CardCatalog.DesignId(a.Card) == "JK45");
        modifiedCost = free ? 0 : originalCost;
        return free;
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        ArrowCombat.Clear();
        DamageModifiers.ClearCombat();
        return Task.CompletedTask;
    }
}
