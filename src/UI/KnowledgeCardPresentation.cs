using Godot;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

/// <summary>One preview per physical card, including repeated copies of the same model.</summary>
public static class KnowledgePreviewCards
{
    private static readonly ConditionalWeakTable<NCard, List<(Control Control, Control.MouseFilterEnum Filter)>> MouseFilters = new();
    public static IReadOnlyList<CardModel> Create(CardModel source, bool upgradeAll = false)
    {
        var group = KnowledgeCombat.Group(source);
        if (group == null) return [];
        var cards = new List<CardModel>();
        bool usedSource = false;
        foreach (var id in group.GrantedCardIds.Prepend(group.MainCardId))
        {
            int count = id == group.MainCardId ? 1 : group.GrantedCardCounts.GetValueOrDefault(id, 1);
            for (int copy = 0; copy < count; copy++)
            {
                // Keep the offered card's modifications; companion copies are independent, as when acquired.
                bool isSource = !usedSource && id == CardCatalog.DesignId(source);
                var card = (CardModel)(isSource ? source : CardCatalog.Model(id)).MutableClone();
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

    internal static bool IsSupported(NCard card)
    {
        for (Node? parent = card.GetParent(); parent != null; parent = parent.GetParent())
        {
            if (parent is KnowledgeCardStack or KnowledgeGroupPreview) return false;
            if (parent is NCardRewardSelectionScreen or NChooseACardSelectionScreen or NMerchantCard or NCardLibraryGrid)
                return true;
        }
        return false;
    }

    internal static void Refresh(NCard card)
    {
        if (!card.IsNodeReady()) return;
        var stack = card.GetNodeOrNull<KnowledgeCardStack>("JunnaKnowledgeStack");
        bool supported = card.Model != null && card.Model.Pile == null && KnowledgeCombat.Group(card.Model) != null && IsSupported(card);
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
            float step = Math.Min(24, 72f / Math.Max(1, cards.Count));
            companion.Position = new((i + 1) * step, -(i + 1) * step * .6f);
            companion.RotationDegrees = 0;
            companion.Scale = Vector2.One * (1 - Math.Min(.12f, .03f * (i + 1)));
            companion.Modulate = Colors.White;
            _companions.Add(companion);
        }
    }

    internal void Clear()
    {
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
