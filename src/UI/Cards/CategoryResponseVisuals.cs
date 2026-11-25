using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKageki.Core.V1.Presentation;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal static class CategoryResponseVisuals
{
    private const string MarkerName = "JunnaCategoryResponse";
    private const float MarkerSize = 44f;
    private const float EnergyIconGap = 6f;

    [HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ResolvedBigIconPath), MethodType.Getter)]
    private static class BigIconPatch
    {
        private static void Postfix(PowerModel __instance, ref string __result)
        {
            // Native power flashes use the big icon, whose path is cached on first use.
            if (__instance is JunnaPowerU_CARD_016 power) __result = power.CustomBigIconPath;
        }
    }

    [HarmonyPatch(typeof(NPower), "OnDisplayAmountChanged")]
    private static class PowerPatch
    {
        private static void Prefix(NPower __instance)
        {
            if (!__instance.IsNodeReady() || __instance.Model is not JunnaPowerU_CARD_016 power) return;
            var icon = __instance.GetNode<TextureRect>("%Icon");
            var next = power.Icon;
            if (icon.Texture == next) return;

            icon.Texture = next;
            __instance.GetNode<CpuParticles2D>("%PowerFlash").Texture = next;
            if (NCombatRoom.Instance?.Ui.Hand is not { } hand) return;
            foreach (var holder in hand.ActiveHolders)
                if (holder.CardNode is { } card && card.Model?.Owner == power.Owner.Player)
                    RefreshCard(card, pulse: true);
        }
    }

    internal static void Clear(NCard node) => node.Body?.GetNodeOrNull<TextureRect>(MarkerName)?.Hide();

    internal static void RefreshCard(NCard node, bool pulse = false)
    {
        if (!node.IsNodeReady() || node.Body == null) return;
        var marker = node.Body.GetNodeOrNull<TextureRect>(MarkerName);
        var model = node.Model;
        JunnaPowerU_CARD_016? power = null;
        if (node.DisplayingPile == PileType.Hand && node.Visibility == ModelVisibility.Visible
            && model?.Pile?.Type == PileType.Hand && CombatManager.Instance.IsInProgress
            && NCombatRoom.Instance?.Ui.Hand.GetCard(model) == node)
        {
            power = model.Owner.Creature.Powers.OfType<JunnaPowerU_CARD_016>()
                .FirstOrDefault(candidate => candidate.Amount > 0 && candidate.Matches(model));
            if (power != null && !model.CanPlay(out _, out _)) power = null;
        }
        if (power == null)
        {
            marker?.Hide();
            return;
        }

        if (marker == null)
        {
            marker = new TextureRect
            {
                Name = MarkerName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = Vector2.One * MarkerSize,
            };
            node.Body.AddChild(marker);
        }

        var energyIcon = node.GetNode<Control>("%EnergyIcon");
        var energyTop = energyIcon.GetGlobalTransform() * new Vector2(energyIcon.Size.X * .5f, 0f);
        var anchor = node.Body.GetGlobalTransform().AffineInverse() * energyTop;
        marker.Position = anchor - new Vector2(MarkerSize * .5f, MarkerSize + EnergyIconGap);

        string markerPath = power.CustomPackedIconPath;
        if (marker.Texture?.ResourcePath != markerPath) marker.Texture = GD.Load<Texture2D>(markerPath);
        marker.Show();
        if (pulse && CoreEffects.Enabled)
        {
            marker.SelfModulate = new Color(1.4f, 1.4f, 1.4f);
            marker.CreateTween().TweenProperty(marker, "self_modulate", Colors.White, .16);
        }
    }
}
