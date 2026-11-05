using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.UI;

/// <summary>Shared stacked and grid inspection for knowledge groups.</summary>
public partial class KnowledgeGroupPreview : Control
{
    private static readonly ConditionalWeakTable<List<CardModel>, object> Groups = new();
    private readonly List<NCard> _backingCards = [];
    private readonly List<(Control Slot, NCard Card)> _gridCards = [];
    private NInspectCardScreen _screen = null!;
    private NCard _front = null!;
    private Control _stack = null!;
    private Label _counter = null!;
    private NCardViewSortButton _layoutButton = null!;
    private NCardRewardAlternativeButton _confirmButton = null!;
    private NCardRewardAlternativeButton _cancelButton = null!;
    private Action? _confirmSelection;
    private ScrollContainer _scroll = null!;
    private GridContainer _grid = null!;
    private List<CardModel>? _cards;
    private int _index;
    private bool _upgraded;
    private bool _spread;

    internal static void OpenForSelection(CardModel card, Action confirm)
    {
        var screen = NGame.Instance!.GetInspectCardScreen();
        screen.Open([card], 0);
        var preview = screen.GetNode<KnowledgeGroupPreview>("JunnaKnowledgePreview");
        preview._confirmSelection = confirm;
        preview._spread = true;
        preview.RenderCards();
    }

    internal static void Prepare(ref List<CardModel> cards, ref int index)
    {
        if (Groups.TryGetValue(cards, out _)) return;
        var source = cards[index];
        if (source.Pile != null || KnowledgeCombat.Group(source) == null) return;
        cards = KnowledgePreviewCards.Create(source).ToList();
        index = Math.Max(0, cards.FindIndex(card => CardCatalog.DesignId(card) == CardCatalog.DesignId(source)));
        Groups.Add(cards, new object());
    }

