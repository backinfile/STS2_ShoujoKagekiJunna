using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers.Mocks;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class MeteorPileProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_meteor_piles";
    public override string Args => "check";
    public override string Description => "Check generated Meteors never leak into draw or hand when their target disappears.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Meteor pile checks started");

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Verify(string name, bool pass, object? actual) => rows.Add(new { name, pass, actual });
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var enemies = state.Enemies.Where(e => e.IsAlive).ToArray();

        async Task Clear()
        {
            foreach (var card in player.PlayerCombatState!.AllCards.ToArray())
            {
                ArrowCombat.Cancel(card);
                await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            }
        }
        try
        {
            foreach (var enemy in enemies) { enemy.SetMaxHpInternal(100000); enemy.SetCurrentHpInternal(100000); }
            await Clear();
            await CardActions.Fly(context, player, 7, enemies[0]);
            var deployed = ArrowCombat.Deployed.Single(a => a.Card.Owner == player);
            Verify("live target: Meteor deploys for two turns", deployed.Remaining == 2, deployed.Remaining);
            await ArrowCombat.Accelerate(context, deployed, 2);
            Verify("live target: completed Meteor exhausts", deployed.Card.Pile?.Type == PileType.Exhaust, deployed.Card.Pile?.Type.ToString());
            await Clear();
            if (enemies.Length > 1)
            {
                await PowerCmd.Apply<MockUnhittablePower>(context, enemies[0], 1, player.Creature, null);
                try
                {
                    await CardActions.Fly(context, player, 9, enemies[0]);
                    var redirected = ArrowCombat.Deployed.Single(a => a.Card.Owner == player);
                    Verify("unavailable original: Meteor retargets a living enemy", redirected.Target == enemies[1], redirected.Target?.IsHittable);
                    await ArrowCombat.Accelerate(context, redirected, 2);
                    Verify("retargeted Meteor exhausts after resolving", redirected.Card.Pile?.Type == PileType.Exhaust, redirected.Card.Pile?.Type.ToString());
                }
                finally { await PowerCmd.Remove(enemies[0].GetPower<MockUnhittablePower>()!); }
                await Clear();
            }
            // Native test power reproduces a living enemy that temporarily cannot be hit.
            try
            {
                foreach (var enemy in enemies) await PowerCmd.Apply<MockUnhittablePower>(context, enemy, 1, player.Creature, null);
                await CardActions.Fly(context, player, 7, enemies[0]);
                await CardActions.Fly(context, player, 7, enemies[0]);
            }
            finally
            {
                foreach (var enemy in enemies) if (enemy.GetPower<MockUnhittablePower>() is { } power) await PowerCmd.Remove(power);
            }
            var meteors = player.PlayerCombatState!.AllCards.Where(c => CardCatalog.DesignId(c) == "JA81B").ToArray();
            Verify("missing target: both generated Meteors exhaust", meteors.Length == 2 && meteors.All(c => c.Pile?.Type == PileType.Exhaust),
                meteors.Select(c => c.Pile?.Type.ToString()).ToArray());
            await CardPileCmd.Draw(context, 1, player);
            Verify("reshuffle: no Meteor enters draw pile", !player.PlayerCombatState.DrawPile.Cards.Any(c => CardCatalog.DesignId(c) == "JA81B"),
                player.PlayerCombatState.DrawPile.Cards.Select(c => c.Id.Entry).ToArray());
            Verify("draw: no Meteor enters hand", !player.PlayerCombatState.Hand.Cards.Any(c => CardCatalog.DesignId(c) == "JA81B"),
                player.PlayerCombatState.Hand.Cards.Select(c => c.Id.Entry).ToArray());
        }
        catch (Exception error) { rows.Add(new { name = "exception", pass = false, actual = error.ToString() }); }
        string path = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../../out/tmp/meteor-pile/results.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
