using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

// Use the native single-target play flow while allowing this utility card to target either side.
public static class CloserStepTargeting
{
    private static readonly ConditionalWeakTable<NTargetManager, CardModel> Selecting = new();
    public static bool Accepts(CardModel card, Creature? target) => target is { IsAlive: true } &&
        target.CombatState == card.Owner.Creature.CombatState && (target.IsPlayer || target.Side != card.Owner.Creature.Side);

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    private static class CardTarget
    {
        private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
        {
            if (CardCatalog.DesignId(__instance) == "JA119") __result = Accepts(__instance, target);
        }
    }

    [HarmonyPatch]
    private static class BeginSelection
    {
        private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(NTargetManager))
            .Where(m => m.Name == nameof(NTargetManager.StartTargeting));
        private static void Prefix(NTargetManager __instance, object[] __args)
        {
            Selecting.Remove(__instance);
            if (__args.OfType<NCard>().FirstOrDefault()?.Model is { } card && CardCatalog.DesignId(card) == "JA119")
                Selecting.Add(__instance, card);
        }
    }

    [HarmonyPatch(typeof(NTargetManager), "AllowedToTargetCreature")]
    private static class SelectCreature
    {
        private static void Postfix(NTargetManager __instance, Creature creature, ref bool __result)
        {
            if (__instance.IsInSelection && Selecting.TryGetValue(__instance, out var card))
                __result = Accepts(card, creature);
        }
    }
}
