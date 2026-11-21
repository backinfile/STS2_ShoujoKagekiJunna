using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaWorkbenchFitSync;

public sealed partial class Workbench5000Command
{
    private static Creature? FollowingTarget;
    private static bool SawFollowing;
    private static void CaptureAutoPlay(CardModel card, Creature? target)
    { if (CardCatalog.DesignId(card) == "U_CARD_012") { SawFollowing = true; FollowingTarget = target; } }
    private static async Task Review(Player player)
    {
        var rows = new List<object>();
        string output = Path.Combine(Path.GetDirectoryName(ResultPath)!, "review-results.json");
        async Task Case(string name, Func<Task> run)
        {
            try { await Reset(player); await run(); rows.Add(new { name, pass = true }); }
            catch (Exception error) { rows.Add(new { name, pass = false, error = error.ToString() }); }
            File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        const string patchId = "JunnaWorkbenchFitSync.NativeReview";
        var patch = new Harmony(patchId);
        patch.Patch(AccessTools.Method(typeof(CardCmd), nameof(CardCmd.AutoPlay)),
            prefix: new HarmonyMethod(typeof(Workbench5000Command), nameof(CaptureAutoPlay)));
        double speed = Godot.Engine.TimeScale; Godot.Engine.TimeScale = 5;
        try
        {
            foreach (bool upgraded in new[] { false, true })
            {
                foreach (var id in new[] { "JA77", "U_CARD_011", "U_CARD_012" })
                    await Case("native attack conversion " + id + " " + upgraded, async () =>
                    {
                        var card = await Card(player, id, upgraded);
                        ArrowConversion.Convert(card);
                        Expect(card.Type == CardType.Attack && !CardCatalog.IsArrow(card), "Damage arrow converts to attack");
                    });
                await Case("volley native multi hit " + upgraded, async () =>
                {
                    var card = await Card(player, "JA77", upgraded);
                    await Play(card); await ArrowCombat.AccelerateAll(Context, player, 99);
                    Expect(player.Creature.CombatState!.Enemies.All(e => e.CurrentHp == 10000 - 10 * (upgraded ? 5 : 4)), "Each native hit retains bonus and hits all enemies");
                });
                await Case("updated soaring hit and haste " + upgraded, async () =>
                {
                    var foe = player.Creature.CombatState!.Enemies.First();
                    var arrow = await Card(player, "JA38"); await Play(arrow);
                    await Play(await Card(player, "JK84B", upgraded));
                    Expect(foe.CurrentHp == 10000 - (upgraded ? 18 : 14) && ArrowCombat.Find(arrow)?.Remaining == 1, "New direct damage and one haste");
                });
                await Case("updated manifesto double hit " + upgraded, async () =>
                {
                    var foe = player.Creature.CombatState!.Enemies.First();
                    await Play(await Card(player, "JK131B", upgraded)); await ArrowCombat.AccelerateAll(Context, player, 99);
                    Expect(foe.CurrentHp == 10000 - 2 * (upgraded ? 32 : 24), "Updated delayed damage occurs twice");
                });
                await Case("cage dynamic targets " + upgraded, async () =>
                {
                    var foes = player.Creature.CombatState!.Enemies.ToArray();
                    var cage = await Card(player, "U_CARD_013", upgraded); await Play(cage);
                    Expect(player.Creature.HasPower<JunnaCagedPower>(), "Player owns native-style temporary power before arrows exist");
                    var power = player.Creature.GetPower<JunnaCagedPower>()!;
                    Expect(power.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Move, foes[0], null, null) == 1, "No arrow means no reduction");
                    var arrow = await Card(player, "JA38"); await Play(arrow, foes[0]);
                    int hp = player.Creature.CurrentHp;
                    await CreatureCmd.Damage(Context, [player.Creature], 20, ValueProp.Move, foes[0], null, null);
                    Expect(player.Creature.CurrentHp == hp - 10, "New arrow starts actual reduction");
                    ArrowCombat.RetargetEnemies(player, foes[1]);
                    Expect(power.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Move, foes[0], null, null) == 1
                        && power.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Move, foes[1], null, null) == 0.5m, "Reduction follows changed target");
                    Expect(power.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Unpowered, foes[1], null, null) == 1
                        && power.ModifyDamageMultiplicative(foes[0], 20, ValueProp.Move, foes[1], null, null) == 1, "Native Colossus attack and owner scope");
                    await Play(await Card(player, "U_CARD_013"));
                    Expect(player.Creature.Powers.Count(p => p is JunnaCagedPower) == 1
                        && power.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Move, foes[1], null, null) == 0.5m, "Repeated cage stays at fifty percent");
                    await ArrowCombat.AccelerateAll(Context, player, 99);
                    Expect(power.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Move, foes[1], null, null) == 1, "Completed arrow stops reduction");
                    await Play(await Card(player, "U_CARD_011"));
                    Expect(foes.All(foe => power.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Move, foe, null, null) == 0.5m), "Group arrow covers current enemies once");
                    await power.AfterSideTurnEnd(Context, CombatSide.Player, [player.Creature]);
                    Expect(player.Creature.HasPower<JunnaCagedPower>(), "Survives player end for enemy response");
                    await power.AfterSideTurnEnd(Context, CombatSide.Enemy, foes);
                    Expect(!player.Creature.HasPower<JunnaCagedPower>(), "Expires at native enemy end");
                });
            }
            await Case("following arrow native random target", async () =>
            {
                await Card(player, "U_CARD_012"); SawFollowing = false; FollowingTarget = player.Creature;
                await Play(await Card(player, "JA15"));
                Expect(SawFollowing && FollowingTarget == null, "Null reaches native AutoPlay for combat target RNG");
            });
            await Case("blocked following arrow native destination", async () =>
            {
                var follower = await Card(player, "U_CARD_012"); follower.AddKeyword(CardKeyword.Unplayable);
                await Play(await Card(player, "JA38"));
                Expect(follower.Pile?.Type == PileType.Discard && ArrowCombat.Find(follower) == null, "Native blocked auto play moves to result pile");
            });
            await Case("burning gem max hp growth", async () =>
            {
                var originals = player.Relics.OfType<JunnaGem>().ToArray();
                var deck = player.Deck.Cards.ToHashSet();
                foreach (var relic in originals) await RelicCmd.Remove(relic);
                var gem = await RelicCmd.Obtain(ModelDb.Relic<JunnaBurningGem>().ToMutable(), player);
                try
                {
                    Expect(gem.DynamicDescription.GetFormattedText().Contains("最大生命值提升2"), "Localization matches max hp growth");
                    int originalMax = player.Creature.MaxHp;
                    await CreatureCmd.SetCurrentHp(player.Creature, 20);
                    var card = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
                    await CardPileCmd.Add(card, PileType.Deck, skipVisuals: true);
                    Expect(player.Creature.CurrentHp == 22 && player.Creature.MaxHp == originalMax + 2, "Single gain increases max hp by two");
                    await CardPileCmd.RemoveFromDeck(card, showPreview: false);
                    Expect(player.Creature.CurrentHp == 24 && player.Creature.MaxHp == originalMax + 4, "Single loss increases max hp by two");
                    var batch = Enumerable.Range(0, 3).Select(_ => player.RunState.CreateCard(CardCatalog.Model("JB01"), player)).ToArray();
                    await CardPileCmd.Add(batch, player.Deck, skipVisuals: true);
                    Expect(player.Creature.CurrentHp == 30 && player.Creature.MaxHp == originalMax + 10, "Three gains increase max hp by six");
                    await CardPileCmd.RemoveFromDeck(batch, showPreview: false);
                    Expect(player.Creature.CurrentHp == 36 && player.Creature.MaxHp == originalMax + 16, "Three losses increase max hp by six");
                    await CreatureCmd.SetCurrentHp(player.Creature, player.Creature.MaxHp);
                    await CardPileCmd.Add(player.RunState.CreateCard(CardCatalog.Model("JB01"), player), PileType.Deck, skipVisuals: true);
                    Expect(player.Creature.CurrentHp == player.Creature.MaxHp, "Native max hp gain also heals at full health");
                }
                finally
                {
                    await RelicCmd.Remove(gem);
                    await CardPileCmd.RemoveFromDeck(player.Deck.Cards.Where(c => !deck.Contains(c)).ToArray(), showPreview: false);
                    foreach (var original in originals) await RelicCmd.Obtain(((RelicModel)original.CanonicalInstance).ToMutable(), player);
                }
            });
        }
        finally
        {
            patch.UnpatchAll(patchId); Godot.Engine.TimeScale = speed;
            File.WriteAllText(Path.ChangeExtension(output, ".completed"), "completed");
        }
    }
    private static async Task PrepareReview(Player player)
    {
        await Reset(player, visualize: true); player.PlayerCombatState!.Energy = 9;
        foreach (var gem in player.Relics.OfType<JunnaGem>().ToArray()) await RelicCmd.Remove(gem);
        await RelicCmd.Obtain(ModelDb.Relic<JunnaBurningGem>().ToMutable(), player);
        await CreatureCmd.SetMaxHp(player.Creature, 70); await CreatureCmd.SetCurrentHp(player.Creature, 40);
        foreach (var foe in player.Creature.CombatState!.Enemies)
        { await CreatureCmd.SetMaxHp(foe, 300); await CreatureCmd.SetCurrentHp(foe, 300); }
        foreach (var id in new[] { "U_CARD_013", "U_CARD_012", "U_CARD_011", "JA77", "JA35" }) await Card(player, id, visualize: true);
        for (int i = 0; i < 5; i++) await Card(player, "JB01", pile: PileType.Draw);
        var rewards = new[] { player.RunState.CreateCard(CardCatalog.Model("JB01"), player) };
        var reward = new CardReward(rewards, CardCreationSource.Other, player,
            new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform));
        _ = reward.SelectUnsynchronized();
    }
}
