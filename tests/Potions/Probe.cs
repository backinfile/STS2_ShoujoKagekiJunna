using System.Text.Json;
using BaseLib.Utils.Attributes;
using BaseLib.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;
using ShoujoKagekiJunna.UI;

namespace PotionChecks;

[ModInitializer(nameof(Initialize))]
public static class Startup
{
    public static void Initialize()
    {
        ModHelper.SubscribeForCombatStateHooks("Potions", _ => [ModelDb.GetById<Observer>(ModelDb.GetId<Observer>())]);
        new Harmony("Junna.Potions.Observe").Patch(
            AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen)),
            prefix: new HarmonyMethod(typeof(BundleObserver), nameof(BundleObserver.Prefix)),
            postfix: new HarmonyMethod(typeof(BundleObserver), nameof(BundleObserver.Postfix)));
        new Harmony("Junna.Potions.ObserveFlight").Patch(
            AccessTools.Method(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create), [typeof(NCard), typeof(PileType), typeof(bool), typeof(string)]),
            prefix: new HarmonyMethod(typeof(BundleObserver), nameof(BundleObserver.ObserveFlight)));
        var tree = (SceneTree)Engine.GetMainLoop();
        RenderingServer.ViewportSetUpdateMode(tree.Root.GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
        long last = 0;
        tree.ProcessFrame += () => {
            RenderingServer.ForceDraw(false);
            if (Case.Active is not { } active || System.Environment.TickCount64 - last < 150) return;
            last = System.Environment.TickCount64; active.Save();
        };
    }
}

[CustomID("JUNNA_POTION_OBSERVER")]
public sealed class Observer : AbstractModel
{
    public override bool ShouldReceiveCombatHooks => true;
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (Case.Active is { } active && play.Card.Owner == active.Player)
            active.Played.Add(play.Card);
        return Task.CompletedTask;
    }
    public override Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        if (Case.Active is { } active && potion.Owner == active.Player)
        {
            active.Used.Add(potion.Id.Entry);
            if (potion is JunnaKnowledgePotion)
                active.Selected = active.Player.PlayerCombatState!.AllCards
                    .Where(card => card.Pile != null && !active.Roles.ContainsKey(card)).ToArray();
        }
        return Task.CompletedTask;
    }
}

public static class BundleObserver
{
    public static void ObserveFlight(PileType pileType)
    {
        if (Case.Active is { } active && active.Bundles.Count > 0) active.Flights.Add(pileType.ToString());
    }
    public static void Prefix(IReadOnlyList<CardModel> cards)
    {
        if (Case.Active is not { } active) return;
        active.RootOptions = cards.ToArray();
        active.Bundles = cards.Select(root => KnowledgeCombat.HasGroup(root)
            ? KnowledgePreviewCards.Create(root) : (IReadOnlyList<CardModel>)new[] { root }).ToArray();
    }
    public static void Postfix(ref Task<CardModel?> __result)
    {
        if (Case.Active is { } active) __result = Observe(__result, active);
    }
    private static async Task<CardModel?> Observe(Task<CardModel?> task, Case active)
    {
        var card = await task; active.SelectedRoot = card; return card;
    }
}

