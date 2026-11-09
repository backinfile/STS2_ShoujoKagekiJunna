using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class PlaytestFixProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_playtest_fixes";
    public override string Args => "check";
    public override string Description => "Check pending damage, paid replay targets, generated previews and corrected card metadata.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Check(player), true, "Playtest regression checks started");
    private static async Task Check(Player player)
    {
        string report = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/playtest-fixes-20261107/results.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        var enemies = player.Creature.CombatState!.Enemies.ToArray();
        var target = enemies[0];
        void Save() => File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        void Expect(string name, object? actual, object? expected)
        { rows.Add(new { name, actual, expected, pass = Equals(actual, expected) }); Save(); }
        async Task Reset()
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
            player.PlayerCombatState!.Energy = 9;
            foreach (var enemy in enemies.Where(enemy => enemy.IsAlive))
            {
                await CreatureCmd.SetMaxHp(enemy, 10000);
                await CreatureCmd.SetCurrentHp(enemy, 10000);
                await TestGameApi.LoseBlock(context, enemy, enemy.Block, null);
            }
        }
        async Task<CardModel> Card(string id)
        {
            var card = CardCatalog.Create(id, player);
            using var single = KnowledgeCombat.SingleCard();
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            return card;
        }
        async Task Case(string name, Func<Task> run)
        {
            try { await Reset(); await run(); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); Save(); }
        }
        await Case("pending damage", async () =>
        {
            var strike = await Card("JB01");
            await PowerCmd.Apply<JunnaNextCardBonus>(context, player.Creature, 2, player.Creature, null);
            strike.UpdateDynamicVarPreview(CardPreviewMode.Normal, target, strike.DynamicVars);
            Expect("pending bonus enters targeted preview", strike.DynamicVars.Damage.PreviewValue, 8m);
            strike.DynamicVars.Damage.UpdateCardPreview(strike, CardPreviewMode.Normal, null, false);
            Expect("pending bonus enters non-global preview", strike.DynamicVars.Damage.PreviewValue, 8m);
            DamageModifiers.Add(strike, 3);
            strike.UpdateDynamicVarPreview(CardPreviewMode.Normal, target, strike.DynamicVars);
            Expect("pending and stored bonuses combine in preview", strike.DynamicVars.Damage.PreviewValue, 11m);
            int hp = target.CurrentHp;
            await CardActions.PaidPlay(context, strike, target);
            Expect("actual damage counts each bonus once", hp - target.CurrentHp, 11);
            Expect("playing consumes pending bonus", player.Creature.GetPower<JunnaNextCardBonus>() == null, true);
        });
        await Case("generated meteor tips", async () =>
        {
            foreach (string id in new[] { "JA76", "JA81" })
            {
                var card = await Card(id);
                foreach (bool upgraded in new[] { false, true })
                {
                    if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
                    var tips = card.HoverTips.ToArray();
                    var meteor = tips.OfType<CardHoverTip>().Select(tip => tip.Card).FirstOrDefault(preview => CardCatalog.DesignId(preview) == "JA81B");
                    Expect(id + " meteor card preview upgraded=" + upgraded, meteor != null, true);
                    if (meteor != null) Expect(id + " meteor countdown upgraded=" + upgraded, HandArrow.Turns(meteor), 2);
                    Expect(id + " arrow keyword tooltip upgraded=" + upgraded,
                        tips.OfType<HoverTip>().Any(tip => tip.Id?.Contains("ARROW", StringComparison.OrdinalIgnoreCase) == true), true);
                }
            }
        });
        await Case("self-target and star countdown", async () =>
        {
            var school = await Card("JK82");
            Expect("learning targets self", school.TargetType, TargetType.Self);
            if (school.TargetType == TargetType.Self)
            {
                player.PlayerCombatState!.Energy = 4;
                await CardActions.PaidPlay(context, school);
                Expect("learning grants energy without enemy selection", player.PlayerCombatState.Energy, 6);
            }
            var star = await Card("JA57");
            Expect("starlight jump base countdown", HandArrow.Turns(star), 3);
            star.UpgradeInternal(); star.FinalizeUpgradeInternal();
            Expect("starlight jump upgraded countdown", HandArrow.Turns(star), 3);
            await CardActions.PaidPlay(context, star);
            Expect("starlight jump deploys with three turns", ArrowCombat.Find(star)?.Remaining, 3);
            await CardActions.PaidPlay(context, await Card("JA105"));
            var permanent = player.RunState.CreateCard(CardCatalog.Model("JA57"), player);
            await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
            string Counter(CardModel model, PileType pile)
            {
                var node = NCard.Create(model)!;
                NGame.Instance!.AddChild(node);
                node.UpdateVisuals(pile, CardPreviewMode.Normal);
                string value = node.Body.GetNode<Label>("JunnaArrowCard/Turns").Text;
                NGame.Instance.RemoveChild(node);
                node.QueueFree();
                return value;
            }
            Expect("combat haste leaves the permanent deck counter unchanged", Counter(permanent, PileType.Deck), "3");
            var fresh = await Card("JA57");
            Expect("combat hand preview still applies haste", Counter(fresh, PileType.Hand), "2");
            Expect("combat haste preserves the printed turn variable", HandArrow.Turns(fresh), 3);
        });
        await Case("updated formation acceleration", async () =>
        {
            var arrow = await Card("JB03");
            var play = new CardPlay { Card = arrow,
#if STS2_BETA
            Player = player,
#endif
 Target = target, ResultPile = PileType.Discard,
                Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            foreach (bool upgraded in new[] { false, true })
            {
                ArrowCombat.Cancel(arrow);
                ArrowCombat.Deploy(arrow, play, 5, (_, _) => Task.CompletedTask);
                if (arrow.Pile?.Type != ArrowPile.PileId) await CardPileCmd.Add(arrow, ArrowPile.PileId);
                var formation = await Card("JA71");
                if (upgraded) { formation.UpgradeInternal(); formation.FinalizeUpgradeInternal(); }
                await CardActions.PaidPlay(context, formation);
                Expect("formation accelerates its printed turns upgraded=" + upgraded, ArrowCombat.Find(arrow)?.Remaining, upgraded ? 1 : 2);
            }
        });
        await Case("paid replay switches dead targets", async () =>
        {
            foreach (var enemy in enemies.Skip(2).Where(enemy => enemy.IsAlive)) await CreatureCmd.Kill(enemy);
            var practice = await Card("JG36");
            int damage = (int)practice.DynamicVars.Damage.BaseValue;
            await CreatureCmd.SetCurrentHp(target, damage);
            await CreatureCmd.SetCurrentHp(enemies[1], 100);
            player.PlayerCombatState!.Energy = 3;
            await CardActions.PaidPlay(context, await Card("JH19"));
            await CardActions.PaidPlay(context, practice, target);
            Expect("first payload kills its original target", target.IsAlive, false);
            Expect("paid replay hits the remaining enemy exactly once", enemies[1].CurrentHp, 100 - damage);
            Expect("explicit paid replay keeps its energy cost", player.PlayerCombatState.Energy, 0);
            Expect("replayed practice retains both non-damage effects", player.Creature.GetPower<JunnaNextCardBonus>()?.Amount, 4);
        });
        rows.Add(new { name = "checks complete", pass = true });
        Save();
    }
}
