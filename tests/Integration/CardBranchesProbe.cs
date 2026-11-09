using System.Text.Encodings.Web;
using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class CardBranchesCommand : AbstractConsoleCmd
{
    public override string CmdName => "junna_branches";
    public override string Args => "core | selection | conditions | relationships | triggers | targets | knowledge | retarget | prepare [card-id]";
    public override string Description => "Run the enumerated per-card resolution branches in the isolated test save.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        if (args.FirstOrDefault() == "retarget") return new(CardBranchesProbe.Retarget(player), true, "Deployed arrow target checks started");
        if (args.FirstOrDefault() == "prepare") return new(CardBranchesProbe.Prepare(player), true, "Selection demonstration prepared");
        return new(CardBranchesProbe.Run(player, args.FirstOrDefault() ?? "core", args.ElementAtOrDefault(1)), true, "Card branches started");
    }
}

public static class CardBranchesProbe
{
    private sealed class Selector : ICardSelector
    {
        public int PileChoice;
        public int? Count;
        public readonly List<(CardModel[] Cards, int Min, int Max)> Calls = [];
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            Calls.Add((cards, minSelect, maxSelect));
            if (cards.All(c => CardCatalog.DesignId(c) is "JK78D" or "JK78E"))
                return Task.FromResult<IEnumerable<CardModel>>(PileChoice < 0 ? [] : cards.Skip(PileChoice).Take(1));
            return Task.FromResult<IEnumerable<CardModel>>(cards.Take(Count ?? maxSelect));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    private sealed record Case(string Id, bool Upgraded, string Branch, Func<Task> Action);
    private sealed record CheckResult(string Name, object? Actual, object? Expected, bool Pass);
    private sealed record CaseResult(string Id, bool Upgraded, string Branch, bool Pass, CheckResult[] Checks, string? Error);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static async Task Retarget(Player player)
    {
        string directory=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,".."));
        var rows=new List<CaseResult>();var context=new ThrowingPlayerChoiceContext();
        var enemies=player.Creature.CombatState!.HittableEnemies.ToArray();
        if(enemies.Length<2)throw new InvalidOperationException("Two living enemies required");
        var original=enemies[0];var survivor=enemies[1];
        foreach(var power in player.Creature.Powers.ToArray())await PowerCmd.Remove(power);
        foreach(var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        {ArrowCombat.Cancel(card);await CardPileCmd.RemoveFromCombat(card,skipVisuals:true);}
        foreach(var enemy in enemies)
        {foreach(var power in enemy.Powers.ToArray())await PowerCmd.Remove(power);await CreatureCmd.SetMaxHp(enemy,1000000);await CreatureCmd.SetCurrentHp(enemy,1000000);await TestGameApi.LoseBlock(context,enemy,enemy.Block,null);}
        var cards=new List<(string Id,bool Up,CardModel Card)>();
        var plan=CardCatalog.Cards.Where(d=>d.Type is "箭矢" or "星矢").SelectMany(d=>new[]{false,true}.Select(up=>new{d.Id,Upgraded=up,Branch="已布置箭矢：目标死亡后的单体转移、群体跟随和自己目标"})).ToArray();
        File.WriteAllText(Path.Combine(directory,"card-branches-retarget-plan.json"),JsonSerializer.Serialize(plan,Json));
        double speed=Godot.Engine.TimeScale;Godot.Engine.TimeScale=20;
        try
        {
            using var selector=CardSelectCmd.UseSelector(new Selector());
            using var single=KnowledgeCombat.SingleCard();
            foreach(var item in plan)
            {
                var card=CardCatalog.Create(item.Id,player);
                if(item.Upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
                await CardPileCmd.Add(card,PileType.Hand,skipVisuals:true);
                player.PlayerCombatState!.Energy=card.EnergyCost.CostsX?0:99;
                await CardActions.PaidPlay(context,card,original);
                cards.Add((item.Id,item.Upgraded,card));
            }
            await CreatureCmd.SetCurrentHp(original,1);player.PlayerCombatState!.Energy=1;
            var strike=CardCatalog.Create("JB01",player);await CardPileCmd.Add(strike,PileType.Hand,skipVisuals:true);
            await CardActions.PaidPlay(context,strike,original);
            foreach(var item in cards)
            {
                var checks=new List<CheckResult>();var arrow=ArrowCombat.Find(item.Card);
                checks.Add(new("deployment survives",arrow!=null,true,arrow!=null));
                if(arrow!=null)
                {
                    bool pass=item.Card.TargetType==TargetType.AnyEnemy?arrow.Play.Target==survivor:
                        item.Card.TargetType==TargetType.AllEnemies?arrow.Targets.All(c=>c.IsAlive)&&arrow.Targets.Contains(survivor):arrow.Target==player.Creature;
                    checks.Add(new("target follows its type",pass,true,pass));
                }
                rows.Add(new(item.Id,item.Up,"已布置箭矢：目标死亡后的单体转移、群体跟随和自己目标",checks.All(c=>c.Pass),checks.ToArray(),null));
            }
        }
        finally{Godot.Engine.TimeScale=speed;}
        File.WriteAllText(Path.Combine(directory,"card-branches-retarget-results.json"),JsonSerializer.Serialize(new{completed=true,planned=plan.Length,results=rows},Json));
    }

    public static async Task Prepare(Player player)
    {
        Godot.Engine.TimeScale = 1;
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card); }
        player.PlayerCombatState.Energy = 10;
        using var single = KnowledgeCombat.SingleCard();
        foreach (var pile in new[] { PileType.Draw, PileType.Discard })
        foreach (var id in new[] { "JB01", "JA38", "JH10", "JB02" })
            await CardPileCmd.AddGeneratedCardToCombat(CardCatalog.Create(id, player), pile, player);
        foreach (bool upgraded in new[] { false, true })
        {
            var card = CardCatalog.Create("JK78", player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }
        foreach (var pile in new[] { player.PlayerCombatState.DrawPile, player.PlayerCombatState.DiscardPile })
            for (int i = 0; i < pile.Cards.Count; i++) pile.InvokeCardAddFinished();
    }

