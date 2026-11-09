using System.Reflection;
using BaseLib.Patches.Content;
using BaseLib.Patches.Localization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna;

[ModInitializer(nameof(Initialize))]
public static class JunnaMod
{
    public const string Id = "ShoujoKagekiJunna";
    public const string ResourceRoot = "res://ShoujoKagekiJunna/";
    public static MegaCrit.Sts2.Core.Logging.Logger Log { get; } = new(Id, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(Assembly.GetExecutingAssembly());
        CoreApi.Get<ICardEffectRouting>().Register(HandArrow.Dispatch, card => Models.CardCatalog.IsArrow(card) ? ArrowPile.PileId : null);
        DamageSourceScopes.Install();
        KnowledgeCombat.Initialize();
        KnowledgeProgress.Initialize();
        DynamicKnowledge.Initialize();
        Models.CardCatalog.RegisterDamageCapabilities();
        Shine.RegisterDeferredResolutionPredicate(Models.CardCatalog.IsArrow);
        new Harmony(Id).PatchAll(Assembly.GetExecutingAssembly());
        ModHelper.SubscribeForCombatStateHooks(Id, _ => [ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>())]);
        DescriptionOverrides.CustomizeDescriptionPost += DynamicKnowledge.Describe;
        Log.Info("Junna 0.1.0 initialized; direct card damage: AttackCommand / CreatureCmd; extra hits once per resolution.");
    }
}
