using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>The approved D portrait window and its independent printed turn counter.</summary>
public partial class ArrowCardPresentation : Control
{
    // Same 300 x 422 card canvas and 1456 x 1080 sprite geometry as the workbench.
    private static readonly Vector2 SpriteSize = new(1456, 1080);
    private static readonly Vector2 WindowPosition = new(-158.4f, -184.414f);
    private static readonly Vector2 WindowSize = new(318, 243.494f);
    private static readonly Shader RarityShader = new() { Code = """
        shader_type canvas_item;
        uniform int rarity = 0;
        void fragment() {
            vec3 c = COLOR.rgb;
            if (rarity == 1) {
                c = vec3(dot(c, vec3(.354834,.696847,.135333))-.278428,
                    dot(c, vec3(.350061,.687474,.133513))+.115498,
                    dot(c, vec3(.349030,.685449,.133120))+.141191);
            } else if (rarity == 2) {
                c = vec3(dot(c, vec3(.196896,.386678,.075096))+.586800,
                    dot(c, vec3(.401855,.789189,.153267))-.072809,
                    dot(c, vec3(.403124,.791682,.153751))-.439005);
            }
            COLOR = vec4(clamp(c, vec3(0.0), vec3(1.0)), COLOR.a);
        }
        """ };
    private static readonly Vector2[] Aperture =
    [
        new(345,90), new(536,73), new(728,67.5f), new(919,73), new(1110,90),
        new(1205,235), new(1273,350), new(1273,508), new(1218,550), new(1273,593),
        new(1273,700), new(1263,722), new(1233,745), new(954,872), new(884,899),
        new(884,866), new(875,837), new(851,802), new(750,720), new(728,710),
        new(704,720), new(605,802), new(581,837), new(572,866), new(572,899),
        new(502,872), new(222,745), new(190,723), new(180,700), new(180,593),
        new(238,550), new(180,508), new(180,350), new(235,235)
    ];
    private static readonly Vector2[] Badge =
    [
        new(502,886), new(553,886), new(553,821), new(626,734), new(728,691),
        new(830,734), new(903,821), new(903,886), new(954,886), new(954,1058), new(502,1058)
    ];
    private NCard _card = null!;
    private TextureRect _nativePortrait = null!, _nativeBorder = null!;
    private NinePatchRect _nativeType = null!;
    private RichTextLabel _description = null!;
    private Vector2 _descriptionPosition, _descriptionSize;
    private Polygon2D _portrait = null!, _ancientBadge = null!;
    private TextureRect _border = null!;
    private Label _turns = null!, _type = null!;
    private bool _applied;
    private ShaderMaterial _rarityMaterial = null!;

    internal static void Update(NCard card, CardPreviewMode previewMode)
    {
        if (!card.IsNodeReady() || card.Body == null || card.Model == null) return;
        var view = card.Body.GetNodeOrNull<ArrowCardPresentation>("JunnaArrowCard");
        if (view == null && CardCatalog.IsArrow(card.Model))
        {
            view = new ArrowCardPresentation { Name = "JunnaArrowCard", _card = card };
            card.Body.AddChild(view);
            card.Body.MoveChild(view, card.GetNode<Control>("%TitleBanner").GetIndex());
        }
        view?.Refresh(previewMode);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _nativePortrait = _card.GetNode<TextureRect>("%Portrait");
        _nativeBorder = _card.GetNode<TextureRect>("%PortraitBorder");
        _nativeType = _card.GetNode<NinePatchRect>("%TypePlaque");
        _description = _card.GetNode<RichTextLabel>("%DescriptionLabel");
        _descriptionPosition = _description.Position;
        _descriptionSize = _description.Size;
        _portrait = new Polygon2D { Name = "ArrowPortrait", Position = WindowPosition,
            Polygon = Aperture.Select(p => p * WindowSize / SpriteSize).ToArray(), Antialiased = true };
        AddChild(_portrait);
        var texture = GD.Load<Texture2D>(JunnaMod.ResourceRoot + "images/arrow_portrait_frame.png");
        _border = new TextureRect { Name = "ArrowPortraitBorder", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, Texture = texture,
            Position = WindowPosition, Size = WindowSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_border);
        _rarityMaterial = new ShaderMaterial { Shader = RarityShader };
        _border.Material = _rarityMaterial;
        _ancientBadge = new Polygon2D { Name = "AncientArrowBadge", Position = WindowPosition,
            Polygon = Badge.Select(p => p * WindowSize / SpriteSize).ToArray(), UV = Badge, Texture = texture, Antialiased = true };
        AddChild(_ancientBadge);
        var nativeType = _card.GetNode<Label>("%TypeLabel");
        _turns = MakeLabel("Turns", new(-33, -10.55f), new(66, 31.65f), 29,
            _card.GetNode<Label>("%EnergyLabel").GetThemeFont("font"));
        _turns.AddThemeColorOverride("font_outline_color", new Color("4D4B40"));
        _turns.AddThemeConstantOverride("outline_size", 5);
        _type = MakeLabel("ArrowType", new(-42, 23.21f), new(84, 24.48f), 16, nativeType.GetThemeFont("font"));
        _type.AddThemeColorOverride("font_color", new Color(0, 0, 0, .8f));
    }