public sealed class Case(Player player, string mode, string caseId)
{
    public static Case? Active;
    public Player Player { get; } = player;
    public Dictionary<CardModel, string> Roles { get; } = [];
    public List<CardModel> Played { get; } = [];
    public List<string> Used { get; } = [];
    public List<string> Flights { get; } = [];
    public IReadOnlyList<IReadOnlyList<CardModel>> Bundles = [];
    public CardModel[] Selected = [];
    public CardModel[] RootOptions = [];
    public CardModel? SelectedRoot;
    public string[][] ExpectedFamilies = [];
    private readonly Dictionary<CardModel, int> _ids = [];
    private int Id(CardModel c) { if (!_ids.TryGetValue(c, out int id)) _ids[c] = id = _ids.Count; return id; }
    private object Card(CardModel c) => new { instance = Id(c), id = c.Id.Entry, designId = CardCatalog.DesignId(c),
        role = Roles.GetValueOrDefault(c, "generated"), pile = c.Pile?.Type.ToString(),
        removed = c.HasBeenRemovedFromState, cost = c.EnergyCost.GetWithModifiers(CostModifiers.All),
        printedCost = c.EnergyCost.GetWithModifiers(CostModifiers.None),
        damageCapable = c.DynamicVars.Any(pair => pair.Value is MegaCrit.Sts2.Core.Localization.DynamicVars.DamageVar), arrow = CardCatalog.IsArrow(c),
        deployed = ArrowCombat.Find(c) is { } a ? new { a.Remaining, a.Sequence, a.PlayCount } : null };
    private static IEnumerable<Node> VisibleNodes(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in VisibleNodes(child)) yield return descendant;
    }
    public void Save()
    {
        var state = Player.PlayerCombatState!;
        var cards = CardActions.All(Player).Concat(state.PlayPile.Cards).Concat(Roles.Keys).Concat(RootOptions).Concat(Bundles.SelectMany(b => b)).Distinct();
        var snapshot = new { caseId, mode, turn = state.TurnNumber, energy = state.Energy,
            pool = Player.Character.PotionPool.AllPotions.Select(p => new { id=p.Id.Entry, rarity=p.Rarity.ToString(), title=p.Title.GetFormattedText() }),
            potions = Player.Potions.Select(p => p?.Id.Entry).ToArray(),
            deckCount = Player.Deck.Cards.Count, flights = Flights.ToArray(),
            cards = cards.Select(Card).ToArray(), played = Played.Select(Id).ToArray(), used = Used.ToArray(),
            rootOptions = RootOptions.Select(Id).ToArray(), selectedRoot = SelectedRoot == null ? (int?)null : Id(SelectedRoot),
            existingKnowledgeStacks = VisibleNodes(((SceneTree)Engine.GetMainLoop()).Root).OfType<KnowledgeCardStack>().Count(node => node.IsVisibleInTree()),
            nativeBundleNodes = VisibleNodes(((SceneTree)Engine.GetMainLoop()).Root).OfType<NCardBundle>().Count(node => node.IsVisibleInTree()),
            bundles = Bundles.Select(b => b.Select(Id).ToArray()).ToArray(), selected = Selected.Select(Id).ToArray(), expectedFamilies = ExpectedFamilies };
        var directory = System.Environment.GetEnvironmentVariable("JUNNA_POTION_OUTPUT")!;
        File.WriteAllText(Path.Combine(directory,"live.json"),JsonSerializer.Serialize(snapshot,new JsonSerializerOptions { WriteIndented=true }));
    }
}

