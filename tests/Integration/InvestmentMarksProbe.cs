using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class InvestmentMarksProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_marks";
    public override string Args => "check | hand";
    public override string Description => "Verify that overlapping cards occlude investment marks.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args) => new(args.FirstOrDefault() == "hand" ? Hand(player!) : Check(player!), true, "Investment mark checks completed");

    private static async Task Hand(Player player)
    {
        foreach (var card in CardActions.All(player).ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
        for (int i = 0; i < 9; i++)
        {
            var card = CardCatalog.Create(i % 2 == 0 ? "JB02" : "JB01", player);
            if (i % 2 != 0) DamageModifiers.Add(card, 6, 6);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }
    }

    private static async Task Check(Player player)
    {
        string directory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../..", "out/tmp/investment-marks"));
        Directory.CreateDirectory(directory);
        var rows = new List<object>();
        void Verify(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        var stage = new Control { Name = "InvestmentMarksProbe", ZIndex = 1000, MouseFilter = Control.MouseFilterEnum.Ignore };
        NGame.Instance!.AddChild(stage);
        stage.AddChild(new ColorRect { Color = new Color("152028"), Size = stage.GetViewportRect().Size, MouseFilter = Control.MouseFilterEnum.Ignore });
        var backHolder = new Control { Position = new(440, 360) };
        var frontHolder = new Control { Position = new(320, 360) };
        stage.AddChild(backHolder); stage.AddChild(frontHolder);
        var model = CardCatalog.Create("JB01", player);
        DamageModifiers.Add(model, 6, 2);
        var back = NCard.Create(model)!;
        var front = NCard.Create(CardCatalog.Create("JB02", player))!;
        backHolder.AddChild(back); frontHolder.AddChild(front);
        back.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        front.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        var marks = back.Body.GetNode<CardInvestmentMarks>("JunnaInvestmentMarks");
        await Task.Delay(300);
        // Map both complete badge regions through the actual card transform to viewport pixels.
        var label = marks.GetNode<Label>("DamageBonus");
        var screen = stage.GetViewport().GetFinalTransform();
        var begin = screen * label.GetGlobalTransformWithCanvas() * Vector2.Zero;
        var hitLabel = marks.GetNode<Label>("ExtraHitCount");
        var end = screen * hitLabel.GetGlobalTransformWithCanvas() * (hitLabel.Size + new Vector2(6, 0));
        int left = (int)begin.X + 2, right = (int)end.X - 2;
        int top = (int)begin.Y + 2, bottom = (int)end.Y - 2;
        var frontTransform = screen * front.GetGlobalTransformWithCanvas();
        File.WriteAllText(Path.Combine(directory, "geometry.json"), JsonSerializer.Serialize(new { left, right, top, bottom, canvas = stage.GetViewportRect().Size.ToString(), front = frontTransform.ToString(), begin = begin.ToString(), end = end.ToString() }));
        Verify("badge region is fully covered by front portrait", left > frontTransform.Origin.X - 135 * frontTransform.X.Length() && right < frontTransform.Origin.X + 135 * frontTransform.X.Length(), true);
        async Task<Image> Capture(bool visible)
        {
            RenderingServer.ViewportSetUpdateMode(stage.GetViewport().GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
            marks.Visible = visible;
            marks.Modulate = new Color(1, 1, 1, visible ? 1 : 0);
            await Task.Delay(90);
            RenderingServer.ForceDraw(false);
            return stage.GetViewport().GetTexture().GetImage();
        }
        async Task<int> Difference(string name)
        {
            using var visible = await Capture(true);
            visible.SavePng(Path.Combine(directory, name + ".png"));
            using var hidden = await Capture(false);
            hidden.SavePng(Path.Combine(directory, name + "-hidden.png"));
            int changed = 0;
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++)
            {
                var delta = visible.GetPixel(x, y) - hidden.GetPixel(x, y);
                if (Mathf.Abs(delta.R) + Mathf.Abs(delta.G) + Mathf.Abs(delta.B) > .08f) changed++;
            }
            return changed;
        }
        try
        {
            Verify("front sibling card occludes bonus and hit badges", await Difference("occluded"), 0);
            frontHolder.ZIndex = 1;
            Verify("hovered front card occludes bonus and hit badges", await Difference("hovered-front"), 0);
            frontHolder.ZIndex = 0; backHolder.ZIndex = 1;
            Verify("hovered invested card shows its own badges", await Difference("hovered-invested") > 30, true);
            backHolder.ZIndex = 0; frontHolder.Visible = false;
            Verify("uncovered bonus and hit badges remain visible", await Difference("uncovered") > 30, true);
            Verify("damage bonus value is retained", label.Text, "+6");
            HoverTip[] Tips(CardModel card) => card.HoverTips.OfType<HoverTip>().Where(tip => tip.Id == "junna_investment").ToArray();
            var both = Tips(model);
            Verify("boosted card has one concise extra tip", both.Length, 1);
            Verify("combined tip describes both current values", both.Single().Description!, "造成伤害增加6点。\n额外造成2次伤害。");
            Verify("unmodified card has no investment tip", Tips(front.Model!).Length, 0);
            var bonusOnly = CardCatalog.Create("JB01", player);
            DamageModifiers.Add(bonusOnly, 2.5m);
            Verify("bonus-only tip", Tips(bonusOnly).Single().Description!, "造成伤害增加2.5点。");
            var hitsOnly = CardCatalog.Create("JB01", player);
            DamageModifiers.Add(hitsOnly, 0, 2);
            Verify("hits-only tip", Tips(hitsOnly).Single().Description!, "额外造成2次伤害。");
            var foreign = player.Creature.CombatState!.CreateCard(ModelDb.Card<ExternalDirectCard>(), player);
            DamageModifiers.Add(foreign, 3, 1);
            Verify("foreign card uses the same investment tip", Tips(foreign).Single().Description!, "造成伤害增加3点。\n额外造成1次伤害。");
            DamageModifiers.Add(model, 2, 1);
            Verify("tip follows later investments", Tips(model).Single().Description!, "造成伤害增加8点。\n额外造成3次伤害。");
            Verify("clone carries its investment explanation", Tips(model.CreateCloneForPlayer(player)).Single().Description!, "造成伤害增加8点。\n额外造成3次伤害。");
        }
        finally { stage.QueueFree(); }
        rows.Add(new { name = "complete", pass = true });
        File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Investment mark occlusion check failed");
    }
}
