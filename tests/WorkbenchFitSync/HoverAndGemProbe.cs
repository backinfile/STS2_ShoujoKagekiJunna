using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKageki.Core.V1;

namespace JunnaWorkbenchFitSync;

public sealed partial class Workbench5000Command
{
    private static async Task HoverReadability(Player player)
    {
        await Reset(player, visualize: true);
        var room = NCombatRoom.Instance!;
        var foe = player.Creature.CombatState!.Enemies.First();
        await Play(await Card(player, "JA38", visualize: true), foe);
        await Play(await Card(player, "JA64", visualize: true), foe);
        for (int i = 0; i < 4; i++) await room.ToSignal(room.GetTree(), SceneTree.SignalName.ProcessFrame);
        var hud = room.FindChildren("*", "", true, false).OfType<ArrowHud>().Single();
        hud.SetProcess(false);
        var anchor = room.CreatureNodes.First(n => n.Entity == foe).Hitbox;
        void Set(string field, object value) => AccessTools.Field(typeof(ArrowHud), field).SetValue(hud, value);
        T Get<T>(string field) => (T)AccessTools.Field(typeof(ArrowHud), field).GetValue(hud)!;
        void Call(string method, params object[] args) => AccessTools.Method(typeof(ArrowHud), method).Invoke(hud, args);
        var candidates = Get<List<ArrowDeployment>>("_hoverCandidates");
        candidates.Clear(); candidates.AddRange(ArrowCombat.Deployed.Where(a => a.Card.Owner == player));
        Set("_hoverAnchor", anchor); Set("_previewTarget", foe);
        Call("GroupHoverCandidates"); Call("EnsureHoverPreview"); Call("RefreshPreviewFan");
        NHoverTipSet.CreateAndShow(anchor, new IHoverTip[]
        {
            ModelDb.Power<StrengthPower>().GetDumbHoverTip(9),
            ModelDb.Power<RitualPower>().GetDumbHoverTip(9),
            ModelDb.Power<VulnerablePower>().GetDumbHoverTip(2)
        })?.SetAlignment(anchor, HoverTip.GetHoverTipAlignment(anchor));
        for (int i = 0; i < 4; i++) await room.ToSignal(room.GetTree(), SceneTree.SignalName.ProcessFrame);
        Call("LayoutHoverPreview", anchor, false);
        var preview = Get<Control>("_hoverPreview"); preview.Show();
        var cards = Get<Dictionary<ArrowDeployment, NCard>>("_previewCache").Values.Where(c => c.Visible).ToArray();
        float scale = cards.Max(c => c.Scale.X);
        var counter = Get<Label>("_previewCount");
        string before = counter.Text;
        hud._Input(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true });
        for (int i = 0; i < 10; i++) await room.ToSignal(room.GetTree(), SceneTree.SignalName.ProcessFrame);
        var actual = new { scale, expectedMinimum = .75f, visibleCards = cards.Length, before, after = counter.Text,
            panel = new { x = preview.GlobalPosition.X, y = preview.GlobalPosition.Y, width = preview.Size.X, height = preview.Size.Y },
            viewport = new { width = hud.GetViewportRect().Size.X, height = hud.GetViewportRect().Size.Y },
            textReadable = scale >= .75f, navigation = before == "1 / 2" && counter.Text == "2 / 2" };
        string dir = Path.GetFullPath("../../../out/tmp/arrow-preview-readability", System.Environment.CurrentDirectory);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(actual, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task BurningMaxHp(Player player)
    {
        var rows = new List<object>();
        string dir = Path.GetFullPath("../../../out/tmp/burning-max-hp", System.Environment.CurrentDirectory);
        Directory.CreateDirectory(dir);
        foreach (bool upgraded in new[] { false, true })
        {
            await Reset(player);
            foreach (var gem in player.Relics.OfType<JunnaGem>().ToArray()) await RelicCmd.Remove(gem);
            var relic = upgraded ? (RelicModel)ModelDb.Relic<JunnaBurningGem>().ToMutable() : ModelDb.Relic<JunnaGreenGem>().ToMutable();
            await RelicCmd.Obtain(relic, player);
            await CreatureCmd.SetMaxHp(player.Creature, 70); await CreatureCmd.SetCurrentHp(player.Creature, 20);
            var cards = Enumerable.Range(0, 3).Select(_ => player.RunState.CreateCard(CardCatalog.Model("JB01"), player)).ToArray();
            await CardPileCmd.Add(cards, player.Deck, skipVisuals: true);
            int maxAfterGain = player.Creature.MaxHp, hpAfterGain = player.Creature.CurrentHp;
            await CardPileCmd.RemoveFromDeck(cards, showPreview: false);
            int maxAfterLoss = player.Creature.MaxHp, hpAfterLoss = player.Creature.CurrentHp;
            await Card(player, "JB01");
            bool temporaryCardDidNotTrigger = player.Creature.MaxHp == maxAfterLoss && player.Creature.CurrentHp == hpAfterLoss;
            CardModel permanent;
            using (KnowledgeCombat.SingleCard())
            {
                permanent = player.RunState.CreateCard(CardCatalog.Model("JK122B"), player);
                await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
            }
            Shine.SetState(permanent, 1, 3);
            var spent = await Card(player, "JK122B"); spent.DeckVersion = permanent;
            Shine.SetState(spent, 1, 3);
            await CreatureCmd.SetCurrentHp(player.Creature, 20);
            int maxBeforeDepletion = player.Creature.MaxHp;
            await Play(spent);
            int maxAfterDepletion = player.Creature.MaxHp, hpAfterDepletion = player.Creature.CurrentHp;
            var temporary = await Card(player, "JK122B"); Shine.SetState(temporary, 1, 3, temporary: true);
            await Play(temporary);
            bool temporaryDepletionDidNotTrigger = player.Creature.MaxHp == maxAfterDepletion && player.Creature.CurrentHp == hpAfterDepletion;
            rows.Add(new { upgraded, pass = maxAfterGain == (upgraded ? 76 : 70) && maxAfterLoss == (upgraded ? 82 : 70)
                && hpAfterGain == 26 && hpAfterLoss == 32 && temporaryCardDidNotTrigger
                && maxAfterDepletion == maxBeforeDepletion + (upgraded ? 2 : 0) && hpAfterDepletion == 22 && temporaryDepletionDidNotTrigger,
                maxAfterGain, hpAfterGain, maxAfterLoss, hpAfterLoss, temporaryCardDidNotTrigger,
                maxBeforeDepletion, maxAfterDepletion, hpAfterDepletion, temporaryDepletionDidNotTrigger,
                description = relic.DynamicDescription.GetFormattedText() });
            File.WriteAllText(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static async Task PrepareGemDepletion(Player player, bool upgraded)
    {
        var room = NCombatRoom.Instance!;
        foreach (var hud in room.FindChildren("*", "", true, false).OfType<ArrowHud>())
        {
            AccessTools.Method(typeof(ArrowHud), "ClearHoverPreview").Invoke(hud, null);
            hud.SetProcess(true);
        }
        await Reset(player, visualize: true);
        foreach (var gem in player.Relics.OfType<JunnaGem>().ToArray()) await RelicCmd.Remove(gem);
        await RelicCmd.Obtain(upgraded ? (RelicModel)ModelDb.Relic<JunnaBurningGem>().ToMutable() : ModelDb.Relic<JunnaGreenGem>().ToMutable(), player);
        CardModel permanent;
        using (KnowledgeCombat.SingleCard())
        {
            permanent = player.RunState.CreateCard(CardCatalog.Model("JK122B"), player);
            await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
        }
        Shine.SetState(permanent, 1, 3);
        var spent = await Card(player, "JK122B", visualize: true); spent.DeckVersion = permanent;
        Shine.SetState(spent, 1, 3);
        var temporary = await Card(player, "JK122B", visualize: true); Shine.SetState(temporary, 1, 3, temporary: true);
        await CreatureCmd.SetMaxHp(player.Creature, 70); await CreatureCmd.SetCurrentHp(player.Creature, 20);
        player.PlayerCombatState!.Energy = 3;
        foreach (var foe in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(foe, 100); await CreatureCmd.SetCurrentHp(foe, 100); }
    }
}
