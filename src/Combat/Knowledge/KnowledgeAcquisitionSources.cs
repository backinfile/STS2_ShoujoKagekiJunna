using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

internal static class KnowledgeAcquisitionSources
{
    private enum Kind { Deck, DeckChoice, Transform, Discovery, Reward }
    private sealed record Entry(string Type, string Method, Kind Kind);
    // Each entry names the effect that was adapted; other model methods remain single-card acquisitions.
    private static readonly Entry[] Entries =
    [
        new("Relics.ArcaneScroll", "AfterObtained", Kind.Deck),
        new("Relics.DustyTome", "AfterObtained", Kind.Deck),
        new("Relics.HeftyTablet", "AfterObtained", Kind.DeckChoice),
        new("Relics.LeadPaperweight", "AfterObtained", Kind.DeckChoice),
        new("Relics.MassiveScroll", "AfterObtained", Kind.DeckChoice),
        new("Relics.ScrollBoxes", "AfterObtained", Kind.DeckChoice),
        new("Relics.SeaGlass", "AfterObtained", Kind.DeckChoice),
        new("Modifiers.SealedDeck", "ChooseCards", Kind.DeckChoice),
        new("Relics.ArchaicTooth", "AfterObtained", Kind.Transform),
        new("Relics.Astrolabe", "AfterObtained", Kind.Transform),
        new("Relics.Byrdpip", "AfterObtained", Kind.Transform),
        new("Relics.Claws", "AfterObtained", Kind.Transform),
        new("Relics.LeafyPoultice", "AfterObtained", Kind.Transform),
        new("Relics.NewLeaf", "AfterObtained", Kind.Transform),
        new("Relics.PandorasBox", "AfterObtained", Kind.Transform),
        new("Events.AromaOfChaos", "LetGo", Kind.Transform),
        new("Events.EndlessConveyor", "JellyLiver", Kind.Transform),
        new("Events.MorphicGrove", "Group", Kind.Transform),
        new("Events.Symbiote", "KillWithFire", Kind.Transform),
        new("Events.Trial", "NondescriptInnocent", Kind.Transform),
        new("Events.WhisperingHollow", "Hug", Kind.Transform),
        new("Events.WoodCarvings", "Bird", Kind.Transform),
        new("Events.WoodCarvings", "Torus", Kind.Transform),
        new("Events.BrainLeech", "ShareKnowledge", Kind.DeckChoice),
        new("Events.RoomFullOfCheese", "Gorge", Kind.DeckChoice),
        new("Events.BrainLeech", "Rip", Kind.Reward),
        new("Events.ColorfulPhilosophers", "OfferRewards", Kind.Reward),
        new("Events.TheFutureOfPotions", "Trade", Kind.Reward),
        new("Events.Trial", "NondescriptGuilty", Kind.Reward),
        new("Relics.DreamCatcher", "TryModifyRestSiteHealRewards", Kind.Reward),
        new("Relics.GlassEye", "AfterObtained", Kind.Reward),
        new("Relics.Kaleidoscope", "AfterObtained", Kind.Reward),
        new("Relics.LostCoffer", "AfterObtained", Kind.Reward),
        new("Relics.Orrery", "AfterObtained", Kind.Reward),
        new("Relics.PrayerWheel", "TryModifyRewards", Kind.Reward),
        new("Relics.WhiteStar", "TryModifyRewards", Kind.Reward),
        new("Cards.TheHunt", "OnPlay", Kind.Reward),
        new("Relics.ChoicesParadox", "AfterPlayerTurnStart", Kind.Discovery),
        new("Relics.Toolbox", "BeforeHandDraw", Kind.Discovery),
        new("Potions.AttackPotion", "OnUse", Kind.Discovery),
        new("Potions.ColorlessPotion", "OnUse", Kind.Discovery),
        new("Potions.PowerPotion", "OnUse", Kind.Discovery),
        new("Potions.SkillPotion", "OnUse", Kind.Discovery),
        new("Cards.Abundance", "OnPlay", Kind.Discovery),
        new("Cards.Discovery", "OnPlay", Kind.Discovery),
        new("Cards.Quasar", "OnPlay", Kind.Discovery),
        new("Cards.Splash", "OnPlay", Kind.Discovery)
    ];
    private sealed record Context(Entry Entry, object Owner, Func<CardModel, bool>? Accepts = null)
    {
        internal HashSet<CardModel> Selected { get; } = [];
    }
    private static readonly AsyncLocal<Context?> Current = new();
    private static readonly ConditionalWeakTable<CardReward, Entry> RewardOrigins = new();
    private static IDisposable? _models;
    private sealed class Scope(Context? context) : IDisposable
    {
        private readonly Context? _previous = Set(context);
        private bool _disposed;
        private static Context? Set(Context? value) { var previous = Current.Value; Current.Value = value; return previous; }
        public void Dispose() { if (_disposed) return; _disposed = true; Current.Value = _previous; }
    }
    internal static void Initialize()
    {
        if (_models != null) return;
        _models = CoreApi.Get<IModelExecutionScopes>().Register((model, _) =>
            new Scope(ReferenceEquals(Current.Value?.Owner, model) ? Current.Value : null));
        var methods = CoreApi.Get<IModelMethodScopes>();
        foreach (var entry in Entries)
        {
            var type = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models." + entry.Type);
            if (type != null && AccessTools.DeclaredMethod(type, entry.Method) is { } method)
                methods.Register(method, owner => new Scope(new(entry, owner ?? type)));
        }
    }
    internal static bool Allows(CardModel card, PileType destination, bool transforming = false) => Current.Value is { } context
        && (context.Entry.Kind == Kind.Deck && destination == PileType.Deck
            || context.Entry.Kind == Kind.DeckChoice && destination == PileType.Deck && context.Selected.Contains(card)
            || context.Entry.Kind == Kind.Transform && destination == PileType.Deck && transforming
            || context.Entry.Kind == Kind.Discovery && destination.IsCombatPile())
        && (context.Accepts?.Invoke(card) ?? true);
    internal static bool AllowsDiscovery => Current.Value?.Entry.Kind is Kind.DeckChoice or Kind.Discovery;
    internal static void Selected(CardModel card) => Current.Value?.Selected.Add(card);
    internal static bool IsKnownReward(CardReward reward) => RewardOrigins.TryGetValue(reward, out _);

