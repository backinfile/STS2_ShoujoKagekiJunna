using System.Text.Json;
using Environment = System.Environment;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace JunnaIntegration;

public sealed class PowerPresentationCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_presentation";
    public override string Args => "powers | merchant | upgrades";
    public override string Description => "Scoped power text, stacking and merchant card checks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "A run is required");
        return new(args.FirstOrDefault() switch
        {
            "merchant" => Merchant(player),
            "upgrades" => Upgrades(),
            _ => Powers(player)
        }, true, "Presentation checks started");
    }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
    private static string Text(PowerModel power) => power.HoverTips.OfType<HoverTip>().First().Description;
    private static async Task Merchant(Player player)
    {
        var rows = new List<object>();
        void Check(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var slot = Descendants(NGame.Instance!).OfType<NMerchantCard>().First(node => node.IsVisibleInTree());
        foreach (var id in new[] { "JK67", "A" })
        {
            var entry = new MerchantCardEntry(player, null, [CardCatalog.Model(id)], CardCatalog.Model(id).Type);
            entry.Populate();
            slot.FillSlot(entry);
            await Task.Delay(350);
            var source = (NCard)AccessTools.Field(typeof(NMerchantCard), "_cardNode").GetValue(slot)!;
            var stack = source.GetNode<KnowledgeCardStack>("JunnaKnowledgeStack");
            var companions = stack.GetChildren().OfType<NCard>().ToArray();
            Check(id + " has physical companion cards", companions.Length, KnowledgePreviewCards.Create(source.Model!).Count - 1);
            var sourceSize = source.GetNode<Control>("%Frame").GetGlobalRect().Size;
            foreach (var companion in companions)
            {
                var ratio = companion.GetNode<Control>("%Frame").GetGlobalRect().Size / sourceSize;
                Check(id + " companion remains within source scale", ratio.X <= 1.01f && ratio.Y <= 1.01f, true);
                Check(id + " companion uses matching body layout", companion.Body.GetTransform().IsEqualApprox(source.Body.GetTransform()), true);
                Check(id + " backing headers remain visible above the offered card",
                    companion.GetNode<Control>("%Frame").GetGlobalRect().Position.Y < source.GetNode<Control>("%Frame").GetGlobalRect().Position.Y, true);
            }
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "merchant-presentation-results.json")), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static Task Upgrades()
    {
        var rows = new List<object>();
        foreach (string id in new[] { "JK10", "B", "JK46", "JK50B", "JA57", "JK82", "JZ05" })
        {
            var card = (CardModel)CardCatalog.Model(id).MutableClone();
            card.UpgradeInternal();
            card.UpdateDynamicVarPreview(CardPreviewMode.Upgrade, null, card.DynamicVars);
            string text = card.GetDescriptionForUpgradePreview();
            var highlights = System.Text.RegularExpressions.Regex.Matches(text, @"\[green\](.*?)\[/green\]")
                .Select(match => match.Groups[1].Value).ToArray();
            rows.Add(new
            {
                name = id + " highlights only changed fragments",
                text,
                highlights,
                pass = highlights.Length == (id == "JZ05" ? 2 : 1) && highlights.All(part => !part.Contains('\n') && !part.Contains('。'))
            });
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "upgrade-presentation-results.json")),
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        return Task.CompletedTask;
    }
    private static async Task Powers(Player player)
    {
        var rows = new List<object>();
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        void Check(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        async Task Reset()
        {
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
            {
                ArrowCombat.Cancel(card);
                await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            }
            foreach (var enemy in state.HittableEnemies)
            {
                await CreatureCmd.SetMaxHp(enemy, 1000000);
                await CreatureCmd.SetCurrentHp(enemy, 1000000);
            }
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState!.Energy = 99;
            player.PlayerCombatState.IncrementTurnNumber();
        }
        async Task<CardModel> Play(string id, bool up = false)
        {
            using var single = KnowledgeCombat.SingleCard();
            var card = CardCatalog.Create(id, player);
            if (up)
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            await CardActions.PaidPlay(context, card, state.HittableEnemies.First());
            return card;
        }
        var report = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "power-presentation-results.json"));
        foreach (var model in ModelDb.AllPowers.Where(p => p.GetType().Assembly == typeof(CatalogPower).Assembly))
        {
            try
            {
                Check(model.Id.Entry + " has default description", !string.IsNullOrWhiteSpace(model.GetDumbHoverTip().Description), true);
                if (model is not CatalogPower catalog || catalog.DesignId == "JH24") continue;
                await Reset();
                await Play(catalog.DesignId);
                await Play(catalog.DesignId, true);
                var power = player.Creature.Powers.OfType<CatalogPower>().Single(p => p.DesignId == catalog.DesignId);
                string text = Text(power);
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                Check(model.Id.Entry + " has smart description", power.HasSmartDescription, true);
                Check(model.Id.Entry + " contains only ongoing effect", !text.Contains("知识。") && !text.Contains("X次"), true);
                Check(model.Id.Entry + " has no duplicate paragraphs", lines.Distinct().Count(), lines.Length);
                Check(model.Id.Entry + " has no unresolved placeholders", !text.Contains('{') && !text.Contains('}'), true);
                if (catalog.DesignId is "JA22" or "JK27" or "JK81")
                {
                    Check(model.Id.Entry + " is nonstacking", power.StackType, PowerStackType.Single);
                    Check(model.Id.Entry + " amount remains one", power.Amount, 1);
                }
                if (catalog.DesignId == "JK67")
                {
                    Check("mixed bonus stacks", power.Amount, 5);
                    Check("bonus text uses combined amount", text.Contains("[blue]5[/blue]"), true);
                }
                if (catalog.DesignId == "JH19")
                {
                    Check("paid replays stack", power.Amount, 2);
                    Check("paid replay text excludes immediate block", !text.Contains("格挡"), true);
                }
                if (catalog.DesignId == "JK80") Check("next block stacks", power.Amount, 12);
                rows.Add(new { name = model.Id.Entry + " rendered text", text, pass = true });
            }
            catch (Exception ex) { rows.Add(new { name = model.Id.Entry, pass = false, error = ex.ToString() }); }
            File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        await Reset();
        await PowerCmd.Apply<JunnaNextCardBonus>(context, player.Creature, 2, player.Creature, null);
        await PowerCmd.Apply<JunnaNextCardBonus>(context, player.Creature, 3, player.Creature, null);
        var next = player.Creature.GetPower<JunnaNextCardBonus>()!;
        Check("next bonus sums", next.Amount, 5);
        Check("next bonus smart description", next.HasSmartDescription, true);
        var strike = await Play("JB01");
        Check("next bonus applies once", DamageModifiers.CaptureDamageModifierSnapshot(strike).DamageBonus, 5m);
        Check("next bonus consumed", player.Creature.GetPower<JunnaNextCardBonus>() == null, true);
        await Reset();
        await EnergyReserve.Add(context, player, 2);
        await EnergyReserve.Add(context, player, 3);
        var reserve = player.Creature.GetPower<EnergyReserve>()!;
        Check("reserve sums", reserve.Amount, 5);
        Check("reserve smart description", reserve.HasSmartDescription, true);
        File.WriteAllText(report, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
