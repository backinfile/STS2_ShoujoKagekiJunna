using System.Text.Json;
using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

[ModInitializer(nameof(Initialize))]
public static class ProbeStartup
{
    public static void Initialize()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        Godot.RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), Godot.RenderingServer.ViewportUpdateMode.Always);
        tree.ProcessFrame += () => Godot.RenderingServer.ForceDraw(false);
    }
}

// Deliberately lives in another assembly and is a Skill without DamageVar.
[CustomID("JUNNA_PROBE_DIRECT")]
[Pool(typeof(JunnaCardPool))]
public sealed class ExternalDirectCard() : CustomCardModel(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    public override string CustomPortraitPath => "res://ShoujoKagekiJunna/images/cards/JB01.jpg";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.Damage(context, [play.Target!], 4, ValueProp.Move, Owner.Creature, this, play);
        await CreatureCmd.GainBlock(Owner.Creature, 7, ValueProp.Move, play);
    }
}

public sealed class JunnaProbeCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_probe";
    public override string Args => "run | invest <hand-index> <bonus> <hits> | prepare";
    public override string Description => "Scoped Junna integration checks in the isolated test game.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (args.FirstOrDefault() == "render")
        {
            return new(true, "Offscreen rendering enabled; window stays minimized");
        }
        if (args.FirstOrDefault() == "portrait")
        {
            var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
            var icons = tree.Root.FindChildren("*", "TextureRect", true, false).OfType<Godot.TextureRect>()
                .Where(t => t.GetPath().ToString().Contains("TopBar"))
                .Select(t => new { path = t.GetPath().ToString(), texture = t.Texture?.ResourcePath,
                    type = t.Texture?.GetType().Name, size = t.Size.ToString(), region = (t.Texture as Godot.AtlasTexture)?.Region.ToString() });
            return new(true, JsonSerializer.Serialize(icons));
        }
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "catalog") return new(CardPoolProbe.Run(player), true, "Card pool checks started");
        if (args.FirstOrDefault() == "catalog-all") return new(CardCompletionProbe.Run(player, only: args.Length > 1 ? string.Join(" ", args.Skip(1)) : null), true, "All card faces started");
        if (args.FirstOrDefault() == "catalog-edges") return new(CardCompletionProbe.Run(player, true, args.Length > 1 ? string.Join(" ", args.Skip(1)) : null), true, "Card edge cases started");
        if (args.FirstOrDefault() == "catalog-events") return new(CardPoolProbe.Run(player, true), true, "Card event checks started");
        if (args.FirstOrDefault() == "catalog-targeted") return new(CardPoolProbe.Run(player, true, "converted clones"), true, "Converted clone checks started");
        if (args.FirstOrDefault() == "arrows") return ArrowHudProbe.Process(player, args.Skip(1).ToArray());
        if (args.FirstOrDefault() == "duplicates") return new(CheckDuplicateTargets(player), true, "Duplicate target check started");
        if (args.FirstOrDefault() == "invest")
        {
            var card = player.PlayerCombatState.Hand.Cards[int.Parse(args[1])];
            return new(DamageModifiers.Add(card, decimal.Parse(args[2]), int.Parse(args[3])), card.Id.Entry);
        }
        return new(args.FirstOrDefault() == "prepare" ? Prepare(player) : Run(player, args.FirstOrDefault() == "real"), true, "Probe started");
    }

    private static async Task CheckDuplicateTargets(Player player)
    {
        var state = player.Creature.CombatState!;
        var target = state.GetOpponentsOf(player.Creature).First();
        var card = state.CreateCard(ModelDb.Card<JunnaStrike>(), player);
        int before = target.CurrentHp;
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), [target, target], 2,
            ValueProp.Unpowered, player.Creature, card, null);
        int actual = before - target.CurrentHp;
        System.IO.File.WriteAllText("D:/Github/STS2_ShoujoKagekiJunna/artifacts/dev-game/duplicate-targets.json",
            JsonSerializer.Serialize(new { name = "original repeated target entries preserved", actual, expected = 4, pass = actual == 4 }));
    }


    private static async Task Prepare(Player player)
    {
        foreach (var enemy in player.Creature.CombatState!.GetOpponentsOf(player.Creature))
        {
            await CreatureCmd.SetMaxHp(enemy, 10000);
            await CreatureCmd.SetCurrentHp(enemy, 10000);
        }
        await CreatureCmd.SetMaxHp(player.Creature, 1000);
        await CreatureCmd.SetCurrentHp(player.Creature, 1000);
    }

    private static async Task Run(Player player, bool realModOnly)
    {
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var target = state.GetOpponentsOf(player.Creature).First();
        T Card<T>() where T : CardModel => (T)state.CreateCard(ModelDb.Card<T>(), player);
        void Check(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        async Task Play(CardModel card)
        {
            var play = new CardPlay { Card = card, Player = player, Target = target, ResultPile = PileType.Discard,
                Resources = default, IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
            context.PushModel(card);
            try { await (Task)AccessTools.Method(card.GetType(), "OnPlay").Invoke(card, [context, play])!; }
            finally { context.PopModel(card); }
        }
        async Task DamageCheck(string name, CardModel card, int expected)
        {
            int before = target.CurrentHp;
            await Play(card);
            Check(name, before - target.CurrentHp, expected);
        }
        try
        {
            await Prepare(player);
            if (realModOnly)
            {
                var canonical = ModelDb.AllCards.Single(c => c.Id.Entry == "KAREN_STRIKE");
                var karen = state.CreateCard(canonical, player);
                Check("Karen card comes from real mod", karen.GetType().Assembly.GetName().Name!, "ShoujoKagekiAijoKaren.Beta");
                Check("Karen strike eligible", DamageModifiers.CanDealDamage(karen), true);
                var baseline = karen.DynamicVars.Damage.IntValue;
                Check("Karen strike accepts investment", DamageModifiers.Add(karen, 2, 1), true);
                await DamageCheck("real Karen strike gains bonus and one hit", karen, (baseline + 2) * 2);
            }
            else
            {
            var defend = Card<JunnaDefend>();
            Check("block-only ineligible", DamageModifiers.CanDealDamage(defend), false);
            Check("poison-only skill ineligible", DamageModifiers.CanDealDamage(Card<DeadlyPoison>()), false);
            Check("orb-only skill ineligible", DamageModifiers.CanDealDamage(Card<Zap>()), false);
            var direct = Card<ExternalDirectCard>();
            Check("external skill without DamageVar eligible", DamageModifiers.CanDealDamage(direct), true);
            DamageModifiers.Add(direct, 2, 1);
            int blockBefore = player.Creature.Block;
            await DamageCheck("external direct damage (4+2)*2", direct, 12);
            Check("external block effect runs once", player.Creature.Block - blockBefore, 7);

            var twin = Card<TwinStrike>();
            DamageModifiers.Add(twin, 2, 1);
            await DamageCheck("vanilla two-hit attack gains one hit", twin, 21);
            var turning = Card<JunnaTurningSlash>();
            DamageModifiers.Add(turning, 2, 1);
            await DamageCheck("Junna three-hit attack gains one hit", turning, 44);

            var guide = Card<JunnaGuidingStar>();
            DamageModifiers.Add(guide, 2, 1);
            int energyBefore = player.Creature.GetPowerAmount<EnergyNextTurnPower>();
            await DamageCheck("guiding star (9+2)*2", guide, 22);
            Check("next turn energy applied once", player.Creature.GetPowerAmount<EnergyNextTurnPower>() - energyBefore, 1);

            int callbackCount = 0, hpBefore = target.CurrentHp;
            await DamageCmd.Attack(4).FromCard(direct, null).Targeting(target).WithHitCount(2)
                .BeforeDamage(() => { callbackCount++; return Task.CompletedTask; }).Execute(context);
            Check("external builder extra hit", hpBefore - target.CurrentHp, 18);
            Check("before-damage callback keeps original count", callbackCount, 2);

            var poisonCard = Card<DeadlyPoison>();
            Check("poison-only skill rejects investment", DamageModifiers.Add(poisonCard, 2, 1), false);
            var poison = await PowerCmd.Apply<PoisonPower>(context, target, 5, player.Creature, direct);
            hpBefore = target.CurrentHp;
            await poison!.AfterSideTurnStart(target.Side, [target], state);
            Check("poison from strengthened card keeps own damage", hpBefore - target.CurrentHp, 5);
            Check("poison decrements once", poison.Amount, 4);
            await PowerCmd.Apply<PoisonPower>(context, target, 5, player.Creature, direct);
            hpBefore = target.CurrentHp;
            await poison.AfterSideTurnStart(target.Side, [target], state);
            Check("stacked poison follows its own amount", hpBefore - target.CurrentHp, 9);
            await PowerCmd.Remove(poison);

            var strike = Card<JunnaStrike>();
            DamageModifiers.Add(strike, 2, 1);
            await PowerCmd.Apply<StrengthPower>(context, player.Creature, 2, player.Creature, null);
            await PowerCmd.Apply<VulnerablePower>(context, target, 1, player.Creature, null);
            await DamageCheck("strength and vulnerability use native order", strike, 30);
            await PowerCmd.Remove(player.Creature.GetPower<StrengthPower>()!);
            await PowerCmd.Remove(target.GetPower<VulnerablePower>()!);
            var clone = (CardModel)strike.MutableClone();
            Check("copy inherits investment", DamageModifiers.CaptureDamageModifierSnapshot(clone).DamageBonus, 2m);
            DamageModifiers.Add(clone, 3);
            Check("copy investment remains independent", DamageModifiers.CaptureDamageModifierSnapshot(strike).DamageBonus, 2m);
            Check("fresh card has no inherited investment", DamageModifiers.CaptureDamageModifierSnapshot(Card<JunnaStrike>()).DamageBonus, 0m);
            int playerBefore = player.Creature.CurrentHp;
            await CreatureCmd.Damage(context, [player.Creature], 4, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature, strike, null);
            Check("self damage unchanged", playerBefore - player.Creature.CurrentHp, 4);
            hpBefore = target.CurrentHp;
            using (DamageExecution.UseDamageSource(ModelDb.Relic<Anchor>()))
                await CreatureCmd.Damage(context, [target], 4, ValueProp.Unpowered, player.Creature, null, null);
            Check("unbound independent effect unchanged", hpBefore - target.CurrentHp, 4);

            var endurance = await PowerCmd.Apply<JunnaEndurancePower>(context, player.Creature, 2, player.Creature, null);
            var trained = Card<JunnaStrike>();
            DamageModifiers.Add(trained, 12, 1);
            await DamageCheck("endurance adds floor(bonus/6)*stacks hits", trained, 108);
            await PowerCmd.Remove(endurance!);
            }
        }
        catch (Exception ex) { rows.Add(new { name = "unhandled", pass = false, error = ex.ToString() }); }
        System.IO.File.WriteAllText("D:/Github/STS2_ShoujoKagekiJunna/artifacts/dev-game/" + (realModOnly ? "karen-compatibility.json" : "integration-results.json"),
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