    private Label MakeLabel(string name, Vector2 position, Vector2 size, int fontSize, Font font)
    {
        var label = new Label { Name = name, Position = position, Size = size, MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        AddChild(label);
        return label;
    }

    private void Refresh(CardPreviewMode previewMode)
    {
        if (!IsNodeReady() || _card.Model is not { } card) return;
        bool arrow = CardCatalog.IsArrow(card);
        Visible = arrow;
        if (!arrow)
        {
            if (_applied)
            {
                _nativePortrait.Visible = _nativeBorder.Visible = card.Rarity != CardRarity.Ancient;
                _nativeType.Visible = true;
                _description.Position = _descriptionPosition;
                _description.Size = _descriptionSize;
                _applied = false;
            }
            return;
        }
        _applied = true;
        bool ancient = card.Rarity == CardRarity.Ancient;
        bool revealed = _card.Visibility == ModelVisibility.Visible;
        _nativePortrait.Visible = _nativeBorder.Visible = _nativeType.Visible = false;
        _portrait.Visible = !ancient && revealed;
        _border.Visible = !ancient;
        _ancientBadge.Visible = ancient;
        _description.Position = new(_descriptionPosition.X, 59.08f);
        _description.Size = new(_descriptionSize.X, 116.05f);
        if (_portrait.Visible)
        {
            _portrait.Texture = card.Portrait;
            var textureSize = card.Portrait.GetSize();
            float cover = Math.Max(SpriteSize.X / textureSize.X, SpriteSize.Y / textureSize.Y);
            var crop = (textureSize - SpriteSize / cover) * .5f;
            _portrait.UV = Aperture.Select(p => crop + p / cover).ToArray();
        }
        _rarityMaterial.SetShaderParameter("rarity", card.Rarity == CardRarity.Rare ? 2 : card.Rarity == CardRarity.Uncommon ? 1 : 0);
        _type.Text = card.Type.ToLocString().GetFormattedText();
        var turnValue = CardCatalog.Get(((JunnaCard)card).DesignId).Values.First(v => v.Name == "Turns");
        int turns = card.DynamicVars["Turns"].IntValue;
        if (previewMode == CardPreviewMode.Upgrade && !card.IsUpgraded)
            turns += turnValue.Upgraded - turnValue.Base;
        _turns.Visible = revealed;
        _turns.Text = turns.ToString();
        int original = turnValue.Base;
        _turns.AddThemeColorOverride("font_color", turns != original ? new Color("7FFF00") : new Color("FFF6E2"));
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class ArrowCardPresentationPatch
{
    private static void Postfix(NCard __instance, CardPreviewMode previewMode) => ArrowCardPresentation.Update(__instance, previewMode);
}
