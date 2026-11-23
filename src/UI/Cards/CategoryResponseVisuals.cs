using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using ShoujoKageki.Core.V1.Presentation;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

internal static class CategoryResponseVisuals
{
    private const string MarkerName = "JunnaCategoryResponse";
    private const float MarkerSize = 36f;
    private const float MarkerOffsetX = 36f;

    [HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.SetIndexLabel))]
    private static class HandIndexPatch
    {
        private static void Postfix(NHandCardHolder __instance)
        {
            if (__instance.CardNode is { } card && card.Body?.GetNodeOrNull<TextureRect>(MarkerName) is { } marker)
                PositionMarker(card, marker, __instance.GetNode<Control>("%HandIndex").Visible);
        }
    }

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
                Texture = GD.Load<Texture2D>(JunnaMod.ResourceRoot + "images/response_echo.png"),
            };
            node.Body.AddChild(marker);
            PositionMarker(node, marker, SaveManager.Instance.PrefsSave.ShowCardIndices);
        }
        marker.Show();
        if (pulse && CoreEffects.Enabled)
        {
            marker.SelfModulate = new Color(1.4f, 1.4f, 1.4f);
            marker.CreateTween().TweenProperty(marker, "self_modulate", Colors.White, .16);
        }
    }

    private static void PositionMarker(NCard card, TextureRect marker, bool indexVisible)
    {
        var banner = card.GetNode<Control>("%TitleBanner");
        var bannerTop = banner.GetGlobalTransform() * new Vector2(banner.Size.X * .5f, 0f);
        var anchor = card.Body.GetGlobalTransform().AffineInverse() * bannerTop;
        // Reserve the derived-card label row even when its text is hidden.
        float labelTop = card.Body.GetNodeOrNull<Control>("DerivedLabel")?.Position.Y ?? anchor.Y;
        float offsetX = indexVisible ? MarkerOffsetX : 0f;
        marker.Position = new Vector2(anchor.X + offsetX - MarkerSize * .5f,
            Math.Min(anchor.Y, labelTop) - MarkerSize - 4f);
    }
}
