using Godot;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

[HarmonyPatch(typeof(NCardHolder), "CreateHoverTips")]
internal static class KnowledgeOfferHoverTipsPatch
{
    private static bool Prefix(NCardHolder __instance)
    {
        if (__instance.CardNode?.Model is not JunnaCard card || !KnowledgeCombat.HasGroup(card)) return true;
        for (Node? parent = __instance.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is NCardLibraryGrid or NCardRewardSelectionScreen or NChooseACardSelectionScreen)
            {
                var preview = __instance.CardNode?.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack")?.PreviewModel ?? card;
                NHoverTipSet.CreateAndShow(__instance, preview.HoverTips.Where(tip => tip is not CardHoverTip))
                    ?.SetAlignmentForCardHolder(__instance);
                return false;
            }
        return true;
    }
}

/// <summary>One preview per physical card, including repeated copies of the same model.</summary>
public static class KnowledgePreviewCards
{
    private static readonly ConditionalWeakTable<NCard, List<(Control Control, Control.MouseFilterEnum Filter)>> MouseFilters = new();
    public static IReadOnlyList<CardModel> Create(CardModel source, bool upgradeAll = false)
    {
        var dynamicMembers = DynamicKnowledge.Members(source);
        if (dynamicMembers.Count > 1)
            return dynamicMembers.Select(member =>
            {
                var copy = ShoujoKageki.Core.V1.CardCopies.Snapshot(member);
                if (upgradeAll && !copy.IsUpgraded && copy.IsUpgradable) copy.UpgradeInternal();
                return copy;
            }).ToArray();
        var group = KnowledgeCombat.Group(source);
        if (group == null)
            return DynamicKnowledge.Members(source).Select(c => ShoujoKageki.Core.V1.CardCopies.Snapshot(c)).ToArray();
        var cards = new List<CardModel>();
        bool usedSource = false;
        foreach (var (id, count) in group.Members)
        {
            for (int copy = 0; copy < count; copy++)
            {
                // Keep the offered card's modifications; companion copies are independent, as when acquired.
                bool isSource = !usedSource && id == CardCatalog.DesignId(source);
                var card = ShoujoKageki.Core.V1.CardCopies.Snapshot(isSource ? source : CardCatalog.Model(id));
                usedSource |= isSource;
                if (upgradeAll && !card.IsUpgraded && card.IsUpgradable) card.UpgradeInternal();
                cards.Add(card);
            }
        }
        return cards;
    }

    internal static void IgnoreMouse(NCard card)
    {
        var filters = MouseFilters.GetOrCreateValue(card);
        void Ignore(Node node)
        {
            if (node is Control control)
            {
                filters.Add((control, control.MouseFilter));
                control.MouseFilter = Control.MouseFilterEnum.Ignore;
            }
            foreach (var child in node.GetChildren()) Ignore(child);
        }
        Ignore(card);
    }

    internal static void RestoreMouse(NCard card)
    {
        if (!MouseFilters.TryGetValue(card, out var filters)) return;
        foreach (var (control, filter) in filters)
            if (GodotObject.IsInstanceValid(control)) control.MouseFilter = filter;
        MouseFilters.Remove(card);
    }
}

public partial class KnowledgeCardStack : Control
{
    private NCard _source = null!;
    private CardModel? _model;
    private readonly List<NCard> _companions = [];
    private bool _libraryUpgrade;
    private int _previewIndex;
    private NCard? _sourcePreview;
    private bool _bodyWasVisible;
    internal CardModel? PreviewModel => _previewIndex == 0 ? _model : _companions[_previewIndex - 1].Model;

    internal static bool IsSupported(NCard card)
    {
        for (Node? parent = card.GetParent(); parent != null; parent = parent.GetParent())
        {
            if (parent is KnowledgeCardStack or KnowledgeGroupPreview) return false;
            if (parent is NCardRewardSelectionScreen or NChooseACardSelectionScreen or NChooseABundleSelectionScreen or NMerchantCard or NCardLibraryGrid)
                return true;
        }
        return false;
    }