    internal static void Refresh(NInspectCardScreen screen, NCard front, List<CardModel> cards, int index, bool upgraded)
    {
        var preview = screen.GetNodeOrNull<KnowledgeGroupPreview>("JunnaKnowledgePreview");
        if (!Groups.TryGetValue(cards, out _))
        {
            preview?.Reset();
            return;
        }
        if (preview == null)
        {
            preview = new KnowledgeGroupPreview { Name = "JunnaKnowledgePreview", _screen = screen, _front = front };
            screen.AddChild(preview);
        }
        if (preview._cards != cards) preview._spread = false;
        preview._cards = cards;
        preview._index = index;
        preview._upgraded = upgraded;
        preview.Visible = true;
        preview.RenderCards();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _stack = new Control { Name = "JunnaInspectStack", MouseFilter = MouseFilterEnum.Ignore };
        _front.AddChild(_stack);
        _front.MoveChild(_stack, 0);
        _counter = new Label
        {
            Name = "CopyIndex", Position = new(-45, 212), Size = new(90, 18),
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore
        };
        _counter.AddThemeFontSizeOverride("font_size", 12);
        _counter.AddThemeColorOverride("font_color", new Color("D0B775"));
        _stack.AddChild(_counter);
        _layoutButton = CreateButton("LayoutToggle", () =>
        {
            _spread = !_spread;
            RenderCards();
        });
        _confirmButton = CreateChoiceButton("Confirm", "JUNNA.KNOWLEDGE_CONFIRM", GameApi.ConfirmHotkey, () =>
        {
            var confirm = _confirmSelection;
            if (confirm == null) return;
            _screen.Close();
            confirm();
        });
        _cancelButton = CreateChoiceButton("Cancel", "JUNNA.KNOWLEDGE_CANCEL", MegaInput.cancel, _screen.Close);
        _scroll = new ScrollContainer
        {
            Name = "Cards", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(_scroll);
        _grid = new GridContainer { Name = "Grid", MouseFilter = MouseFilterEnum.Pass };
        _grid.AddThemeConstantOverride("h_separation", 28);
        _grid.AddThemeConstantOverride("v_separation", 28);
        var padding = new MarginContainer { MouseFilter = MouseFilterEnum.Pass };
        foreach (string edge in new[] { "left", "top", "right", "bottom" })
            padding.AddThemeConstantOverride($"margin_{edge}", 80);
        _scroll.AddChild(padding);
        padding.AddChild(_grid);
        Resized += LayoutCards;
    }

    private NCardViewSortButton CreateButton(string name, Action released)
    {
        var button = ResourceLoader.Load<PackedScene>("res://scenes/screens/card_library/library_sort_button.tscn")
            .Instantiate<NCardViewSortButton>();
        button.Name = name;
        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => released()));
        AddChild(button);
        button.GetNode<TextureRect>("%Image").Hide();
        return button;
    }

    private NCardRewardAlternativeButton CreateChoiceButton(string name, string label, string hotkey, Action released)
    {
        var button = NCardRewardAlternativeButton.Create(new LocString("gameplay_ui", label).GetFormattedText(), hotkey);
        button.Name = name;
        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => released()));
        AddChild(button);
        return button;
    }

    private NCard CreatePreview(CardModel source, Node parent)
    {
        var model = (CardModel)source.MutableClone();
        if (_upgraded && !model.IsUpgraded && model.IsUpgradable) model.UpgradeInternal();
        else if (!_upgraded && model.IsUpgraded) CardCmd.Downgrade(model);
        var card = NCard.Create(model)!;
        parent.AddChild(card);
        card.UpdateVisuals(PileType.None, _upgraded ? CardPreviewMode.Upgrade : CardPreviewMode.Normal);
        KnowledgePreviewCards.IgnoreMouse(card);
        return card;
    }

    private void RenderCards()
    {
        if (_cards == null) return;
        ClearCards();
        _front.Visible = !_spread;
        _stack.Visible = !_spread;
        _scroll.Visible = _spread;
        _layoutButton.Visible = _confirmSelection == null;
        _confirmButton.Visible = _cancelButton.Visible = _confirmSelection != null;
        _counter.Text = $"{_index + 1} / {_cards.Count}";
        _layoutButton.SetLabel(new LocString("gameplay_ui", _spread ? "JUNNA.KNOWLEDGE_STACK_LAYOUT" : "JUNNA.KNOWLEDGE_SPREAD_LAYOUT").GetFormattedText());
        var left = _screen.GetNode<NButton>("LeftArrow");
        var right = _screen.GetNode<NButton>("RightArrow");
        left.Visible = !_spread && _index > 0;
        right.Visible = !_spread && _index < _cards.Count - 1;
        left.MouseFilter = left.Visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        right.MouseFilter = right.Visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        NHoverTipSet.Remove(_screen);
        if (_spread)
        {
            foreach (var model in _cards)
            {
                var slot = new Control { MouseFilter = MouseFilterEnum.Pass };
                _grid.AddChild(slot);
                var card = CreatePreview(model, slot);
                var hoverBounds = new Control { MouseFilter = MouseFilterEnum.Ignore };
                slot.AddChild(hoverBounds);
                Tween? hoverTween = null;
                slot.MouseEntered += () =>
                {
                    hoverTween?.Kill();
                    card.Scale = slot.CustomMinimumSize.X / 300 * Vector2.One * 1.25f;
                    card.ZIndex = 1;
                    hoverBounds.Position = -slot.CustomMinimumSize * .125f - new Vector2(8, 0);
                    hoverBounds.Size = slot.CustomMinimumSize * 1.25f + new Vector2(16, 0);
                    NHoverTipSet.CreateAndShow(slot, card.Model!.HoverTips.Where(tip => tip is not CardHoverTip))
                        ?.SetAlignment(hoverBounds, HoverTip.GetHoverTipAlignment(slot));
                };
                slot.MouseExited += () =>
                {
                    hoverTween?.Kill();
                    hoverTween = card.CreateTween();
                    hoverTween.TweenProperty(card, "scale", slot.CustomMinimumSize.X / 300 * Vector2.One, .5)
                        .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
                    card.ZIndex = 0;
                    NHoverTipSet.Remove(slot);
                };
                _gridCards.Add((slot, card));
            }
        }
        else
        {
            float step = Math.Min(20, 40f / Math.Max(1, _cards.Count - 1));
            for (int depth = _cards.Count - 1; depth > 0; depth--)
            {
                var card = CreatePreview(_cards[(_index + depth) % _cards.Count], _stack);
                card.Position = new(step * depth, -step * depth * .8f);
                card.Scale = Vector2.One * (1 - Math.Min(.12f, .04f * depth));
                _backingCards.Add(card);
            }
            var hoverRect = _screen.GetNode<Control>("HoverTipRect");
            NHoverTipSet.CreateAndShow(_screen, _front.Model!.HoverTips.Where(tip => tip is not CardHoverTip))
                ?.SetAlignment(hoverRect, HoverTip.GetHoverTipAlignment(_screen));
        }
        LayoutCards();
    }

    private void LayoutCards()
    {
        _layoutButton.Position = new(Size.X - 290, Size.Y - 98);
        _layoutButton.Size = new(210, 54);
        float choicesWidth = _cancelButton.Size.X + _confirmButton.Size.X + 32;
        float choicesY = _screen.GetNode<Control>("%Upgrade").GetGlobalRect().Position.Y - GlobalPosition.Y
            - Math.Max(_cancelButton.Size.Y, _confirmButton.Size.Y) - 32;
        _cancelButton.Position = new((Size.X - choicesWidth) / 2, choicesY);
        _confirmButton.Position = _cancelButton.Position + new Vector2(_cancelButton.Size.X + 32, 0);
        if (_gridCards.Count == 0) return;
        int count = _gridCards.Count;
        int columns = count <= 3 ? count : Math.Min(4, (int)Math.Ceiling(Math.Sqrt(count)));
        int rows = (count + columns - 1) / columns;
        float availableWidth = Math.Max(300, Size.X - 200);
        float availableHeight = Math.Max(422, Size.Y - 230);
        float scale = rows == 1 ? Math.Clamp(Size.Y / 800, .9f, 1.35f) : Math.Clamp((availableHeight - 96) / 844, .85f, 1.15f);
        scale = Math.Min(scale, (availableWidth - (columns - 1) * 28 - 160) / (columns * 300));
        var cardSize = new Vector2(300, 422) * scale;
        float width = columns * cardSize.X + (columns - 1) * 28;
        float height = rows * cardSize.Y + (rows - 1) * 28;
        _grid.Columns = columns;
        _grid.CustomMinimumSize = new(width, height);
        float viewportWidth = MathF.Ceiling(width) + 160;
        float viewportHeight = Math.Min(MathF.Ceiling(height) + 160, availableHeight);
        _scroll.Position = new((Size.X - viewportWidth) / 2, Math.Max(80, (Size.Y - viewportHeight) / 2 - 30));
        _scroll.Size = new(viewportWidth, viewportHeight);
        foreach (var (slot, card) in _gridCards)
        {
            slot.CustomMinimumSize = cardSize;
            card.Position = cardSize / 2;
            card.Scale = Vector2.One * scale;
        }
    }

    private void ClearCards()
    {
        foreach (var card in _backingCards)
        {
            _stack.RemoveChild(card);
            card.QueueFreeSafely();
        }
        _backingCards.Clear();
        foreach (var (slot, card) in _gridCards)
        {
            NHoverTipSet.Remove(slot);
            slot.RemoveChild(card);
            card.QueueFreeSafely();
            _grid.RemoveChild(slot);
            slot.QueueFree();
        }
        _gridCards.Clear();
    }

    internal void Reset()
    {
        ClearCards();
        Visible = false;
        _stack.Visible = false;
        _front.Visible = !_spread;
        _cards = null;
        _spread = false;
        _confirmSelection = null;
        _confirmButton.Hide();
        _cancelButton.Hide();
        _scroll.ScrollVertical = 0;
    }

    public override void _Input(InputEvent input)
    {
        if (IsVisibleInTree() && !NDevConsole.Instance.IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(_screen))
            HandleInput(input);
    }

    internal bool HandleInput(InputEvent input)
    {
        if (!IsVisibleInTree() || _spread || input is not InputEventMouseButton { Pressed: true } mouse ||
            mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)) return false;
        var button = _screen.GetNode<NButton>(mouse.ButtonIndex == MouseButton.WheelDown ? "RightArrow" : "LeftArrow");
        if (button.Visible) button.ForceClick();
        GetViewport().SetInputAsHandled();
        return true;
    }
}

[HarmonyPatch(typeof(NInspectCardScreen), "UpdateCardDisplay")]
internal static class KnowledgeInspectPatch
{
    private static void Prefix(ref List<CardModel> ____cards, ref int ____index) => KnowledgeGroupPreview.Prepare(ref ____cards, ref ____index);
    private static void Postfix(NInspectCardScreen __instance, NCard ____card, List<CardModel> ____cards, int ____index) =>
        KnowledgeGroupPreview.Refresh(__instance, ____card, ____cards, ____index, __instance.GetNode<NTickbox>("%Upgrade").IsTicked);
}

[HarmonyPatch(typeof(NInspectCardScreen), nameof(NInspectCardScreen.Close))]
internal static class KnowledgeInspectClosePatch
{
    private static void Prefix(NInspectCardScreen __instance) =>
        __instance.GetNodeOrNull<KnowledgeGroupPreview>("JunnaKnowledgePreview")?.Reset();
}
