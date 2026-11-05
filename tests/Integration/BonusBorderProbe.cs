using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class BonusBorderProbe : AbstractConsoleCmd
{
    public override string CmdName => "junna_bonus_border";
    public override string Args => "check | stage | clear | hand";
    public override string Description => "Targeted bonus history, frame particles and left rail checks.";
    public override bool IsNetworked => false;
    private static Control? _stage;
    private static readonly List<NCard> Views = [];
    private static string DirectoryPath => Path.GetFullPath(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../..", "out/tmp/card-bonus-border"));

    public override CmdResult Process(Player? player, string[] args)
    {
        switch (args.FirstOrDefault())
        {
            case "clear": _stage?.QueueFree(); _stage = null; Views.Clear(); return new(true, "Preview closed");
            case "stage": Stage(player!); return new(true, "Bonus states shown");
            case "hand": return new(Hand(player!), true, "Demo hand prepared");
            default: return new(Check(player!), true, "Bonus checks started");
        }
    }

    private static void Stage(Player player)
    {
        _stage?.QueueFree(); Views.Clear();
        _stage = new Control { Name = "BonusBorderStage", ZIndex = 1000, MouseFilter = Control.MouseFilterEnum.Ignore };
        NGame.Instance!.AddChild(_stage);
        _stage.AddChild(new ColorRect { Color = new Color("152028"), Size = _stage.GetViewportRect().Size, MouseFilter = Control.MouseFilterEnum.Ignore });
        (string id, int bonus, int hits)[] cases = [("JB01", 0, 0), ("JB01", 6, 0), ("JB01", 0, 1), ("JB01", 6, 3),
            ("JB03", 6, 0), ("JB03", 0, 1), ("JB03", 6, 12), ("JB01", 6, 0)];
        for (int i = 0; i < cases.Length; i++)
        {
            var (id, bonus, hits) = cases[i];
            var card = CardCatalog.Create(id, player);
            if (bonus != 0 || hits != 0) DamageModifiers.Add(card, bonus, hits);
            if (i == 7) DamageModifiers.TransferBonus([card], CardCatalog.Create("JB01", player));
            var view = NCard.Create(card)!;
            _stage.AddChild(view);
            view.Position = new(420 + i % 4 * 360, 305 + i / 4 * 470);
            view.Scale = Vector2.One * .93f;
            view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            Views.Add(view);
        }
    }

    private static async Task Check(Player player)
    {
        var rows = new List<object>();
        void Verify(string name, object actual, object expected) => rows.Add(new { name, actual, expected, pass = Equals(actual, expected) });
        bool History(CardModel card) => (bool)AccessTools.Method(typeof(DamageModifiers), "HasReceivedBonus").Invoke(null, [card])!;
        Stage(player);
        await Task.Delay(1000);
        var clean = Views[0];
        Verify("unmodified card has no frame effect", clean.Body.GetNode<CardBonusVfx>("JunnaBonusVfx").Visible, false);
        DamageModifiers.Add(clean.Model!, 0, 0);
        Verify("zero grant does not mark card as invested", History(clean.Model!), false);
        for (int i = 1; i < Views.Count; i++)
        {
            var effect = Views[i].Body.GetNode<CardBonusVfx>("JunnaBonusVfx");
            Verify($"state {i} retains received-bonus history", effect.Visible, true);
            Verify($"state {i} follows body draw order", effect.ZIndex, 0);
            Verify($"state {i} particles follow card transforms", effect.GetChildren().OfType<CpuParticles2D>().All(p => p.LocalCoords), true);
            if (i == 7) continue;
            var marks = Views[i].Body.GetNode<CardInvestmentMarks>("JunnaInvestmentMarks");
            Verify($"state {i} rail begins on left", marks.Position.X, -122f);
        }
        Verify("transferred source has zero current bonus", DamageModifiers.CaptureDamageModifierSnapshot(Views[7].Model!).DamageBonus, 0m);
        Verify("transferred source hides numeric rail", Views[7].Body.GetNode<CardInvestmentMarks>("JunnaInvestmentMarks").Visible, false);
        var clone = Views[7].Model!.CreateCloneForPlayer(player);
        Verify("clone inherits received-bonus history after transfer", History(clone), true);
        Verify("new generated card has independent history", History(CardCatalog.Create("JB01", player)), false);
        var invested = Views[1];
        invested.Visibility = ModelVisibility.NotSeen;
        invested.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        Verify("unrevealed card hides frame effect", invested.Body.GetNode<CardBonusVfx>("JunnaBonusVfx").Visible, false);
        invested.Visibility = ModelVisibility.Visible;
        invested.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        Verify("revealed card restores frame effect", invested.Body.GetNode<CardBonusVfx>("JunnaBonusVfx").Visible, true);
        var saved = invested.Model!;
        invested.Model = CardCatalog.Create("JB01", player);
        invested.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        Verify("reused card node clears old frame effect", invested.Body.GetNode<CardBonusVfx>("JunnaBonusVfx").Visible, false);
        invested.Model = saved;
        invested.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        System.IO.Directory.CreateDirectory(DirectoryPath);
        await Task.Delay(350);
        NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(DirectoryPath, "bonus-states.png"));
        AccessTools.Method(typeof(DamageModifiers), "ClearCombat").Invoke(null, []);
        await Task.Delay(100);
        Verify("combat boundary clears received-bonus history", History(saved), false);
        Verify("combat boundary removes all frame effects", Views.All(v => !v.Body.GetNode<CardBonusVfx>("JunnaBonusVfx").Visible), true);
        File.WriteAllText(Path.Combine(DirectoryPath, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        if (rows.Any(row => !(bool)row.GetType().GetProperty("pass")!.GetValue(row)!)) throw new InvalidOperationException("Bonus border check failed");
        Stage(player);
    }

    private static async Task Hand(Player player)
    {
        _stage?.QueueFree(); _stage = null; Views.Clear();
        foreach (var card in CardActions.All(player).ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card); }
        foreach (string id in new[] { "JB01", "JB03", "JG09", "JG09", "JG03", "JG03" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), PileType.Hand, player);
    }
}
