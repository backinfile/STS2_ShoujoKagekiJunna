using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaCompatibilityProbe;

public sealed class DescriptionAnnotationsCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_annotations";
    public override string Args => "check | prepare [JZ05|JK260]";
    public override string Description => "Check conditional card notes and remaining power triggers.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => player?.PlayerCombatState == null
        ? new(false, "Combat required") : new(Run(player, args.Contains("prepare"), args.Skip(1).FirstOrDefault()), true, "Annotation checks started");

    private static async Task Run(Player player, bool prepare, string? hoverPower)
    {
        string directory = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/card-annotations-20261110"));
        Directory.CreateDirectory(directory);
        var checks = new List<object>();
        void Save() => File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        void Check(string name, bool pass) { checks.Add(new { name, pass }); Save(); }
        var context = new ThrowingPlayerChoiceContext();
        var target = player.Creature.CombatState!.HittableEnemies.First();
        var turn = typeof(PlayerCombatState).GetProperty("TurnNumber", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        void NextTurn() => turn.SetValue(player.PlayerCombatState, player.PlayerCombatState!.TurnNumber + 1);
        async Task Clear()
        {
            foreach (var node in NCombatRoom.Instance!.FindChildren("*", "", true, false).OfType<NPower>())
                node.EmitSignal(Control.SignalName.MouseExited);
            foreach (var creature in new[] { player.Creature, target })
                foreach (var power in creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card); }
            await CreatureCmd.SetMaxHp(target, 100000);
            await CreatureCmd.SetCurrentHp(target, 100000);
            player.PlayerCombatState!.Energy = 100;
        }
        async Task<CardModel> Add(string id, bool upgraded = false)
        {
            using var single = KnowledgeCombat.SingleCard();
            var card = CardCatalog.Create(id, player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: !prepare);
            return card;
        }
        Task<bool> Play(CardModel card) => CardActions.PaidPlay(context, card, target);
        string Describe(CardModel card, PileType pile = PileType.Hand)
        {
            var previewTarget = pile.IsCombatPile() && card.TargetType == TargetType.AnyEnemy ? target : null;
            card.UpdateDynamicVarPreview(CardPreviewMode.Normal, previewTarget, card.DynamicVars);
            return Regex.Replace(card.GetDescriptionForPile(pile, previewTarget), @"\[[^\]]*\]", "");
        }
        string Effects(CatalogPower power) => Regex.Replace(((StringVar)power.DynamicVars["Effects"]).StringValue, @"\[[^\]]*\]", "");
        CatalogPower Power(string id) => player.Creature.Powers.OfType<CatalogPower>().Single(power => power.DesignId == id);
        bool Remaining(CatalogPower power, int count) => Effects(power).Contains("本回合还可触发" + count + "次");
        async Task<CatalogCard[]> Acquire(string id)
        {
            var before = player.Deck.Cards.ToHashSet();
            await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model(id), player), PileType.Deck, skipVisuals: true);
            return player.Deck.Cards.Except(before).OfType<CatalogCard>().ToArray();
        }
        Task Deplete(CatalogCard card) => card.OnShineDepleted(new ShineDepletionContext(card, card, player, Guid.NewGuid().ToString()));
        try
        {
            await Clear();
            if (prepare)
            {
                if (hoverPower is "JZ05" or "JK260")
                {
                    NextTurn();
                    await Play(await Add("JZ05", upgraded: true));
                    await Play(await Add("JK260"));
                    await Add("JB01"); await Add("JB01");
                    await Add("JK11"); await Add("JK11");
                    player.PlayerCombatState!.Energy = 20;
                    var node = NCombatRoom.Instance!.FindChildren("*", "", true, false).OfType<NPower>()
                        .Single(node => node.Model == Power(hoverPower));
                    node.EmitSignal(Control.SignalName.MouseEntered);
                    return;
                }
                foreach (string id in new[] { "JK184", "JK314" })
                {
                    var group = await Acquire(id);
                    var member = group.First(card => card.DesignId != id);
                    await Deplete(member);
                    var permanent = group.Single(card => card.DesignId == id);
                    var main = CardCopies.ForCombat(permanent, player, CardCopyPurpose.Transfer, permanent);
                    using var single = KnowledgeCombat.SingleCard();
                    await CardPileCmd.Add(main, PileType.Hand);
                }
                await Add("JK206");
                await Add("JZ05", upgraded: true);
                await Add("JK260");
                await Add("JK11");
                await Add("JB01");
                player.PlayerCombatState!.Energy = 20;
                return;
            }
            foreach (var definition in CardCatalog.Cards)
                foreach (bool upgraded in new[] { false, true })
                {
                    var card = CardCatalog.Create(definition.Id, player);
                    if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
                    string description = Describe(card);
                    Check(definition.Id + " valid description " + upgraded, !description.Contains("ERROR") &&
                        (definition.Id == "JK206" || !description.Contains('（')));
                }
            foreach (bool upgraded in new[] { false, true })
            {
                await Clear();
                var formula = await Add("JK206", upgraded);
                await PowerCmd.Apply<StrengthPower>(context, player.Creature, 2, player.Creature, formula);
                await PowerCmd.Apply<VulnerablePower>(context, target, 1, player.Creature, formula);
                string description = Describe(formula);
                int predicted = (int)formula.DynamicVars.CalculatedDamage.PreviewValue;
                Check("formula combat calculation " + upgraded, description.Contains("\n（造成" + predicted + "点伤害）"));
                var deck = player.RunState.CreateCard(CardCatalog.Model("JK206"), player);
                Check("formula note hidden in deck " + upgraded, !Describe(deck, PileType.Deck).Contains('（'));
                int hp = target.CurrentHp;
                await Play(formula);
                Check("formula prediction matches actual payment result " + upgraded, hp - target.CurrentHp == predicted);
            }
            await Clear();
            foreach (string id in new[] { "JK184", "JK314" })
            {
                var group = await Acquire(id);
                var otherGroup = await Acquire(id);
                var main = group.Single(card => card.DesignId == id);
                var members = group.Where(card => card != main).ToArray();
                Check(id + " zero progress is hidden", !Describe(main, PileType.Deck).Contains("已耗尽"));
                await Deplete(members[0]);
                await Deplete(members[0]);
                Check(id + " member contributes only once", KnowledgeProgress.DepletedPartners(main) == 1);
                Check(id + " deck shows permanent progress", Describe(main, PileType.Deck).Contains("（已耗尽1张）"));
                Check(id + " independent acquisition group stays at zero", KnowledgeProgress.DepletedPartners(otherGroup.Single(card => card.DesignId == id)) == 0);
                var loaded = player.RunState.LoadCard(main.ToSerializable(), player);
                Check(id + " progress survives save", KnowledgeProgress.DepletedPartners(loaded) == 1 && Describe(loaded, PileType.Deck).Contains("已耗尽1张"));
                var clone = CardCopies.ForCombat(main, player, CardCopyPurpose.Transfer, main);
                await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Hand, player);
                DamageModifiers.Add(clone, extraHits: 2);
                Check(id + " generic extra hits do not change research count", KnowledgeProgress.DepletedPartners(clone) == 1 && Describe(clone).Contains("已耗尽1张"));
                foreach (var member in members.Skip(1)) await Deplete(member);
                Check(id + " all members contribute to expected limit", KnowledgeProgress.DepletedPartners(main) == (id == "JK184" ? 6 : 3));
                Check(id + " combat copies refresh with permanent progress", KnowledgeProgress.DepletedPartners(clone) == KnowledgeProgress.DepletedPartners(main));
                if (id == "JK314")
                {
                    Check("growth increases printed damage", Describe(main, PileType.Deck).Contains("造成60点伤害"));
                    main.UpgradeInternal(); main.FinalizeUpgradeInternal();
                    Check("upgrade preserves growth", Describe(main, PileType.Deck).Contains("造成68点伤害"));
                    var attack = CardCopies.ForCombat(main, player, CardCopyPurpose.Transfer, main);
                    await CardPileCmd.AddGeneratedCardToCombat(attack, PileType.Hand, player);
                    int hp = target.CurrentHp;
                    await Play(attack);
                    Check("grown damage actually resolves", hp - target.CurrentHp == 68);
                }
            }
            foreach (bool upgraded in new[] { false, true })
            {
                await Clear(); NextTurn();
                await Play(await Add("JZ05", upgraded));
                var improv = Power("JZ05");
                int limit = upgraded ? 2 : 1;
                Check("improv initial remaining " + upgraded, Remaining(improv, limit));
                var zero = await Add("JK49");
                zero.DynamicVars.Damage.BaseValue = 0;
                await Play(zero);
                Check("zero damage preserves remaining triggers " + upgraded, Remaining(improv, limit));
                await Play(await Add("JB01"));
                Check("improv decrements after damage " + upgraded, Remaining(improv, limit - 1));
                await Play(await Add("JB01"));
                Check("improv clamps at zero " + upgraded, Remaining(improv, 0));
                NextTurn(); await improv.AfterPlayerTurnStart(context, player);
                Check("improv resets next turn " + upgraded, Remaining(improv, limit));
                await Clear(); NextTurn();
                await Play(await Add("JK260", upgraded));
                var strength = Power("JK260");
                Check("knowledge power initial remaining " + upgraded, Remaining(strength, 1));
                await Play(await Add("JK11"));
                Check("knowledge power consumes trigger " + upgraded, Remaining(strength, 0));
                Check("knowledge power grants expected strength " + upgraded, player.Creature.GetPower<StrengthPower>()?.Amount == (upgraded ? 3 : 2));
                await Play(await Add("JK11"));
                Check("knowledge power does not retrigger " + upgraded, Remaining(strength, 0) && player.Creature.GetPower<StrengthPower>()?.Amount == (upgraded ? 3 : 2));
                NextTurn(); await strength.AfterPlayerTurnStart(context, player);
                Check("knowledge power resets next turn " + upgraded, Remaining(strength, 1));
            }
            await Clear(); NextTurn();
            await Play(await Add("JB01"));
            await Play(await Add("JZ05"));
            Check("mid-turn install accounts for earlier attack damage", Remaining(Power("JZ05"), 0));
            await Play(await Add("JZ05", upgraded: true));
            Check("stacking increases the shared remaining limit", Remaining(Power("JZ05"), 2));
            await Play(await Add("JK200"));
            Check("multiple attack hits each consume the shared limit", Remaining(Power("JZ05"), 0));
            await Clear(); NextTurn();
            await Play(await Add("JK260"));
            await Play(await Add("JK260", upgraded: true));
            Check("stacked knowledge powers show both pending triggers", Remaining(Power("JK260"), 2));
            await Play(await Add("JK11"));
            Check("stacked knowledge triggers resolve together", Remaining(Power("JK260"), 0) && player.Creature.GetPower<StrengthPower>()?.Amount == 5);
            await Clear();
            var acquired = await Acquire("JK314");
            var growing = acquired.Single(card => card.DesignId == "JK314");
            var lastUse = acquired.First(card => card.DesignId == "JK122B");
            Shine.SetState(lastUse, 1, 3);
            var combatUse = CardCopies.ForCombat(lastUse, player, CardCopyPurpose.Transfer, lastUse);
            await CardPileCmd.AddGeneratedCardToCombat(combatUse, PileType.Hand, player);
            await Play(combatUse);
            Check("real final use removes the permanent companion", !player.Deck.Cards.Contains(lastUse));
            Check("real final use triggers the bound source growth", KnowledgeProgress.DepletedPartners(growing) == 1);
            Check("real depletion updates printed damage and progress", Describe(growing, PileType.Deck).Contains("造成36点伤害") && Describe(growing, PileType.Deck).Contains("已耗尽1张"));
        }
        catch (Exception error) { checks.Add(new { name = "probe exception", pass = false, error = error.ToString() }); Save(); }
    }
}