public sealed class PotionCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_potions";
    public override string Args => "prepare <knowledge|capacity|dynamic|sports|arrows> <caseId>";
    public override string Description => "Prepare potion fixtures before recording; observe native potion and card actions.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false,"Combat required");
        return new(Prepare(player,args.ElementAtOrDefault(1)??"knowledge",args.ElementAtOrDefault(2)??"test"),true,"Preparing potion fixture");
    }
    private static async Task Prepare(Player player,string mode,string caseId)
    {
        Case.Active=null;
        var relationField=(SavedSpireField<Player,string>)AccessTools.Field(typeof(DynamicKnowledge),"Relations").GetValue(null)!;
        relationField.Set(player,"");
        foreach(var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach(var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card,skipVisuals:false); }
        foreach(var potion in player.Potions.OfType<PotionModel>().ToArray()) await PotionCmd.Discard(potion);
        await CreatureCmd.SetMaxHp(player.Creature,1000); await CreatureCmd.SetCurrentHp(player.Creature,1000);
        foreach(var enemy in player.Creature.CombatState!.HittableEnemies)
        {
            foreach(var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy,5000); await CreatureCmd.SetCurrentHp(enemy,5000);
        }
        player.PlayerCombatState!.Energy=3;
        var active=new Case(player,mode,caseId);
        using var single=KnowledgeCombat.SingleCard();
        async Task<CardModel> Add(string id,string role,PileType pile)
        {
            var card=CardCatalog.Create(id,player); active.Roles[card]=role;
            if (pile == PileType.Hand) await CardPileCmd.AddGeneratedCardToCombat(card,pile,player);
            else await CardPileCmd.Add(card,pile,CardPilePosition.Bottom,skipVisuals:true);
            return card;
        }
        for(int i=0;i<30;i++) await Add("JB02","filler",PileType.Draw);
        if(mode=="dynamic")
        {
            var roots=ModelDb.CardPool<JunnaCardPool>().AllCards
                .Where(c => CardCatalog.IsRewardCard(c) && CardCatalog.IsKnowledge(c))
                .Concat(new[] { CardCatalog.Model("JS35"), CardCatalog.Model("JA38") }).Distinct().ToArray();
            var families=Enumerable.Range(0,3).Select(bucket => roots.Where((_,index) => index%3==bucket).ToArray()).ToArray();
            var relations=families.SelectMany(family => family.Select(root =>
                new KnowledgeRelation(root.Id.ToString(),family.ToDictionary(c => c.Id.ToString(),_ => 1)))).ToList();
            relationField.Set(player,JsonSerializer.Serialize(relations));
            active.ExpectedFamilies=families.Select(family => family.Select(c => c.Id.Entry).ToArray()).ToArray();
        }
        if(mode is "knowledge" or "capacity")
            for(int i=0;i<(mode=="capacity"?10:2);i++) await Add("JB02","original",PileType.Hand);
        if(mode=="sports")
        {
            await Add("JB01","handAttack",PileType.Hand);
            await Add("JA38","handArrow",PileType.Hand);
            await Add("JB02","handSkill",PileType.Hand);
            var status=player.Creature.CombatState!.CreateCard(ModelDb.Card<Wound>(),player);
            active.Roles[status]="handStatus"; await CardPileCmd.AddGeneratedCardToCombat(status,PileType.Hand,player);
            await Add("JB01","drawAttack",PileType.Draw);
            await Add("JB01","discardAttack",PileType.Discard);
            await Add("JB01","exhaustAttack",PileType.Exhaust);
        }
        if(mode is "sports" or "arrows")
        {
            var context = new ThrowingPlayerChoiceContext();
            var deployed=await Add(mode == "arrows" ? "JA81B" : "JA38","deployedArrow",PileType.Hand);
            await CardActions.FreePlay(context,deployed,player.Creature.CombatState!.HittableEnemies.First(),true);
            if (mode == "arrows")
            {
                var converted = await Add("JB01", "convertedArrow", PileType.Hand);
                await HandArrow.Play(context, converted);
                var meteor = ArrowCombat.Find(deployed)!;
                var material = ArrowCombat.Find(converted)!;
                await ArrowCombat.Accelerate(context, meteor, meteor.Remaining - 1);
                await ArrowCombat.Accelerate(context, material, material.Remaining - 1);
            }
        }
        if(mode=="arrows")
        {
            foreach(var (pile,role) in new[]{(PileType.Hand,"handArrow"),(PileType.Draw,"drawArrow"),(PileType.Discard,"discardArrow"),(PileType.Exhaust,"exhaustArrow")})
                await Add("JA38",role,pile);
            await Add("JB01","nonArrow",PileType.Hand);
        }
        await PotionCmd.TryToProcure(ModelDb.Potion<JunnaKnowledgePotion>().ToMutable(),player);
        await PotionCmd.TryToProcure(ModelDb.Potion<JunnaSportsDrink>().ToMutable(),player);
        await PotionCmd.TryToProcure(ModelDb.Potion<JunnaArrowRainPotion>().ToMutable(),player);
        Case.Active=active;active.Save();
    }
}
