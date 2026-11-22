using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowPlayCountProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_arrow_play_count";
    public override string Args => "check | prepare";
    public override string Description => "Check arrow completion against the Ceremonial Beast's Ringing limit.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required")
        : new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Check(player), true, "Arrow play-count probe started");

    private static async Task Prepare(Player player)
    {
        var enemy = player.Creature.CombatState!.Enemies.Single();
        await Stage(player, "JB03", enemy);
        await Stage(player, "U_CARD_025", player.Creature);
        if (enemy.Monster is CeremonialBeast beast)
            beast.SetMoveImmediate(beast.BeastCryState, forceTransition: true);
    }

    private static async Task Check(Player player)
    {
        var context = new ThrowingPlayerChoiceContext();
        var combat = player.Creature.CombatState!;
        var enemy = combat.Enemies.Single();
        var plain = await Stage(player, "JB03", enemy);
        var loop = await Stage(player, "U_CARD_025", player.Creature);
        var first = CardCatalog.Create("JB02", player);
        var second = CardCatalog.Create("JB02", player);
        await CardPileCmd.AddGeneratedCardToCombat(first, PileType.Hand, player);
        await CardPileCmd.AddGeneratedCardToCombat(second, PileType.Hand, player);
        await PowerCmd.Apply<RingingPower>(context, player.Creature, 1, enemy, null);
        var ringing = player.Creature.GetPower<RingingPower>()!;

        int PlayCount() => CombatManager.Instance.History.CardPlaysStarted
            .Count(entry => entry.Actor == player.Creature && entry.HappenedThisTurn(combat));

        int before = PlayCount();
        int hp = enemy.CurrentHp;
        await ArrowCombat.Accelerate(context, ArrowCombat.Find(plain)!, 1);
        int plainPlays = PlayCount() - before;
        bool damage = enemy.CurrentHp < hp;
        int energy = player.PlayerCombatState!.Energy;
        await ArrowCombat.Accelerate(context, ArrowCombat.Find(loop)!, 1);
        int loopPlays = PlayCount() - before - plainPlays;
        bool renewed = ArrowCombat.Find(loop) is { Remaining: > 0 } && player.PlayerCombatState.Energy > energy;
        bool firstAllowed = ringing.ShouldPlay(first, AutoPlayType.Default);
        bool played = firstAllowed && await TestGameApi.PaidPlay(context, first);
        bool secondBlocked = played && PlayCount() == before + 1 && !ringing.ShouldPlay(second, AutoPlayType.Default);
        bool passed = before == 0 && plainPlays == 0 && damage && loopPlays == 0 && renewed && firstAllowed && secondBlocked;
        Godot.GD.Print($"[JunnaArrowPlayCount] pass={passed} prior={before} plain={plainPlays} damage={damage} loop={loopPlays} renewed={renewed} firstAllowed={firstAllowed} secondBlocked={secondBlocked}");
    }

    private static async Task<CardModel> Stage(Player player, string id, Creature target)
    {
        var card = CardCatalog.Create(id, player);
        var play = new CardPlay
        {
            Card = card,
#if STS2_BETA
            Player = player,
#endif
            Target = target,
            ResultPile = PileType.Discard,
            Resources = default,
            IsAutoPlay = true,
            PlayIndex = 0,
            PlayCount = 1
        };
        var effect = CoreApi.Get<ICardPayloadRouting>().Native.Bind(card);
        ArrowCombat.Deploy(card, play, 1, effect.Invoke);
        await CardPileCmd.AddGeneratedCardToCombat(card, ArrowPile.PileId, player);
        return card;
    }
}