    internal static void Refresh(NCard card)
    {
        if (!card.IsNodeReady()) return;
        var stack = card.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack");
        bool supported = card.Model != null && card.Model.Pile == null && KnowledgeCombat.HasGroup(card.Model) && IsSupported(card);
        if (!supported)
        {
            stack?.Clear();
            if (stack != null) stack.Visible = false;
            return;
        }
        if (stack == null)
        {
            stack = new KnowledgeCardStack { Name = "JunnaKnowledgeStack", _source = card };
            card.AddChild(stack);
            card.MoveChild(stack, 0);
        }
        stack.Visible = card.Visibility == ModelVisibility.Visible;
        if (stack.Visible) stack.RefreshCards();
        else stack.Clear();
    }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    private void RefreshCards()
    {
        bool inLibrary = false;
        for (Node? node = GetParent(); node != null; node = node.GetParent()) inLibrary |= node is NCardLibraryGrid;
        bool libraryUpgrade = inLibrary && _source.Model!.IsUpgraded;
        if (_model == _source.Model && _libraryUpgrade == libraryUpgrade) return;
        Clear();
        _model = _source.Model;
        _libraryUpgrade = libraryUpgrade;
        var cards = KnowledgePreviewCards.Create(_model!, libraryUpgrade).ToList();
        int sourceIndex = cards.FindIndex(card => CardCatalog.DesignId(card) == CardCatalog.DesignId(_model!));
        if (sourceIndex >= 0) cards.RemoveAt(sourceIndex);
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var companion = NCard.Create(cards[i])!;
            AddChild(companion);
            companion.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            KnowledgePreviewCards.IgnoreMouse(companion);
            LayoutCard(companion, i + 1, cards.Count);
            companion.RotationDegrees = 0;
            companion.Modulate = Colors.White;
            _companions.Insert(0, companion);
        }
    }

    private void LayoutCard(NCard card, int depth, int count)
    {
        bool inMerchant = Holder() is NMerchantCard;
        float step = Math.Min(24, (inMerchant ? 64f : 72f) / Math.Max(1, count));
        card.Position = new(depth * step, -depth * step * .6f);
        card.Scale = Vector2.One * (1 - Math.Min(.12f, .03f * depth));
    }

    private Control? Holder()
    {
        for (Node? parent = _source.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is NCardHolder or NMerchantCard) return (Control)parent;
        return null;
    }

    private bool IsHovered(Control holder)
    {
        for (Node? node = GetViewport().GuiGetHoveredControl(); node != null; node = node.GetParent())
            if (node == holder) return true;
        return false;
    }

    public override void _Process(double delta)
    {
        if (_previewIndex != 0 && (Holder() is not { } holder || !IsHovered(holder))) ResetPreview();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || input is not InputEventMouseButton { Pressed: true } mouse
            || _companions.Count == 0 || Holder() is not { } holder || !IsHovered(holder)) return;
        if (mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown))
        {
            if (mouse.ButtonIndex is MouseButton.Left or MouseButton.Right) ResetPreview();
            return;
        }
        if (_previewIndex == 0)
        {
            _bodyWasVisible = _source.Body.Visible;
            _sourcePreview = NCard.Create(ShoujoKageki.Core.V1.CardCopies.Snapshot(_model!))!;
            AddChild(_sourcePreview);
            _sourcePreview.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            KnowledgePreviewCards.IgnoreMouse(_sourcePreview);
        }
        int count = _companions.Count + 1;
        _previewIndex = (_previewIndex + (mouse.ButtonIndex == MouseButton.WheelDown ? 1 : count - 1)) % count;
        if (_previewIndex == 0) ResetPreview();
        else
        {
            _source.Body.Visible = false;
            for (int depth = count - 1; depth >= 0; depth--)
            {
                int index = (_previewIndex + depth) % count;
                var card = index == 0 ? _sourcePreview! : _companions[index - 1];
                LayoutCard(card, depth, count - 1);
                card.ZIndex = depth == 0 ? 1 : 0;
                MoveChild(card, GetChildCount() - 1);
            }
        }
        NHoverTipSet.Remove(holder);
        var tips = NHoverTipSet.CreateAndShow(holder, PreviewModel!.HoverTips.Where(tip => tip is not CardHoverTip));
        if (holder is NCardHolder cardHolder) tips?.SetAlignmentForCardHolder(cardHolder);
        else tips?.SetAlignment(holder, HoverTip.GetHoverTipAlignment(holder));
        GetViewport().SetInputAsHandled();
    }

    private void ResetPreview()
    {
        if (_sourcePreview != null)
        {
            _source.Body.Visible = _bodyWasVisible;
            RemoveChild(_sourcePreview);
            _sourcePreview.QueueFreeSafely();
            _sourcePreview = null;
        }
        _previewIndex = 0;
        for (int i = _companions.Count - 1; i >= 0; i--)
        {
            var card = _companions[i];
            LayoutCard(card, i + 1, _companions.Count);
            card.ZIndex = 0;
            MoveChild(card, GetChildCount() - 1);
        }
    }

    internal void Clear()
    {
        ResetPreview();
        foreach (var companion in _companions)
        {
            RemoveChild(companion);
            companion.QueueFreeSafely();
        }
        _companions.Clear();
        _model = null;
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class KnowledgeCardVisualsPatch
{
    private static void Postfix(NCard __instance) => KnowledgeCardStack.Refresh(__instance);
}

[HarmonyPatch(typeof(NCard), nameof(NCard.OnFreedToPool))]
internal static class KnowledgeCardPoolPatch
{
    private static void Prefix(NCard __instance)
    {
        // Native cards return to a shared pool, including cards previously used as non-interactive previews.
        KnowledgePreviewCards.RestoreMouse(__instance);
        var stack = __instance.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack");
        stack?.Clear();
        if (stack != null) stack.Visible = false;
    }
}

[HarmonyPatch(typeof(NCardFlyVfx), nameof(NCardFlyVfx._Ready))]
internal static class KnowledgeCardFlightPatch
{
    private static void Prefix(NCard ____card)
    {
        // Native flight animates Body only; offer companions must not follow at full size.
        var stack = ____card.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack");
        stack?.Clear();
        if (stack != null) stack.Visible = false;
    }
}
