using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class ArrowTestSubjectProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_test_subject";
    public override string Args => "check [single|group|support|pending|conversion|stardust|meteor|stardust-chain|large-prop] | prepare | inspect";
    public override string Description => "Check arrow completion while Test Subject waits to revive.";
    public override bool IsNetworked => false;
    private static readonly List<CardModel> Cards = [];
    private static readonly List<object> Checks = [];
    private static bool _passed;
    private static string Output => Path.GetFullPath("../../../out/tmp/experiment-death-zero-arrows");

    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "inspect") { Save(player); return new(true, "Saved"); }
        return new(Run(player, args.Contains("prepare"), args.ElementAtOrDefault(1) ?? "single"), true, "Started");
    }

    private static void Verify(string name, bool pass)
    {
        Checks.Add(new { name, pass });
        _passed &= pass;
    }

    private static void Save(Player player, string? error = null)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "results.json"), JsonSerializer.Serialize(new
        {
            completed = true, error, checks = Checks,
            enemies = player.Creature.CombatState?.Enemies.Select(e => new { id = e.Monster?.Id.Entry, e.CurrentHp, e.IsAlive, e.IsHittable }),
            cards = Cards.Select(c => new { id = CardCatalog.DesignId(c) ?? c.Id.Entry, pile = c.Pile?.Type.ToString(), remaining = ArrowCombat.Find(c)?.Remaining }),
            deployed = ArrowCombat.Deployed.Select(a => new { id = CardCatalog.DesignId(a.Card) ?? a.Card.Id.Entry, a.Remaining, target = a.Target?.Monster?.Id.Entry }),
            pass = error == null && _passed && !ArrowCombat.Deployed.Any(a => a.Remaining == 0)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<CardModel> AddInkyShiv(Player player)
    {
        var shiv = player.Creature.CombatState!.CreateCard(ModelDb.Card<Shiv>(), player);
        CardCmd.Enchant<Inky>(shiv, 1m);
        await CardPileCmd.AddGeneratedCardToCombat(shiv, PileType.Hand, player);
        return shiv;
    }

    private static async Task Run(Player player, bool prepare, string scenario)
    {
        try
        {
            var context = new ThrowingPlayerChoiceContext();
            var target = player.Creature.CombatState!.Enemies.Single(e => e.Monster?.Id.Entry == "TEST_SUBJECT");
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: !prepare); }
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            player.PlayerCombatState!.Energy = 20;
            Cards.Clear(); Checks.Clear(); _passed = true;
            using var single = KnowledgeCombat.SingleCard();
            async Task<CardModel> Play(string id)
            {
                var card = CardCatalog.Create(id, player);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
                await TestGameApi.PaidPlay(context, card, card.TargetType == TargetType.AnyEnemy ? target : null);
                return card;
            }
            if (scenario is "stardust-chain" or "large-prop")
            {
                bool chain = scenario == "stardust-chain";
                await CreatureCmd.SetMaxHp(target, 200);
                await CreatureCmd.SetCurrentHp(target, 200);
                await Play("JA105");
                if (chain) await Play("JA105");
                var card = CardCatalog.Create(chain ? "U_CARD_005" : "U_CARD_031", player);
                Cards.Add(card);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
                if (prepare)
                {
                    SavePreparation();
                    return;
                }
                var block = player.Creature.Block;
                await TestGameApi.PaidPlay(context, card, chain ? target : null);
                if (chain)
                {
                    Verify("eight Stardust resolve in one chain", target.CurrentHp == 40 &&
                        player.PlayerCombatState.ExhaustPile.Cards.Count(c => CardCatalog.DesignId(c) == "U_CARD_005") == 8);
                    Verify("the nine surviving copies stop at one",
                        ArrowCombat.Deployed.Count == 9 && ArrowCombat.Deployed.All(a => a.Remaining == 1 &&
                            CardCatalog.DesignId(a.Card) == "U_CARD_005"));
                }
                else
                {
                    Verify("Big Prop resolves once and exhausts under Accelerate", card.Pile?.Type == PileType.Exhaust &&
                        ArrowCombat.Find(card) == null && player.Creature.Block == block + 3);
                }
                Save(player);
                return;
            }
            bool stardust = scenario == "stardust";
            Cards.Add(await Play(stardust ? "U_CARD_005" : "JB03"));
            if (!stardust && scenario != "meteor")
                Cards.Add(await Play(scenario == "group" ? "JA77" : scenario == "support" ? "JA80" : "JB03"));
            await CreatureCmd.SetCurrentHp(target, 1);
            if (prepare)
            {
                await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JA71", player), PileType.Hand, player);
                if (scenario == "conversion")
                {
                    Cards.Add(await AddInkyShiv(player));
                    await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create("JA87", player), PileType.Hand, player);
                    await PowerCmd.Apply<BufferPower>(context, player.Creature, 12m, player.Creature, null);
                }
                SavePreparation();
                return;
            }
            if (scenario is "group" or "support") await Play("JB01");
            int energy = player.PlayerCombatState.Energy;
            if (stardust)
            {
                await Play("JA71");
                var children = ArrowCombat.Deployed.ToArray();
                Verify("the original Stardust exhausts", Cards[0].Pile?.Type == PileType.Exhaust);
                Verify("two free Stardust copies deploy against the reviving target",
                    children.Length == 2 && children.All(a => CardCatalog.DesignId(a.Card) == "U_CARD_005" &&
                        a.Remaining == 3 && a.Target == target) && player.PlayerCombatState.Energy == energy - 3);
            }
            else if (scenario == "meteor")
            {
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(Cards[0])!, 99);
                await CardActions.Fly(context, player, 5, target);
                await CardActions.Fly(context, player, 7);
                var meteors = ArrowCombat.Deployed.ToArray();
                Verify("both aimed and random Meteors deploy during knockout", meteors.Length == 2 &&
                    meteors.All(a => a.Remaining == 2 && a.Target == target && CardCatalog.DesignId(a.Card) == "JA81B"));
                foreach (var meteor in meteors)
                {
                    Cards.Add(meteor.Card);
                    await ArrowCombat.Accelerate(context, meteor, 1);
                }
                Verify("the Meteors remain deployed at one",
                    meteors.Length == 2 && meteors.All(a => ArrowCombat.Find(a.Card) == a && a.Remaining == 1));
                foreach (var meteor in meteors) await ArrowCombat.Accelerate(context, meteor, 1);
                Verify("the Meteors exhaust at zero if no enemy has revived",
                    meteors.Length == 2 && meteors.All(a => ArrowCombat.Find(a.Card) == null && a.Card.Pile?.Type == PileType.Exhaust));
            }
            else if (scenario == "conversion")
            {
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(Cards[0])!, 99);
                var shiv = await AddInkyShiv(player);
                Cards.Add(shiv);
                await HandArrow.Play(context, shiv);
                var deployed = ArrowCombat.Find(shiv);
                Verify("enchanted single-target material deploys for three turns during knockout",
                    deployed is { Remaining: 3 } && shiv.Pile?.Type == ArrowPile.PileId &&
                    shiv.Type == ArrowPile.ArrowType && shiv.Enchantment is Inky);
                Verify("new deployment tracks the reviving enemy",
                    deployed?.Target == target);
                if (deployed != null) await ArrowCombat.Accelerate(context, deployed, 3);
                Verify("if the enemy has not revived at zero the shiv exhausts without applying Weak",
                    ArrowCombat.Find(shiv) == null && shiv.Pile?.Type == PileType.Exhaust &&
                    shiv.Type == CardType.Attack && target.GetPower<WeakPower>() == null);
            }
            else if (scenario == "pending")
            {
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(Cards[0])!, 99);
                await ArrowCombat.Accelerate(context, ArrowCombat.Find(Cards[1])!, 1);
                Verify("unfinished arrow keeps its original reviving target and countdown",
                    ArrowCombat.Find(Cards[1]) is { Remaining: 2 } pending && pending.Target == target && Cards[1].Pile?.Type == ArrowPile.PileId);
            }
            else
            {
                await ArrowCombat.AccelerateAll(context, player, 99);
                Verify("all ready arrows leave deployment and reach discard",
                    Cards.All(c => ArrowCombat.Find(c) == null && c.Pile?.Type == PileType.Discard));
            }
            Verify("native knockout keeps combat running with no hittable target",
                !target.IsAlive && !target.IsHittable && target.CombatState != null && !CombatManager.Instance.IsOverOrEnding);
            if (scenario == "support") Verify("self-target arrow still grants its energy during knockout", player.PlayerCombatState.Energy == energy + 2);
            await Task.Delay(500);
            Save(player);
        }
        catch (Exception error) { Save(player, error.ToString()); }
    }

    private static void SavePreparation()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "prepared.json"), "{}");
    }
}