    [HarmonyPatch]
    private static class RewardCreation
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(CardReward).GetConstructors();
        private static void Postfix(CardReward __instance, object[] __args)
        {
            if (Current.Value?.Entry is { Kind: Kind.Reward } entry) RewardOrigins.AddOrUpdate(__instance, entry);
            else if (__args[0] is CardCreationOptions { Source: CardCreationSource.Encounter }
                || __args.OfType<CardCreationSource>().Contains(CardCreationSource.Encounter))
                RewardOrigins.AddOrUpdate(__instance, new("Rewards.CardReward", "Encounter", Kind.Reward));
        }
    }
    [HarmonyPatch(typeof(CardReward), "OnSelect")]
    private static class RewardSelection
    {
        private static void Prefix(CardReward __instance, out IDisposable __state) => __state = new Scope(IsKnownReward(__instance)
            ? new(new("Rewards.CardReward", "OnSelect", Kind.Deck), __instance, card => __instance.Cards.Contains(card)) : null);
        private static void Finalizer(IDisposable? __state) => __state?.Dispose();
    }
#if STS2_BETA
    private const string MerchantPurchaseMethod = "OnTryPurchaseInternal";
#else
    private const string MerchantPurchaseMethod = "OnTryPurchase";
#endif
    [HarmonyPatch(typeof(MerchantCardEntry), MerchantPurchaseMethod)]
    private static class MerchantPurchase
    {
        private static void Prefix(MerchantCardEntry __instance, out IDisposable __state) => __state = new Scope(
            new(new("Merchant.MerchantCardEntry", MerchantPurchaseMethod, Kind.Deck), __instance, card => __instance.CreationResult?.Card == card));
        private static void Finalizer(IDisposable? __state) => __state?.Dispose();
    }
    [HarmonyPatch]
    private static class SelectedDeckCards
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CardSelectCmd), "FromChooseABundleScreen");
            yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CardSelectCmd), "FromSimpleGridForRewards");
            yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CardSelectCmd), "FromSimpleGrid");
        }
        private static void Postfix(object[] __args, ref Task<IEnumerable<CardModel>> __result) => __result = Complete(__result, __args);
        private static async Task<IEnumerable<CardModel>> Complete(Task<IEnumerable<CardModel>> task, object[] args)
        {
            var selected = (await task).ToArray();
            if (Current.Value?.Entry.Kind is Kind.DeckChoice or Kind.Discovery)
            {
                var options = args.OfType<IReadOnlyList<CardModel>>().FirstOrDefault()
                    ?? args.OfType<IReadOnlyList<CardCreationResult>>().FirstOrDefault()?.Select(result => result.Card).ToArray();
                var context = args.OfType<MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext>().FirstOrDefault()
                    ?? new MegaCrit.Sts2.Core.GameActions.Multiplayer.BlockingPlayerChoiceContext();
                foreach (var card in selected.Where(card => card.Pile == null))
                {
                    Selected(card);
                    if (options != null) await DynamicKnowledge.Prepare(context, card, options);
                }
            }
            return selected;
        }
    }
}