    public static async Task Run(Player player, string part, string? only)
    {
        string directory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".."));
        string suffix = part + (only == null ? "" : "-" + only);
        var cases = new List<Case>();
        var results = new List<CaseResult>();
        var checks = new List<CheckResult>();
        var context = new ThrowingPlayerChoiceContext();
        var state = player.Creature.CombatState!;
        var target = state.GetOpponentsOf(player.Creature).First();
        var selection = new Selector();
        void Add(string id, bool up, string branch, Func<Task> action)
        { if (only == null || id == only) cases.Add(new(id, up, branch, action)); }
        void Check(string name, object? actual, object? expected) => checks.Add(new(name, actual, expected, Equals(actual, expected)));
        async Task Reset()
        {
            await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
            selection.Count = null; selection.PileChoice = 0; selection.Calls.Clear();
            foreach (var p in player.Creature.Powers.ToArray()) await PowerCmd.Remove(p);
            foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
            { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
            foreach (var enemy in state.GetOpponentsOf(player.Creature))
            {
                foreach (var p in enemy.Powers.ToArray()) await PowerCmd.Remove(p);
                await CreatureCmd.SetMaxHp(enemy, 1000000); await CreatureCmd.SetCurrentHp(enemy, 1000000);
                await TestGameApi.LoseBlock(context, enemy, enemy.Block, null);
            }
            await TestGameApi.LoseBlock(context, player.Creature, player.Creature.Block, null);
            player.PlayerCombatState.Energy = 99;
            player.PlayerCombatState.IncrementTurnNumber();
        }
        async Task<CardModel> Card(string id, PileType pile = PileType.Hand, bool up = false)
        {
            using var single = KnowledgeCombat.SingleCard();
            var c = CardCatalog.Create(id, player);
            if (up) { c.UpgradeInternal(); c.FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(c, pile, skipVisuals: true);
            return c;
        }
        Task<bool> Play(CardModel c) => CardActions.PaidPlay(context, c, target);
        async Task Finish(CardModel c)
        { if (ArrowCombat.Find(c) is { } arrow) await ArrowCombat.Accelerate(context, arrow, int.MaxValue); }
        int Number(CardDefinition d, bool up, string key, int fallback = 0)
            => d.Values.FirstOrDefault(v => v.Name == key) is { } n ? up ? n.Upgraded : n.Base : fallback;
        decimal Bonus(CardModel c) => DamageModifiers.CaptureDamageModifierSnapshot(c).DamageBonus;
        int Hits(CardModel c) => DamageModifiers.CaptureDamageModifierSnapshot(c).ExtraHits;

        foreach (var definition in CardCatalog.Cards)
        foreach (bool up in new[] { false, true })
        {
            string id = definition.Id;
            if (part == "core")
            {
                Add(id, up, "不可打出：保持手牌、费用、生命与布置", async () =>
                {
                    var card = await Card(id, up: up); card.AddKeyword(CardKeyword.Unplayable);
                    int hp = target.CurrentHp, energy = player.PlayerCombatState!.Energy;
                    Check("play rejected", await Play(card), false);
                    Check("pile", card.Pile!.Type, PileType.Hand); Check("energy", player.PlayerCombatState.Energy, energy);
                    Check("enemy hp", target.CurrentHp, hp); Check("deployment", ArrowCombat.Deployed.Count, 0);
                });
                int cost = up ? definition.UpgradedCost : definition.Cost;
                if (cost > 0 && !definition.CostsX)
                Add(id, up, "能量不足：整张牌不结算", async () =>
                {
                    var card = await Card(id, up: up); player.PlayerCombatState!.Energy = cost - 1;
                    int hp = target.CurrentHp;
                    Check("play rejected", await Play(card), false); Check("energy", player.PlayerCombatState.Energy, cost - 1);
                    Check("pile", card.Pile!.Type, PileType.Hand); Check("enemy hp", target.CurrentHp, hp);
                    Check("block", player.Creature.Block, 0); Check("powers", player.Creature.Powers.Count, 0);
                });
                if (definition.DamageCapable && id is not ("JG34" or "JH30"))
                foreach (int shield in new[] { 0, 7 })
                Add(id, up, $"伤害强化与额外次数：目标格挡{shield}", async () =>
                {
                    var card = await Card(id, up: up);
                    int damage = Number(definition, up, "Damage"), hits = Number(definition, up, "Repeat", 1);
                    if (id == "JA81B") { ((CatalogCard)card).SetGeneratedDamage(7); damage = 7; }
                    Check("investment accepted", DamageModifiers.Add(card, 3, 1), true);
                    if (shield > 0) await CreatureCmd.GainBlock(target, shield, ValueProp.Unpowered, null);
                    player.PlayerCombatState!.Energy = definition.CostsX ? 0 : cost;
                    int hp = target.CurrentHp;
                    Check("paid play", await Play(card), true); await Finish(card);
                    int expected = Math.Max(0, (damage + 3) * (hits + 1) - shield);
                    Check("damage", hp - target.CurrentHp, expected);
                    Check("block side effect occurs once", player.Creature.Block, id == "JA73" ? 0 : Number(definition, up, "Block"));
                    if (id == "JG33") Check("self investment after attack", Bonus(card), 13m);
                    if (id == "JA76")
                    { Check("flying stays base", ArrowCombat.Deployed.All(a => !a.Card.IsUpgraded), true); Check("flying waits two turns", ArrowCombat.Deployed.All(a => a.Remaining == 2), true); }
                    if (id is "JA81" or "JA76")
                        Check("generated arrow captures result", ArrowCombat.Deployed.Where(a => CardCatalog.DesignId(a.Card) == "JA81B").All(a => a.Card.DynamicVars.Damage.IntValue == (damage + 3) * (hits + 1)), true);
                });
            }

            if (part == "selection")
            {
                if ("JG03 JG09 JG16 JT02 JH01 JK11 JK11B JK50B JK53 JG27 JA65 JA66 JK73B JK78 JG36 JK37C JK64 JK78D JK78E".Split(' ').Contains(id))
                Add(id, up, "选择提示与空候选", async () =>
                {
                    var c = await Card(id, up: up);
                    string prompt = CardChoices.Prompt(c, 3).GetFormattedText();
                    Check("localized prompt", prompt.Contains("选择") && !prompt.Contains("ERROR") && !prompt.Contains('{'), true);
                    Check("paid play with empty candidates", await Play(c), true); await Finish(c);
                    Check("no selector opened", selection.Calls.Count, 0);
                    Check("no created cards", CardActions.All(player).Count(x => x != c), 0);
                });
                if (id is "JG03" or "JG09" or "JG16" or "JG27" or "JG36" or "JT02")
                Add(id, up, "筛选有效目标并保留实际实例", async () =>
                {
                    var invalid = await Card("JB02");
                    var chosen = await Card(id == "JT02" ? "JA38" : "JB01");
                    var other = await Card("JB01", PileType.Exhaust);
                    if (id == "JG16") { DamageModifiers.Add(chosen, 2, 1); DamageModifiers.Add(other, 4, 2); }
                    var c = await Card(id, up: up); await Play(c);
                    Check("invalid excluded", selection.Calls.All(call => !call.Cards.Contains(invalid)), true);
                    Check("selected identity stays in hand", chosen.Pile!.Type, PileType.Hand);
                    Check("invalid stays unmodified", Bonus(invalid), 0m);
                    if (id == "JT02") { Check("converted type", chosen.Type, CardType.Attack); Check("converted cost", CardActions.Cost(chosen), 1); }
                    else if (id == "JG03") Check("extra hit", Hits(chosen), 1);
                    else if (id == "JG16")
                    { Check("bonus conserved", Bonus(chosen), 6m); Check("donor bonus cleared", Bonus(other), 0m); Check("target hits retained", Hits(chosen), 1); Check("donor hits retained", Hits(other), 2); }
                    else Check("bonus", Bonus(chosen), (decimal)Number(definition, up, "Bonus"));
                });
                if (id is "JK78" or "JK78D" or "JK78E")
                foreach (int pileChoice in id == "JK78" ? new[] { -1, 0, 1 } : new[] { id == "JK78D" ? 0 : 1 })
                foreach (int count in new[] { 0, 1, 3 })
                Add(id, up, $"牌堆选项{pileChoice}、选择{count}张、另一牌堆隔离", async () =>
                {
                    selection.PileChoice = pileChoice; selection.Count = count;
                    var draw = new List<CardModel>(); var discard = new List<CardModel>();
                    for (int i = 0; i < 4; i++) { draw.Add(await Card("JB01", PileType.Draw)); discard.Add(await Card("JB01", PileType.Discard)); }
                    var invalidDraw = await Card("JB02", PileType.Draw); var invalidDiscard = await Card("JB02", PileType.Discard);
                    var beforeDraw = player.PlayerCombatState!.DrawPile.Cards.ToArray(); var beforeDiscard = player.PlayerCombatState.DiscardPile.Cards.ToArray();
                    await Play(await Card(id, up: up));
                    int selectedCount = id == "JK78" && pileChoice < 0 ? 0 : count;
                    var selected = pileChoice == 0 ? draw : discard; var untouched = pileChoice == 0 ? discard : draw;
                    Check("exact selected count", selected.Count(c => Bonus(c) == (up ? 8m : 6m)), selectedCount);
                    Check("other pile untouched", untouched.All(c => Bonus(c) == 0), true);
                    Check("non-damage cards excluded", Bonus(invalidDraw) == 0 && Bonus(invalidDiscard) == 0, true);
                    Check("draw identity and order", player.PlayerCombatState.DrawPile.Cards.SequenceEqual(beforeDraw), true);
                    Check("discard identity and order", player.PlayerCombatState.DiscardPile.Cards.Where(c => CardCatalog.DesignId(c) != id).SequenceEqual(beforeDiscard), true);
                    Check("options stay outside combat", CardActions.All(player).Count(c => CardCatalog.DesignId(c) is "JK78D" or "JK78E" && CardCatalog.DesignId(c) != id), 0);
                    if (selectedCount > 0) Check("optional min and max", selection.Calls.Last().Min == 0 && selection.Calls.Last().Max == 3, true);
                });
                if (id == "JK78")
                foreach (int pileChoice in new[] { 0, 1 })
                Add(id, up, $"一堆空、选择空牌堆{pileChoice}", async () =>
                {
                    selection.PileChoice = pileChoice;
                    var other = await Card("JB01", pileChoice == 0 ? PileType.Discard : PileType.Draw);
                    await Play(await Card(id, up: up)); Check("other pile untouched", Bonus(other), 0m);
                    Check("only pile choice", selection.Calls.Count, 1);
                });
                if (id == "JA66")
                foreach (int available in new[] { 1, 4 })
                foreach (int count in new[] { 0, 1, 2 })
                Add(id, up, $"弃牌回收：候选{available}、选择{count}", async () =>
                {
                    selection.Count = count;
                    var cards = new List<CardModel>(); for (int i = 0; i < available; i++) cards.Add(await Card("JB01", PileType.Discard));
                    await Play(await Card(id, up: up)); await Finish(CardActions.All(player).Single(c => CardCatalog.DesignId(c) == id));
                    Check("only selected recalled", cards.Count(c => c.Pile!.Type == PileType.Hand), Math.Min(available, count));
                    Check("optional minimum", selection.Calls.Last().Min, 0); Check("selection maximum", selection.Calls.Last().Max, Math.Min(2, available));
                });
                if (id is "JK11" or "JK11B")
                foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
                Add(id, up, $"具名召回：{pile}", async () =>
                {
                    string name = id == "JK11" ? "JK11B" : "JK11";
                    var recalled = await Card(name, pile, up: true); DamageModifiers.Add(recalled, 3);
                    await Play(await Card(id, up: up));
                    Check("actual instance recalled", recalled.Pile!.Type, PileType.Hand); Check("upgrade retained", recalled.IsUpgraded, true);
                    if (id == "JK11") Check("investment retained", Bonus(recalled), 3m);
                });
                if (id == "JK73B")
                foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, ArrowPile.PileId })
                foreach (bool affordable in new[] { false, true })
                Add(id, up, $"具名付费打出：{pile}、可支付{affordable}", async () =>
                {
                    var arrow = await Card("JK73", pile == ArrowPile.PileId ? PileType.Hand : pile);
                    if (pile == ArrowPile.PileId) await Play(arrow);
                    var c = await Card(id, up: up); player.PlayerCombatState!.Energy = CardActions.Cost(c) + (affordable ? CardActions.Cost(arrow) : 0);
                    int before = player.PlayerCombatState.Energy; await Play(c);
                    Check("payment", player.PlayerCombatState.Energy, before - CardActions.Cost(c) - (affordable ? CardActions.Cost(arrow) : 0));
                    Check("played only if affordable", ArrowCombat.Find(arrow) != null, affordable || pile == ArrowPile.PileId);
                    if (!affordable && pile != ArrowPile.PileId) Check("unaffordable card unchanged", arrow.Pile!.Type, pile);
                });
            }

                if (part == "relationships")
                {
                    if (id is "JK32" or "JK32B" or "JK32C" or "JK33" or "JK33B")
                    Add(id, up, "具名修改：多牌区、重复实例与无关牌隔离", async () =>
                    {
                        string[] related = id switch { "JK32" => ["JK32B","JK32C"], "JK32B" => ["JK32","JK32C"], "JK32C" => ["JK32","JK32B"], "JK33" => ["JK33B"], _ => ["JK33"] };
                        var affected = new List<CardModel>();
                        foreach (var name in related)
                        foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust }) affected.Add(await Card(name,pile));
                        var unrelated = await Card("JB01",PileType.Draw);
                        await Play(await Card(id,up:up));
                        if (CardCatalog.IsArrow(CardCatalog.Model(id))) await Finish(CardActions.All(player).Single(c=>CardCatalog.DesignId(c)==id));
                        foreach (var c in affected)
                            Check("named " + c.Pile!.Type, id.StartsWith("JK32") ? CardActions.Cost(c) : id=="JK33" ? (object)Bonus(c) : Hits(c), id.StartsWith("JK32") ? 0 : id=="JK33" ? (object)6m : 1);
                        Check("unrelated cost",CardActions.Cost(unrelated),1); Check("unrelated bonus",Bonus(unrelated),0m);
                    });
                    if (id == "JG16")
                    Add(id,up,"跨牌区伤害加成守恒、额外次数独立",async()=>
                    {
                        var chosen=await Card("JB01"); DamageModifiers.Add(chosen,1,1);
                        var donors=new List<CardModel>(); int amount=2;
                        foreach(var pile in new[]{PileType.Hand,PileType.Draw,PileType.Discard,PileType.Exhaust})
                        { var c=await Card("JB01",pile); DamageModifiers.Add(c,amount++,2); donors.Add(c); }
                        var arrow=await Card("JB03"); await Play(arrow); DamageModifiers.Add(arrow,6,3); donors.Add(arrow);
                        // Choose the first eligible hand card, which is the same chosen instance.
                        await Play(await Card(id,up:up));
                        Check("total conserved",Bonus(chosen),21m); Check("donors cleared",donors.All(c=>Bonus(c)==0),true);
                        Check("target extra hits",Hits(chosen),1); Check("donor extra hits",Hits(arrow),3);
                        Check("deployed donor stays",ArrowCombat.Find(arrow)!=null,true);
                    });
                    if(id=="JG33")
                    Add(id,up,"同一实例循环：先攻击后加成、返回抽牌堆",async()=>
                    {
                        var c=await Card(id,up:up); int hp=target.CurrentHp; await Play(c);
                        Check("first attack",hp-target.CurrentHp,up?40:30); Check("first investment",Bonus(c),10m);
                        Check("returns to draw",c.Pile!.Type,PileType.Draw); await CardPileCmd.Add(c,PileType.Hand);
                        hp=target.CurrentHp; await Play(c); Check("second attack",hp-target.CurrentHp,up?50:40);
                        Check("second investment",Bonus(c),20m); Check("one actual instance",CardActions.All(player).Count(x=>ReferenceEquals(x,c)),1);
                    });
                    if(id is "JA22" or "JA66")
                    foreach(int occupied in new[]{9,10})
                    Add(id,up,$"回手容量：已有{occupied}张手牌",async()=>
                    {
                        CardModel selected;
                        CardModel source;
                        if(id=="JA22")
                        { await Play(await Card(id,up:up)); selected=await Card("JB03"); await Play(selected); source=selected; }
                        else { selected=await Card("JB01",PileType.Discard); source=await Card(id,up:up); await Play(source); }
                        for(int i=0;i<occupied;i++)await Card("JB02");
                        await Finish(source);
                        Check("hand capacity",player.PlayerCombatState!.Hand.Cards.Count,10);
                        Check("overflow destination",selected.Pile!.Type,occupied==9?PileType.Hand:PileType.Discard);
                        Check("no duplicate actual card",CardActions.All(player).Count(c=>ReferenceEquals(c,selected)),1);
                    });
                    if(id=="JH22")
                    foreach(int shield in new[]{0,99})
                    Add(id,up,$"伤害结果转加成：目标格挡{shield}、限定布置的伤害箭矢",async()=>
                    {
                        var deployed=await Card("JB03");await Play(deployed);
                        var utility=await Card("JA15");await Play(utility);
                        var draw=await Card("JB03",PileType.Draw);
                        if(shield>0)await CreatureCmd.GainBlock(target,shield,ValueProp.Unpowered,null);
                        var c=await Card(id,up:up);DamageModifiers.Add(c,2,1);await Play(c);
                        Check("deployed damage arrow",Bonus(deployed),(decimal)((up?12:10)+2)*2);
                        Check("utility arrow unmodified",Bonus(utility),0m);Check("draw arrow unmodified",Bonus(draw),0m);
                    });
                    if(id=="JA75")
                    Add(id,up,"完成时筛选手牌：攻击、持续伤害、无伤害、其他牌区",async()=>
                    {
                        var c=await Card(id,up:up);await Play(c);
                        var attack=await Card("JB01");var persistent=await Card("JH30");var invalid=await Card("JB02");var draw=await Card("JB01",PileType.Draw);
                        await Finish(c);Check("attack bonus",Bonus(attack),up?4m:3m);Check("persistent source bonus",Bonus(persistent),up?4m:3m);
                        Check("non-damage excluded",Bonus(invalid),0m);Check("other pile excluded",Bonus(draw),0m);
                    });
                    if(id=="JK75")
                    foreach(int eligible in new[]{0,3})
                    Add(id,up,$"知识触发随机强化：合格手牌{eligible}",async()=>
                    {
                        await Play(await Card(id,up:up));var cards=new List<CardModel>();
                        for(int i=0;i<eligible;i++)cards.Add(await Card("JB01"));
                        var invalid=await Card("JB02");await Play(await Card("JK15B"));
                        Check("exactly one eligible receives bonus",cards.Count(c=>Bonus(c)==(up?2m:1m)),Math.Min(1,eligible));
                        Check("invalid excluded",Bonus(invalid),0m);
                    });
                    if(id=="JH19")
                    foreach(string next in new[]{"JB01","JB02","JB03","JA38","JK15B","JK69B","JH10","JA59","JA82"})
                    foreach(int existing in new[]{0,1})
                    Add(id,up,$"原版临时重放：{next}、已有重放{existing}",async()=>
                    {
                        await Play(await Card(id,up:up));
                        // An intervening Power must leave the preparation available.
                        await Play(await Card("JK27"));Check("power does not consume readiness",CatalogPower.Has(player,id),true);
                        var c=await Card(next);c.BaseReplayCount=existing;player.PlayerCombatState!.Energy=c.EnergyCost.CostsX?3:CardActions.Cost(c);
                        int hp=target.CurrentHp,block=player.Creature.Block;await Play(c);
                        Check("single payment",player.PlayerCombatState.Energy,0);Check("readiness consumed",CatalogPower.Has(player,id),false);
                        Check("permanent replay unchanged",c.BaseReplayCount,existing);
                        if(CardCatalog.IsArrow(c))
                        {Check("one deployment",ArrowCombat.Deployed.Count,1);Check("captured native replay count",ArrowCombat.Find(c)!.PlayCount,existing+2);await Finish(c);}
                        int damage=next switch{"JB01"=>6,"JB03"=>25,"JA38"=>12,"JK69B"=>22,"JH10"=>27,"JA59"=>24,_=>0};
                        Check("damage repetitions",hp-target.CurrentHp,damage*(existing+2));
                        if(next=="JA82")Check("each replay generates X arrows",ArrowCombat.Deployed.Count,3*(existing+2));
                        int gain=next=="JB02"?5:next=="JK15B"?8:0;Check("block repetitions",player.Creature.Block-block,gain*(existing+2));
                        Check("normal destination",c.Pile!.Type,next is "JK15B" or "JK69B"?PileType.Exhaust:PileType.Discard);
                        var following=await Card("JB01");player.PlayerCombatState.Energy=1;hp=target.CurrentHp;await Play(following);
                        Check("following card normal",hp-target.CurrentHp,6);
                    });
                }

                if (part == "triggers")
                {
                    if (id == "JH24")
                    Add(id, up, "战斗能量上限：叠加、刷新与战后清除", async () =>
                    {
                        int maximum = player.PlayerCombatState!.MaxEnergy, permanent = player.MaxEnergy;
                        await Play(await Card(id, up: up));
                        await Play(await Card(id, up: !up));
                        Check("mixed upgrades stack maximum", player.PlayerCombatState.MaxEnergy, maximum + 2);
                        Check("no installed buff", CatalogPower.Has(player, id), false);
                        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
                        Check("maximum independent of buffs", player.PlayerCombatState.MaxEnergy, maximum + 2);
                        player.PlayerCombatState.ResetEnergy();
                        Check("refresh uses stacked maximum", player.PlayerCombatState.Energy, maximum + 2);
                        player.PlayerCombatState.AddMaxEnergyToCurrent();
                        Check("full retention uses stacked maximum", player.PlayerCombatState.Energy, 2 * (maximum + 2));
                        Check("permanent maximum unchanged", player.MaxEnergy, permanent);
                        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).AfterCombatEnd(null!);
                        Check("combat end clears bonus", player.PlayerCombatState.MaxEnergy, maximum);
                    });
                    if ("JA04 JK10 JA22 JK15 A JK27 JA35 JK32 JK37C JA45 JK64 JK67 JG31 JH19 JH21 JG34 JK75 JH23 JA70 JK80 JK81 JZ05 JZ06 JH30 JK46".Split(' ').Contains(id))
                    Add(id,up,"无关事件、条件不满足与延迟收益",async()=>
                    {
                        if(id=="JG34")player.PlayerCombatState!.Energy=0;
                        await Play(await Card(id,up:up));
                        var power=player.Creature.Powers.OfType<CatalogPower>().Single(p=>p.DesignId==id);
                        if(id is "JK37C" or "JK64" or "JA45")
                        {
                            var arrow=await Card("JB03");await Play(arrow);int before=ArrowCombat.Find(arrow)!.Remaining;
                            await Play(await Card("JB01"));Check("unrelated play no acceleration",ArrowCombat.Find(arrow)!.Remaining,before);
                        }
                        else if(id=="JH19")
                        {await Play(await Card("JK27"));Check("power skipped",CatalogPower.Has(player,id),true);}
                        else if(id is "JK10" or "JK15" or "JA04" or "JH23" or "JA70" or "JK80" or "JG31" or "JK75")
                        {
                            var candidate=await Card("JB01");await Card("JB01",PileType.Draw);
                            int block=player.Creature.Block;await Play(await Card("JB02"));
                            Check("only defense block",player.Creature.Block-block,5);Check("no unrelated bonus",Bonus(candidate),0m);
                            Check("no copy or draw",player.PlayerCombatState!.DrawPile.Cards.Count,1);
                            if(id is "JA04" or "JK80")Check("preparation remains",CatalogPower.Has(player,id),true);
                        }
                        else if(id=="JK27")
                        {
                            await Card("JK11",PileType.Draw);await Card("JK11B",PileType.Draw);
                            var ordinary=await Card("JB01",PileType.Draw);await CardPileCmd.Add(ordinary,PileType.Draw,CardPilePosition.Top,skipVisuals:true);
                            await CardPileCmd.Draw(context,player);Check("ordinary draw no linked draw",player.PlayerCombatState!.Hand.Cards.Count,1);
                        }
                        else if(id=="JH21")
                        {int block=player.Creature.Block;player.PlayerCombatState!.Energy=0;await power.BeforeSideTurnEnd(context,player.Creature.Side,[player.Creature]);Check("zero remaining energy",player.Creature.Block,block);}
                        else if(id=="JK67")
                        {var ordinary=await Card("JB01");await power.BeforeSideTurnEnd(context,player.Creature.Side,[player.Creature]);Check("non-arrow excluded",Bonus(ordinary),0m);}
                        else if(id=="JK81")
                        {var low=await Card("JB01");await power.BeforeFlush(context,player);Check("below cost threshold",low.ShouldRetainThisTurn,false);}
                        else if(id=="JZ05")
                        {await TestGameApi.Damage(context,target,3,ValueProp.Unpowered,player.Creature);Check("nonattack excluded",ArrowCombat.Deployed.Count,0);}
                        else if(id=="JH30")
                        {int hp=target.CurrentHp;await PlayerCmd.GainEnergy(0,player);await PlayerCmd.SetEnergy(4,player);Check("zero and set energy excluded",target.CurrentHp,hp);}
                        else if(id=="JZ06")
                        {await Card("JA59");int energy=player.PlayerCombatState!.Energy;await power.AfterPlayerTurnStart(context,player);Check("X excluded from numeric maximum",player.PlayerCombatState.Energy,energy);}
                        else if(id is "A" or "JK46")
                        {
                            if(id=="A")await power.AfterPlayerTurnStart(context,player);
                            else await power.BeforeSideTurnEnd(context,player.Creature.Side,[player.Creature]);
                            Check("generation waits for its phase",CardActions.All(player).Count(),0);
                        }
                        else if(id is "JA35" or "JK32" or "JG34")
                        {
                            int hp=target.CurrentHp;await power.AfterPlayerTurnStart(context,player);
                            Check("empty source no damage",target.CurrentHp,hp);Check("empty source no cards",player.PlayerCombatState!.Hand.Cards.Count,0);
                        }

                        else if(id=="JA22")
                        {var c=await Card("JB01");await Play(c);Check("non-arrow destination",c.Pile!.Type,PileType.Discard);}
                    });
                    if(id is "JG34" or "JH30" or "JA70" or "JK15" or "JA45")
                    Add(id,up,"叠加施加：独立数值与次数",async()=>
                    {
                        if(id=="JG34")player.PlayerCombatState!.Energy=2;
                        await Play(await Card(id,up:up));
                        if(id=="JG34")player.PlayerCombatState!.Energy=3;
                        await Play(await Card(id,up:up));
                        if(id=="JG34")
                        {int hp=target.CurrentHp;var p=player.Creature.Powers.OfType<CatalogPower>().Single(p=>p.DesignId==id);await p.AfterPlayerTurnStart(context,player);Check("independent X snapshots",hp-target.CurrentHp,(up?4:3)*5);}
                        else if(id=="JH30")
                        {int hp=target.CurrentHp;await PlayerCmd.GainEnergy(2,player);Check("both sources deal damage",hp-target.CurrentHp,4);}
                        else if(id=="JA70")
                        {int before=player.Creature.Block;await Play(await Card("JB03"));Check("both sources grant block",player.Creature.Block-before,(up?3:2)*2);}
                        else if(id=="JA45")
                        {var c=await Card("JB03");await Play(c);Check("both accelerate",ArrowCombat.Find(c)!.Remaining,1);}
                        else
                        {int before=player.PlayerCombatState!.DrawPile.Cards.Count;await Play(await Card("JK15B"));Check("both copies granted",player.PlayerCombatState.DrawPile.Cards.Count-before,2);}
                    });
                }

                if (part == "targets" && (definition.DamageCapable || id is "JA82" or "JF10" or "JA58"))
                Add(id,up,"多敌人：单体隔离、群体逐目标、格挡与派生目标",async()=>
                {
                    var enemies=state.GetOpponentsOf(player.Creature).ToArray();
                    Check("multiple enemy fixture",enemies.Length>=2,true);
                    if(enemies.Length<2)return;
                    var hp=enemies.Select(e=>e.CurrentHp).ToArray();
                    if(id!="JH30")await CreatureCmd.GainBlock(enemies[1],3,ValueProp.Unpowered,null);
                    var c=await Card(id,up:up);
                    int damage=Number(definition,up,"Damage"),hits=Number(definition,up,"Repeat",1);
                    if(id=="JA81B"){((CatalogCard)c).SetGeneratedDamage(7);damage=7;}
                    if(id=="JA82")damage=0;
                    player.PlayerCombatState!.Energy=definition.CostsX?(id=="JG34"?2:id=="JA82"?3:0):CardActions.Cost(c);
                    await Play(c);await Finish(c);
                    if(id=="JG34")
                    {hits=2;await player.Creature.Powers.OfType<CatalogPower>().Single(p=>p.DesignId==id).AfterPlayerTurnStart(context,player);}
                    if(id=="JH30")
                    {
                        await PlayerCmd.GainEnergy(2,player);
                        Check("total random damage",enemies.Select((e,i)=>hp[i]-e.CurrentHp).Sum(),2);
                        Check("one random living target",enemies.Select((e,i)=>hp[i]-e.CurrentHp).Count(x=>x>0),1);
                    }
                    else
                    {
                        bool all=definition.Target=="AllEnemies"||id=="JG34";
                        for(int i=0;i<enemies.Length;i++)Check("target "+i,hp[i]-enemies[i].CurrentHp,i==0||all?Math.Max(0,damage*hits-(i==1?3:0)):0);
                    }
                    if(id=="JA76")
                    {
                        Check("one flying per enemy",ArrowCombat.Deployed.Count,enemies.Length);
                        Check("flying stays base",ArrowCombat.Deployed.All(a=>!a.Card.IsUpgraded&&a.Remaining==2),true);
                        foreach(var enemy in enemies)Check("explicit flying target",ArrowCombat.Deployed.Count(a=>a.Play.Target==enemy),1);
                    }
                    if(id=="JA81")Check("source-assigned target",ArrowCombat.Deployed.Single().Play.Target==target,true);
                    if(id=="JA82")
                    {Check("X flying count",ArrowCombat.Deployed.Count,3);Check("automatic targets are living enemies",ArrowCombat.Deployed.All(a=>a.Play.Target!=null&&enemies.Contains(a.Play.Target)),true);}
                    if(id is "JF10" or "JA58")
                    {
                        Check("weak only on selected target",enemies[0].GetPower<WeakPower>()?.Amount??0,Number(definition,up,"N0"));
                        Check("vulnerable only on selected target",enemies[0].GetPower<VulnerablePower>()?.Amount??0,Number(definition,up,"N0"));
                        Check("other target unaffected",enemies.Skip(1).All(e=>e.GetPower<WeakPower>()==null&&e.GetPower<VulnerablePower>()==null),true);
                    }
                });

                if (part == "knowledge")
                {
                    var group=CardCatalog.Groups.FirstOrDefault(g=>g.MainCardId==id||g.GrantedCardIds.Contains(id));
                    if(group!=null)
                    {
                        Add(id,up,"发现入口：整组数量、源牌升级与关联牌基础版",async()=>
                        {
                            var entry=CardCatalog.Create(id,player);
                            if(up){entry.UpgradeInternal();entry.FinalizeUpgradeInternal();}
                            var picked=await CardSelectCmd.FromChooseACardScreen(context,[entry],player);
                            Check("same discovery instance",ReferenceEquals(picked,entry),true);
                            await CardPileCmd.AddGeneratedCardToCombat(picked!,PileType.Hand,player);
                            var expected=group.GrantedCardIds.ToDictionary(key=>key,key=>group.GrantedCardCounts.GetValueOrDefault(key,1));expected[group.MainCardId]=1;
                            Check("group total",player.PlayerCombatState!.Hand.Cards.Count,expected.Values.Sum());
                            foreach(var pair in expected)Check("member "+pair.Key,player.PlayerCombatState.Hand.Cards.Count(c=>CardCatalog.DesignId(c)==pair.Key),pair.Value);
                            Check("source upgrade retained",entry.IsUpgraded,up);
                            Check("related cards base",player.PlayerCombatState.Hand.Cards.Where(c=>c!=entry).All(c=>!c.IsUpgraded),true);
                            Check("owner retained",player.PlayerCombatState.Hand.Cards.All(c=>c.Owner==player),true);
                        });
                        Add(id,up,"单张生成：只生成指定对象与版本",async()=>
                        {
                            await CardActions.Generate(player,id,1,PileType.Hand,up);
                            Check("only one generated",player.PlayerCombatState!.Hand.Cards.Count,1);
                            var generated=player.PlayerCombatState.Hand.Cards.Single();Check("selected member",CardCatalog.DesignId(generated),id);Check("generated version",generated.IsUpgraded,up);
                        });
                        Add(id,up,"关联抽取：全部关联实际牌、同名及弃牌堆隔离",async()=>
                        {
                            var entry=await Card(id,up:up);
                            var partners=new List<CardModel>();
                            foreach(var partnerId in group.GrantedCardIds.Prepend(group.MainCardId).Where(key=>key!=id))
                                for(int i=0;i<(partnerId==group.MainCardId?1:group.GrantedCardCounts.GetValueOrDefault(partnerId,1));i++)partners.Add(await Card(partnerId,PileType.Draw));
                            var same=await Card(id,PileType.Draw);var unrelated=await Card("JB01",PileType.Draw);
                            var discarded=await Card(CardCatalog.DesignId(partners[0])!,PileType.Discard);
                            await KnowledgeCombat.DrawPartners(context,entry);
                            Check("linked actual instances drawn",partners.All(c=>c.Pile!.Type==PileType.Hand),true);
                            Check("exact hand count",player.PlayerCombatState!.Hand.Cards.Count,partners.Count+1);
                            Check("same named source stays",same.Pile!.Type,PileType.Draw);
                            Check("unrelated stays",unrelated.Pile!.Type,PileType.Draw);Check("discard not searched",discarded.Pile!.Type,PileType.Discard);
                        });
                    }
                }

            if (part == "conditions")
            {
                if (definition.Type is "箭矢" or "星矢")
                Add(id, up, "自然倒计时：布置当回合不计、同回合只计一次", async () =>
                {
                    var c = await Card(id, up: up);
                    player.PlayerCombatState!.Energy = definition.CostsX ? 0 : CardActions.Cost(c);
                    await Play(c); int initial = Number(definition, up, "Turns");
                    var hooks = ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>());
                    await hooks.AfterPlayerTurnStartLate(context, player);
                    Check("deployment turn waits", ArrowCombat.Find(c)?.Remaining ?? 0, initial);
                    player.PlayerCombatState.IncrementTurnNumber();
                    await hooks.AfterPlayerTurnStartLate(context, player);
                    Check("next turn decrements once", ArrowCombat.Find(c)?.Remaining ?? 0, Math.Max(0, initial - 1));
                    int hp = target.CurrentHp, block = player.Creature.Block;
                    await hooks.AfterPlayerTurnStartLate(context, player);
                    Check("duplicate tick does not decrement", ArrowCombat.Find(c)?.Remaining ?? 0, Math.Max(0, initial - 1));
                    Check("duplicate tick no damage", target.CurrentHp, hp); Check("duplicate tick no block", player.Creature.Block, block);
                });
                if (id is "JA59" or "JA82" or "JG34")
                foreach (int energy in new[] { 0, 1, 3, 12 })
                Add(id, up, $"X结算：支付{energy}", async () =>
                {
                    var c = await Card(id, up: up); player.PlayerCombatState!.Energy = energy; int hp = target.CurrentHp;
                    await Play(c); Check("X paid", player.PlayerCombatState.Energy, 0); Check("captured X", c.EnergyCost.CapturedXValue, energy);
                    if (id == "JA82") Check("generated count", ArrowCombat.Deployed.Count, energy);
                    else if (id == "JG34")
                    {
                        foreach (var p in player.Creature.Powers.OfType<CatalogPower>().ToArray()) await p.AfterPlayerTurnStart(context, player);
                        Check("X periodic hit count", hp - target.CurrentHp, energy * (up ? 4 : 3));
                    }
                    else
                    {
                        int remaining = Math.Max(0, Number(definition, up, "Turns") - energy);
                        Check("initial countdown", ArrowCombat.Find(c)?.Remaining ?? 0, remaining); await Finish(c);
                        Check("X does not alter damage", hp - target.CurrentHp, (up ? 16 : 12) * 2);
                    }
                });
                if (id == "JH13")
                foreach (int remainder in new[] { 0, 1, 2, 3, 4, 5 })
                Add(id, up, $"剩余能量阈值：{remainder}", async () =>
                {
                    var c = await Card(id, up: up); player.PlayerCombatState!.Energy = CardActions.Cost(c) + remainder;
                    int hp = target.CurrentHp; await Play(c);
                    Check("quotient after payment", hp - target.CurrentHp, (up ? 14 : 10) * (1 + remainder / 2));
                });
                if (id is "JK74" or "JK74B")
                foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
                Add(id, up, $"关联牌所在牌区：{pile}", async () =>
                {
                    await Card(id == "JK74" ? "JK74B" : "JK74", pile);
                    int hp = target.CurrentHp; await Play(await Card(id, up: up));
                    Check("hand-only condition", id == "JK74" ? hp - target.CurrentHp : player.Creature.Block,
                        (id == "JK74" ? up ? 12 : 10 : up ? 9 : 7) * (pile == PileType.Hand ? 2 : 1));
                });
                if (id is "JK48" or "JK48B")
                foreach (int count in new[] { 0, 4 })
                Add(id, up, $"各牌区知识计数：{count}", async () =>
                {
                    foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust }.Take(count)) await Card("JK15B", pile);
                    await Card("JB01", PileType.Draw); int hp = target.CurrentHp;
                    await Play(await Card(id, up: up));
                    Check("knowledge count", id == "JK48" ? hp - target.CurrentHp : player.Creature.Block,
                        (id == "JK48" ? up ? 10 : 6 : up ? 7 : 4) + player.Deck.Cards.Count(CardCatalog.IsKnowledge));
                });
                if (id is "JA73" or "JX02")
                foreach (int count in new[] { 0, 1, 3 })
                Add(id, up, $"布置箭矢数量：{count}", async () =>
                {
                    for (int i = 0; i < count; i++) await Play(await Card("JB03"));
                    var c = await Card(id, up: up); int energy = player.PlayerCombatState!.Energy; await Play(c);
                    if (id == "JA73") Check("conditional block", player.Creature.Block, count == 0 ? 0 : up ? 6 : 4);
                    else Check("energy equals deployments", player.PlayerCombatState.Energy, energy - CardActions.Cost(c) + count);
                });
                if (id == "JK81B")
                foreach (int count in new[] { 0, 1, 9 })
                Add(id, up, $"其他手牌数量：{count}", async () =>
                {
                    for (int i = 0; i < count; i++) await Card("JB01");
                    await Play(await Card(id, up: up)); Check("source excluded", player.Creature.Block, 6 + count * (up ? 3 : 2));
                });
                if (id == "JG23")
                foreach (int bonus in new[] { 0, 5, 6, 11, 12 })
                foreach (int stacks in new[] { 1, 2 })
                Add(id, up, $"加成阈值{bonus}、叠加{stacks}", async () =>
                {
                    for (int i = 0; i < stacks; i++) await Play(await Card(id, up: up));
                    var c = await Card("JB01"); DamageModifiers.Add(c, bonus); int hp = target.CurrentHp; await Play(c);
                    Check("threshold hits", hp - target.CurrentHp, (6 + bonus) * (1 + bonus / 6 * stacks));
                });
                if (id is "JA15" or "JF07" or "JK82B" or "JA57" or "B" or "JK05" or "JA53" or "JK37B" or "JK43B" or "JH31")
                foreach (bool blocked in new[] { false, true })
                Add(id, up, blocked ? "不可抽牌：独立收益仍结算" : "手牌容量：抽取至上限", async () =>
                {
                    string seed = id == "JK43B" ? "JK43" : id is "JK05" ? "JK15B" : id == "JH31" ? "JA38" : "JA38";
                    var draw = await Card(seed, PileType.Draw);
                    var c = await Card(id, up: up);
                    if (blocked) await PowerCmd.Apply<NoDrawPower>(context, player.Creature, 1, player.Creature, null);
                    else for (int i = 0; i < 9; i++) await Card("JB02");
                    // Arrow resolution happens after deployment has freed its original hand slot.
                    await Play(c);
                    if (!blocked && CardCatalog.IsArrow(c)) await Card("JB02");
                    int handBefore = player.PlayerCombatState!.Hand.Cards.Count;
                    await Finish(c);
                    Check("draw obeys capacity", draw.Pile!.Type, blocked || CardCatalog.IsArrow(c) ? PileType.Draw : PileType.Hand);
                    if (blocked) Check("no draw", player.PlayerCombatState.Hand.Cards.Count, handBefore);
                    if (id == "JK43B") Check("independent cost reduction", CardActions.Cost(draw), up ? 2 : 3);
                });
                if (id is "JX07" or "JH20" or "JH26" or "JA80")
                foreach (int leftover in new[] { 0, 1, 9 })
                Add(id, up, $"能量保留：回合余量{leftover}", async () =>
                {
                    await Play(await Card(id, up: up));
                    if (CardActions.All(player).FirstOrDefault(c => CardCatalog.DesignId(c) == id) is { } c) await Finish(c);
                    int capacity = id == "JX07" ? 2 : id == "JH20" ? up ? 2 : 1 : id == "JH26" ? up ? 2 : 1 : up ? 6 : 5;
                    player.PlayerCombatState!.Energy = leftover; player.PlayerCombatState.ResetEnergy();
                    Check("retained actual leftover", player.PlayerCombatState.Energy, 3 + Math.Min(capacity, leftover));
                    if (player.Creature.GetPower<EnergyReserve>() is { } reserve) await reserve.AfterEnergyReset(player);
                    player.PlayerCombatState.Energy = 9; player.PlayerCombatState.ResetEnergy();
                    Check("retention consumed", player.PlayerCombatState.Energy, 3);
                });
            }
        }
        string planPath = Path.Combine(directory, "card-branches-" + suffix + "-plan.json");
        File.WriteAllText(planPath, JsonSerializer.Serialize(cases.Select(c => new { c.Id, c.Upgraded, c.Branch }), Json));
        float previousSpeed = (float)Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 20;
        try
        {
            using var selectorScope = CardSelectCmd.UseSelector(selection);
            foreach (var c in cases)
            {
                checks.Clear(); string? error = null;
                try { await Reset(); await c.Action(); }
                catch (Exception ex) { error = ex.ToString(); }
                results.Add(new(c.Id, c.Upgraded, c.Branch, error == null && checks.Count > 0 && checks.All(c => c.Pass), checks.ToArray(), error));
                File.WriteAllText(Path.Combine(directory, "card-branches-" + suffix + "-results.json"), JsonSerializer.Serialize(new
                { completed = false, planned = cases.Count, results }, Json));
            }
        }
        finally { Godot.Engine.TimeScale = previousSpeed; }
        File.WriteAllText(Path.Combine(directory, "card-branches-" + suffix + "-results.json"), JsonSerializer.Serialize(new
        { completed = true, planned = cases.Count, results }, Json));
    }
}
