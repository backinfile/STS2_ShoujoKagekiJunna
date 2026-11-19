using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShoujoKageki.Core.V1.Presentation;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal static class CategoryResponseVisuals
{
    private const string MarkerName = "JunnaCategoryResponse";
    private const float MarkerSize = 52f;

    [HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
    private static class CardPatch
    {
        private static void Postfix(NCard __instance) => RefreshCard(__instance);
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

    private static void RefreshCard(NCard node, bool pulse = false)
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
            var banner = node.GetNode<Control>("%TitleBanner");
            var anchor = banner.GetGlobalTransform() * new Vector2(banner.Size.X * .56f, 0f);
            marker = new TextureRect
            {
                Name = MarkerName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = Vector2.One * MarkerSize,
                Position = node.Body.GetGlobalTransform().AffineInverse() * anchor
                    - new Vector2(MarkerSize * .5f, MarkerSize * .99f),
            };
            node.Body.AddChild(marker);
        }
        marker.Texture = power.Icon;
        marker.Show();
        if (pulse && CoreEffects.Enabled)
        {
            marker.SelfModulate = new Color(1.4f, 1.4f, 1.4f);
            marker.CreateTween().TweenProperty(marker, "self_modulate", Colors.White, .16);
        }
    }
}
