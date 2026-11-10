using System.Text.Json;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;
using ShoujoKageki.Core.V1;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaCompatibilityProbe;

public sealed class KnowledgeAvailabilityCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_knowledge_availability";
    public override string Args => "merchant | learning-prepare | learning-check | learning-click | learning-info";
    public override string Description => "Check merchant group entrances and repeated smith selection.";
    public override bool IsNetworked => false;
    private static string DirectoryPath => Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "../../../out/tmp/knowledge-availability"));
    private static void Save(string file, object results)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, file), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player == null) return new(false, "Run required");
        return args.FirstOrDefault() switch
        {
            "merchant" => new(Merchant(player), true, "Merchant checks started"),
            "learning-prepare" => new(PrepareLearning(player), true, "Learning fixture prepared"),
            "learning-click" => new(ClickLearning(), true, "Clicking learning with native pointer input"),
            "learning-check" => new(CheckLearning(player), true, "Learning checks started"),
            _ => new(true, JsonSerializer.Serialize(player.Deck.Cards.Select(card => new { id = card.Id.Entry,
                title = card.Title, level = card.CurrentUpgradeLevel, maximum = card.MaxUpgradeLevel, upgradable = card.IsUpgradable })))
        };
    }
    private static async Task Merchant(Player player)
    {
        var checks = new List<object>();
        void Expect(string name, bool pass) { checks.Add(new { name, pass }); Save("merchant.json", checks); }
        try
        {
            var source = player.RunState.CreateCard(CardCatalog.Model("JK126"), player);
            var partner = player.RunState.CreateCard(CardCatalog.Model("JK82"), player);
            await DynamicKnowledge.Prepare(new ThrowingPlayerChoiceContext(), source, [source, partner]);
            await CardPileCmd.Add(source, PileType.Deck, skipVisuals: true);
            var candidates = new[] { CardCatalog.Model("JK126"), CardCatalog.Model("JK82"), CardCatalog.Model("JK184B") };
            Expect("non-pool singing companion is not an entrance", !DynamicKnowledge.IsRewardEntrance(player, candidates[2]));
            var filtered = Hook.ModifyMerchantCardPool(player.RunState, player, candidates).ToArray();
            Expect("merchant pool excludes non-pool cards", filtered.All(card => card.Id != candidates[2].Id));
            Expect("merchant pool preserves independently pooled related cards", filtered.Any(card => card.Id == source.Id) && filtered.Any(card => card.Id == partner.Id));
            Expect("typed merchant generation excludes the singing companion", Enumerable.Range(0, 16).All(_ =>
                CardFactory.CreateForMerchant(player, candidates, CardType.Skill).Card.Id != candidates[2].Id));
            Expect("rarity merchant generation excludes the singing companion", Enumerable.Range(0, 16).All(_ =>
                CardFactory.CreateForMerchant(player, candidates, CardRarity.Rare).Card.Id != candidates[2].Id));
            Expect("caller candidates are preserved", candidates.Length == 3 && candidates[1].Id == partner.Id);
        }
        catch (Exception error) { checks.Add(new { pass = false, error = error.ToString() }); Save("merchant.json", checks); }
    }
    private static async Task PrepareLearning(Player player)
    {
        var relations = (SavedSpireField<Player, string>)AccessTools.Field(typeof(DynamicKnowledge), "Relations").GetValue(null)!;
        relations.Set(player, null!);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        for (int level = 0; level < 3; level++)
        {
            var card = player.RunState.CreateCard(CardCatalog.Model("JK82"), player);
            for (int i = 0; i < level; i++) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(card, PileType.Deck, skipVisuals: true);
        }
        Save("learning-before.json", player.Deck.Cards.Select(card => new { card.Title, card.CurrentUpgradeLevel, card.IsUpgradable }));
    }
    private static async Task ClickLearning()
    {
        var overlay = NOverlayStack.Instance!.Peek() as Node ?? throw new InvalidOperationException("Upgrade screen required");
        var holder = overlay.FindChildren("*", "", true, false).OfType<NGridCardHolder>()
            .First(node => node.CardModel?.Title == "学无止境+1");
        var hitbox = holder.GetNodeOrNull<Control>("Hitbox") ?? holder;
        var point = hitbox.GetGlobalRect().GetCenter();
        var viewport = holder.GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await holder.ToSignal(holder.GetTree(), SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = true }, true);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
        Save("learning-pointer.json", new { point = point.ToString(), hovered = viewport.GuiGetHoveredControl()?.GetPath().ToString() });
    }
    private sealed class LearningSelector(CardModel[] selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult(options.Where(selected.Contains));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private static async Task CheckLearning(Player player)
    {
        var checks = new List<object>();
        void Expect(string name, bool pass) { checks.Add(new { name, pass }); Save("learning-check.json", checks); }
        try
        {
            await PrepareLearning(player);
            var cards = player.Deck.Cards.Where(card => CardCatalog.DesignId(card) == "JK82").ToArray();
            foreach (var card in cards)
            {
                int before = card.CurrentUpgradeLevel;
                int known = player.Deck.Cards.Count(c => CardCatalog.DesignId(c) == "JK82B");
                int observed = 0;
                using var observer = CoreApi.Get<ICardUpgrades>().Register(c => c == card, async (_, _, _) =>
                { await Task.Yield(); observed++; });
                using (CardSelectCmd.UseSelector(new LearningSelector([card])))
                {
                    bool result = await new SmithRestSiteOption(player).OnSelect();
                    Expect("smith succeeds from upgrade level " + before, result && card.CurrentUpgradeLevel == before + 1);
                    Expect("smith awaits new knowledge from level " + before,
                        player.Deck.Cards.Count(c => CardCatalog.DesignId(c) == "JK82B") == known + 1);
                    Expect("smith awaits asynchronous observers from level " + before, observed == 1);
                    Expect("learning remains upgradable from level " + before, card.IsUpgradable);
                }
            }
            using (CardSelectCmd.UseSelector(new LearningSelector([])))
                Expect("canceling smith preserves false result", !await new SmithRestSiteOption(player).OnSelect());
            try { CardCmd.Upgrade(cards[0], MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None); }
            catch (InvalidOperationException) { Expect("rest site scope is released after completion", true); return; }
            Expect("rest site scope is released after completion", false);
        }
        catch (Exception error) { checks.Add(new { pass = false, error = error.ToString() }); Save("learning-check.json", checks); }
    }
}
