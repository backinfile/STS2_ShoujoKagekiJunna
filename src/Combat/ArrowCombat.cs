using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using BaseLib.Utils.Attributes;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public sealed class ArrowPile() : CustomPile(PileId)
{
    [CustomEnum] public static PileType PileId;
    [CustomEnum] public static CardType ArrowType;
    public override bool CardShouldBeVisible(CardModel card) => false;
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

    public static void Deploy(JunnaFullDraw card, CardPlay play, int turns)
        => Deploy(card, play, turns, card.ResolveArrow);

    public static void Deploy(CardModel card, CardPlay play, int turns,
        Func<PlayerChoiceContext, CardPlay, Task> resolve)
    {
        Arrows.Add(new(card, play, turns, card.CombatState!.RoundNumber, resolve));
        var room = NCombatRoom.Instance;
        if (room != null && room.GetNodeOrNull<ArrowHud>("JunnaArrows") == null)
            room.AddChild(new ArrowHud { Name = "JunnaArrows" });
    }

    internal static async Task Advance(PlayerChoiceContext context, Player player)
    {
        int round = player.Creature.CombatState!.RoundNumber;
        if (LastTick.TryGetValue(player.NetId, out var previous) && previous == round) return;
        LastTick[player.NetId] = round;
        foreach (var arrow in Arrows.Where(a => a.Card.Owner == player && a.DeployedRound < round).ToArray())
        {
            if (arrow.Resolving || !Arrows.Contains(arrow)) continue;
            if (arrow.Card.Pile?.Type != ArrowPile.PileId) { Arrows.Remove(arrow); continue; }
            arrow.Remaining = Math.Max(0, arrow.Remaining - 1);
            if (arrow.Remaining > 0) continue;
            arrow.Resolving = true;
            context.PushModel(arrow.Card);
            try
            {
                for (int replay = 0; replay < arrow.Play.PlayCount; replay++)
                    await arrow.Resolve(context);
                Arrows.Remove(arrow);
                var destination = arrow.Card.Keywords.Contains(CardKeyword.Exhaust) ? PileType.Exhaust : PileType.Discard;
                await CardPileCmd.Add(arrow.Card, destination);
            }
            finally { context.PopModel(arrow.Card); arrow.Resolving = false; }
        }
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
    public override Task AfterCombatEnd(CombatRoom room)
    {
        ArrowCombat.Clear();
        DamageModifiers.ClearCombat();
        return Task.CompletedTask;
    }
}
