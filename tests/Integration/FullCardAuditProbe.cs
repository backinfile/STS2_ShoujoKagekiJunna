using MegaCrit.Sts2.Core.Localization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using BaseLib.Utils.Attributes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

public sealed class FullCardAuditCommand : AbstractConsoleCmd
{
    public static void Initialize()
    {
        ModHelper.SubscribeForCombatStateHooks("JunnaFullCardAudit",
            _ => [ModelDb.GetById<AuditObserver>(ModelDb.GetId<AuditObserver>())]);
        long last = 0;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame += () =>
        {
            if (_prepared == null || System.Environment.TickCount64 - last < 100) return;
            last = System.Environment.TickCount64;
            _prepared.WriteReadiness();
        };
    }
    private static AuditScenario? _prepared;
    public override string CmdName => "junna_full_audit";
    public override string Args => "check [id] | combos [id] | prepare <id> [upgrade] | inspect";
    public override string Description => "Verify each current card and prepare legal-action recordings.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? player, string[] args)
    {
        if (player?.PlayerCombatState == null) return new(false, "Combat required");
        return new(Run(player, args), true, "Per-card audit started");
    }
    private static async Task Run(Player player, string[] args)
    {
        Directory.CreateDirectory(AuditScenario.Output);
        if (args.FirstOrDefault() == "prepare")
        {
            _prepared = new(player, CardCatalog.Get(args[1]), args.Contains("upgrade"), "normal");
            await _prepared.Prepare(recording: true);
            return;
        }
        if (args.FirstOrDefault() == "inspect")
        {
            _prepared!.CheckResult();
            _prepared.Save("video-result");
            return;
        }
        double speed = Engine.TimeScale;
        Engine.TimeScale = 8;
        try
        {
            foreach (var definition in AuditExpectations.Cards
                         .Where(card => args.Length < 2 || card.Id == args[1]))
            foreach (bool upgraded in new[] { false, true })
            foreach (string variant in args.FirstOrDefault() == "combos" ? new[] { "combo" } : new[] { "normal", "empty", "combo", "capacity" })
            {
                var scenario = new AuditScenario(player, definition, upgraded, variant);
                try
                {
                    await scenario.Prepare();
                    await scenario.CheckDisplayAndRejections();
                    await scenario.Execute();
                    scenario.CheckResult();
                    await scenario.CheckAdditional();
                }
                catch (Exception error) { scenario.Error(error); }
                scenario.Save("result");
            }
        }
        finally { Engine.TimeScale = speed; }
        File.WriteAllText(Path.Combine(AuditScenario.Output, "completed" + (args.Length > 1 ? "-" + args[1] : "") + ".json"),
            JsonSerializer.Serialize(new { revision = CardCatalog.Revision, completed = true }));
    }
}

internal sealed partial class AuditScenario(Player player, CardDefinition definition, bool upgraded, string variant)
{
    internal static string Output => Path.GetFullPath("../../../out/tmp/full-card-tests/results");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly ThrowingPlayerChoiceContext _context = new();
    private readonly List<object> _checks = [];
    private readonly List<CardModel> _helpers = [];
    private Creature[] _enemies = [];
    private CardModel _source = null!;
    private CardModel? _selected;
    private CardModel? _arrow;
    private int[] _beforeHp = [];
    private int _beforeEnergy;
    private int _beforeHand;
    private int _beforeDraw;
    private int _beforeDeck;
    private int _beforeDepletedDesires;
    private int _categoryCount;
    private bool _recording;
    private string Id => definition.Id;
    private string Text => upgraded ? definition.UpgradedText : definition.Text;
    private bool Empty => variant == "empty";
    private bool Enhanced => variant == "combo";
    private sealed class Selector(CardModel? preferred = null) : ICardSelector
    {
        internal string? Choice;
        internal CardModel? Preferred;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minimum, int maximum)
            => Task.FromResult(options.OrderBy(card => card == preferred || card == Preferred || card.Id.Entry == Choice ? 0 : 1).Take(maximum));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException();
    }
    internal void Check(string name, object? actual, object? expected)
        => _checks.Add(new { name, actual, expected, pass = Equals(actual, expected) });
    internal void Error(Exception error) => _checks.Add(new { name = "exception", pass = false, error = error.ToString() });
    internal void Save(string suffix)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, $"{Id}-{(upgraded ? "upgrade" : "base")}-{variant}-{suffix}.json"),
            JsonSerializer.Serialize(new { id = Id, definition.Name, upgraded, variant, checks = _checks }, Json));
    }
    internal void WriteReadiness()
    {
        var deployed = ArrowCombat.Deployed.Select(arrow => new
        {
            id = CardCatalog.DesignId(arrow.Card),
            arrow.Remaining,
            resolving = Traverse.Create(arrow).Field<bool>("Resolving").Value
        }).ToArray();
        var status = new
        {
            id = Id,
            updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            playPileCount = player.PlayerCombatState?.PlayPile.Cards.Count ?? 0,
            resolving = deployed.Any(arrow => arrow.resolving),
            deployed
        };
        try
        {
            Directory.CreateDirectory(Output);
            string temporary = Path.Combine(Output, "readiness.tmp");
            File.WriteAllText(temporary, JsonSerializer.Serialize(status));
            File.Move(temporary, Path.Combine(Output, "readiness.json"), overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    private async Task<CardModel> Add(string id, PileType pile = PileType.Hand, bool upgrade = false)
    {
        using var single = KnowledgeCombat.SingleCard();
        var card = CardCatalog.Create(id, player);
        if (upgrade) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(card, pile, pile == PileType.Draw ? CardPilePosition.Bottom : CardPilePosition.Top, skipVisuals: !_recording);
        if (_recording && pile == PileType.Hand && id != Id) card.AddKeyword(CardKeyword.Retain);
        return card;
    }
    private Task<bool> Play(CardModel card) => TestGameApi.PaidPlay(_context, card, _enemies[0]);
    private async Task Finish(CardModel card)
    {
        if (ArrowCombat.Find(card) is { } arrow) await ArrowCombat.Accelerate(_context, arrow, 1000);
    }
    internal async Task Prepare(bool recording = false)
    {
        _recording = recording;
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: !recording); }
        _enemies = player.Creature.CombatState!.HittableEnemies.Where(enemy => enemy.IsAlive).ToArray();
        foreach (var enemy in _enemies)
        {
            foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
            await CreatureCmd.SetMaxHp(enemy, recording ? 1000 : 1000000);
            await CreatureCmd.SetCurrentHp(enemy, recording ? 1000 : 1000000);
            await TestGameApi.LoseBlock(_context, enemy, enemy.Block, null);
        }
        await CreatureCmd.SetMaxHp(player.Creature, recording ? 200 : 100000);
        await CreatureCmd.SetCurrentHp(player.Creature, recording ? 200 : 100000);
        await TestGameApi.LoseBlock(_context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState!.Energy = 20;
        player.PlayerCombatState.IncrementTurnNumber();
        if (!Empty)
        {
            if (Id is "JG03" or "JG27" or "JT02" or "JA84" or "JA87" or "JA88" or "JK84" or "JG37" or "JK200B" or "JG36" or "JA75" or "JK131" or "JH19")
            { _selected = await Add("JB01"); _helpers.Add(_selected); }
            if (Id is "JK11" or "JK11B") _selected = await Add(Id == "JK11" ? "JK11B" : "JK11", PileType.Exhaust);
            if (Id is "JK32" or "JK32B" or "JK32C")
                foreach (string id in new[] { "JK32", "JK32B", "JK32C" }.Where(id => id != Id)) _helpers.Add(await Add(id));
            if (Id is "JA43" or "JA71" or "JX01" or "JH22" or "JA176" or "JA177" or "JA104" or "JA119" or "JA105" or "JA35" or "JA96" or "JK67" or "JK37B" or "JK84B" or "JK362")
            {
                _arrow = await Add("JB03");
                await TestGameApi.PaidPlay(_context, _arrow, Id == "JK37B" ? _enemies[^1] : _enemies[0]);
            }
            if (definition.Knowledge)
            {
                var group = KnowledgeCombat.Group(CardCatalog.Create(Id, player));
                if (group != null)
                    foreach (string related in group.GrantedCardIds.Take(4)) _helpers.Add(await Add(related, PileType.Draw));
            }
            foreach (string id in new[] { "JB01", "JA38", "JK11", "JK133", "JB02", "JA15" })
                _helpers.Add(await Add(id, PileType.Draw));
            if (Id == "U_CARD_010")
                for (int i = 0; i < 3; i++) foreach (string id in new[] { "JK11", "JA38", "JS35" }) _helpers.Add(await Add(id, PileType.Draw));
        }
        _source = await Add(Id, upgrade: upgraded);
        if (Id == "U_CARD_017" && !Empty)
        {
            foreach (string helper in new[] { "JK49B", "JA38", "JS35" }) _helpers.Add(await Add(helper));
            _categoryCount = 3;
        }
        if (Id == "JK126" && !Empty)
        {
            var linked = new[] { _source, await Add("JB01", PileType.Draw), await Add("JA38", PileType.Draw) };
            AccessTools.Method(typeof(DynamicKnowledge), "Merge").Invoke(null, [player, linked]);
        }
        if (variant == "capacity")
            while (player.PlayerCombatState.Hand.Cards.Count < CardPile.MaxCardsInHand) await Add("JB02");
        if (Id == "JA81B") ((CatalogCard)_source).SetGeneratedDamage(13);
        if (Enhanced && definition.DamageCapable && definition.Type != "能力" && Id != "JA82")
            await CreatureCmd.GainBlock(_enemies[0], 7, ValueProp.Unpowered, null);
        if (definition.CostsX) player.PlayerCombatState.Energy = Empty ? 0 : 3;
        if (Id == "JA67") player.PlayerCombatState.Energy = upgraded ? definition.UpgradedCost : definition.Cost;
        if (recording && (CardCatalog.IsArrow(_source) || _arrow != null || definition.Type == "能力"
            || Id is "JT02" or "JA84" or "JA88" or "JK84" or "JG37" or "JA108") && Id != "JA71")
        {
            var complete = await Add("JA71");
            complete.AddKeyword(CardKeyword.Retain);
        }
        if (recording && Id == "JK254")
        {
            await Add("JK11B");
            await Add("JK11");
        }
        if (recording && Id == "JK27") await Add("JK11B", PileType.Draw);
        if (recording && Id == "JA105") await Add("JB03");
        if (recording && Id is "JA22" or "JK37C" or "JA118") await Add("JA38");
        if (recording && Id == "JZ05") await Add("JB01");
        if (recording && Id is "JK235" or "JK260") await Add("JK49B");
        if (recording && Id == "JK314")
        {
            using var single = KnowledgeCombat.SingleCard();
            var main = player.RunState.CreateCard(CardCatalog.Model(Id), player);
            var desires = Enumerable.Range(0, 3).Select(_ => player.RunState.CreateCard(CardCatalog.Model("JK122B"), player)).ToArray();
            var group = desires.Prepend(main).ToArray();
            foreach (var member in group) await CardPileCmd.Add(member, PileType.Deck, skipVisuals: true);
            _source.DeckVersion = main;
            foreach (var permanent in desires)
            {
                Shine.SetState(permanent, 1, 3);
                var desire = player.Creature.CombatState!.CloneCard(permanent);
                desire.DeckVersion = permanent;
                await CardPileCmd.Add(desire, PileType.Hand);
            }
        }
        if (recording && Id is "U_CARD_008" or "JK127" or "JS19")
        {
            foreach (var enemy in _enemies)
            { await CreatureCmd.SetMaxHp(enemy, 6); await CreatureCmd.SetCurrentHp(enemy, 6); }
            await Add("JK275");
            using var single = KnowledgeCombat.SingleCard();
            var knowledge = player.RunState.CreateCard(CardCatalog.Model("JK49B"), player);
            await CardPileCmd.Add(knowledge, PileType.Deck, skipVisuals: true);
            if (Id == "JS19")
            {
                var permanent = player.RunState.CreateCard(CardCatalog.Model(Id), player);
                await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
                _source.DeckVersion = permanent;
                Shine.SetState(permanent, 1, 9);
                Shine.SetState(_source, 1, 9);
            }
        }
        _beforeHp = _enemies.Select(enemy => enemy.CurrentHp).ToArray();
        _beforeEnergy = player.PlayerCombatState.Energy;
        _beforeHand = player.PlayerCombatState.Hand.Cards.Count;
        _beforeDraw = player.PlayerCombatState.DrawPile.Cards.Count;
        _beforeDeck = player.Deck.Cards.Count;
        _beforeDepletedDesires = Shine.GetDepletedCards(player).Count(entry => entry.ModelId == CardCatalog.Model("JK122B").Id.ToString());
        Save("prepared");
        if (recording) WriteReadiness();
    }
    internal async Task CheckDisplayAndRejections()
    {
        Check("名称", _source.Title, definition.Name + (upgraded ? Id == "JK82" ? "+1" : "+" : ""));
        Check("类型", _source.Type, definition.CardType);
        Check("稀有度", _source.Rarity, definition.CardRarity);
        Check("费用", _source.EnergyCost.GetWithModifiers(CostModifiers.None), upgraded ? definition.UpgradedCost : definition.Cost);
        Check("X费用", _source.EnergyCost.CostsX, definition.CostsX);
        Check("知识关键词", CardCatalog.IsKnowledge(_source), definition.Knowledge);
        string description = _source.GetDescriptionForPile(PileType.Hand);
        Check("本地化完整", !description.Contains("ERROR") && !description.Contains('{') && !description.Contains('}'), true);
        Check("卡图资源", ResourceLoader.Exists(((JunnaCard)_source).CustomPortraitPath), true);
        if (Id == "JA108")
        {
            int count = player.PlayerCombatState!.DrawPile.Cards.Count(CardCatalog.IsArrow);
            Check("战斗内动态飞矢张数", description.Contains($"（打出{count}张）"), true);
            Check("战斗外隐藏动态备注", CardCatalog.Model(Id).GetDescriptionForPile(PileType.Deck).Contains("（打出"), false);
            if (Enhanced)
            {
                var added = await Add("JB03", PileType.Draw);
                Check("飞矢入堆后更新备注", _source.GetDescriptionForPile(PileType.Hand).Contains($"（打出{count + 1}张）"), true);
                await CardPileCmd.Add(added, PileType.Discard, skipVisuals: true);
                Check("飞矢离开抽牌堆更新备注", _source.GetDescriptionForPile(PileType.Hand).Contains($"（打出{count}张）"), true);
            }
        }
        foreach (var value in definition.Values)
        {
            if (!_source.DynamicVars.ContainsKey(value.Name)) continue;
            Check("动态数值 " + value.Name, _source.DynamicVars[value.Name].BaseValue,
                Id == "JA81B" && value.Name == "Damage" ? 13m : (decimal)(upgraded ? value.Upgraded : value.Base));
        }
        if (!definition.CostsX && CardActions.Cost(_source) > 0)
        {
            player.PlayerCombatState!.Energy = CardActions.Cost(_source) - 1;
            Check("能量不足拒绝打出", await Play(_source), false);
            Check("拒绝后保留手牌", _source.Pile?.Type, PileType.Hand);
            Check("拒绝后没有伤害", _enemies[0].CurrentHp, _beforeHp[0]);
            player.PlayerCombatState.Energy = _beforeEnergy;
        }
        _source.AddKeyword(CardKeyword.Unplayable);
        Check("不可打出拒绝结算", await Play(_source), false);
        Check("拒绝后能量", player.PlayerCombatState!.Energy, _beforeEnergy);
        _source.RemoveKeyword(CardKeyword.Unplayable);
    }
    internal async Task Execute()
    {
        using var selector = TestGameApi.UseSelector(new Selector(_selected));
        AuditObserver.Drawn.Clear();
        Check("支付并打出", await Play(_source), true);
        if (ArrowCombat.Find(_source) is { } arrow)
        {
            int turns = definition.Values.Single(value => value.Name == "Turns") is { } number
                ? upgraded ? number.Upgraded : number.Base : 0;
            Check("布置初始计数", arrow.Remaining, turns);
            Check("布置阶段等待伤害", _enemies[0].CurrentHp, _beforeHp[0]);
            if (_arrow == null && arrow.Remaining > 1)
            {
                player.PlayerCombatState!.IncrementTurnNumber();
                var hooks = ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>());
                await hooks.AfterPlayerTurnStartLate(_context, player);
                int remaining = arrow.Remaining;
                Check("自然倒计时一步", remaining, turns - 1);
                await hooks.AfterPlayerTurnStartLate(_context, player);
                Check("同回合倒计时去重", arrow.Remaining, remaining);
            }
            await Finish(_source);
        }
    }
    internal void CheckResult()
    {
        var match = Regex.Match(Text, @"(?:造成|敌人造成)(\d+)点伤害(?:([0-9]+)次)?");
        int damage = match.Success ? int.Parse(match.Groups[1].Value) : 0;
        int hits = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
        if (Id == "JK314") hits += _beforeDepletedDesires;
        if (Id == "U_CARD_017") hits += _categoryCount;
        if (Id == "JA81B") damage = 13;
        if (Id == "JK48") damage += player.Deck.Cards.Count(CardCatalog.IsKnowledge);
        if (Id == "JK206") damage = _beforeDeck + (upgraded ? 8 : 0);
        if (Id == "JA82") damage = 0;
        if (definition.Type == "能力") damage = 0;
        int expected = damage * hits;
        if (Id is not ("U_CARD_005" or "JZ05"))
        {
            for (int i = 0; i < _enemies.Length; i++)
            {
                int supportDamage = !Empty && Id is "JA71" ? 25 : 0;
                bool group = _source.TargetType == TargetType.AllEnemies;
                int shield = i == 0 && Enhanced && definition.DamageCapable && definition.Type != "能力" && Id != "JA82" ? 7 : 0;
                Check("目标" + i + "伤害", _beforeHp[i] - _enemies[i].CurrentHp, Math.Max(0, (i == 0 || group ? expected : 0) - shield) + (i == 0 ? supportDamage : 0));
            }
        }
        var blockMatch = Regex.Match(Text, @"获得(\d+)点格挡");
        int block = blockMatch.Success ? int.Parse(blockMatch.Groups[1].Value) : 0;
        if (definition.Type == "能力") block = 0;
        if (Id == "JK48B") block += player.Deck.Cards.Count(CardCatalog.IsKnowledge);
        if (_recording && CardCatalog.IsArrow(_source)) block += 12;
        Check("格挡", player.Creature.Block, block);
        if (definition.Type == "能力")
            Check("能力安装", player.Creature.Powers.OfType<CatalogPower>().Any(power => power.DesignId == Id), true);
        else if (Id != "JA67")
            Check("结算后去向", _source.Pile?.Type ?? PileType.None,
                (upgraded ? definition.UpgradedExhaust : definition.Exhaust) ? PileType.Exhaust : PileType.Discard);
        Check("结算不永久增牌", player.Deck.Cards.Count, _beforeDeck);
        if (Id is "JA81" or "JA76" or "JA82")
        {
            var generated = ArrowCombat.Deployed.Where(arrow => CardCatalog.DesignId(arrow.Card) == "JA81B").ToArray();
            Check("流星数量", generated.Length, Id == "JA82" ? _beforeEnergy : Id == "JA76" ? _enemies.Length : 1);
            Check("流星版本", generated.All(arrow => !arrow.Card.IsUpgraded && arrow.Remaining == 2), true);
        }
        if (Id is "JK272" or "JK272B" or "JA58")
            foreach (var enemy in _enemies)
            {
                if (Id != "JK272B") Check("虚弱层数", enemy.GetPower<WeakPower>()?.Amount, upgraded ? 3 : 2);
                if (Id != "JK272") Check("易伤层数", enemy.GetPower<VulnerablePower>()?.Amount, upgraded ? 3 : 2);
            }
        if (_selected != null)
        {
            if (Id == "JT02") { Check("改编后飞矢", CardCatalog.IsArrow(_selected), true); Check("改编后费用", CardActions.Cost(_selected), 0); }
            if (Id is "JK11" or "JK11B") Check("关联牌移入手牌或原生满手牌去向", _selected.Pile?.Type,
                variant == "capacity" ? PileType.Discard : PileType.Hand);
        }
        if (Id == "JA176") Check("布置复制数量", ArrowCombat.Deployed.Count, Empty ? 0 : 2);
        if (Id is "JK32" or "JK32B" or "JK32C")
            foreach (var partner in _helpers.Where(card => card.Pile?.Type == PileType.Hand))
                Check("三联牌费用归零 " + CardCatalog.DesignId(partner), CardActions.Cost(partner), 0);
        if (Id is "JA84" or "JA88" or "JK84" or "JG37")
        {
            if (_selected != null) Check("手牌作为五回合飞矢", ArrowCombat.Find(_selected)?.Remaining, 5);
        }
        if (Id == "JA65")
            Check("抽牌堆飞矢免费打出", ArrowCombat.Deployed.Count, Empty ? 0 : 1);
        if (Id == "JA67") Check("连珠箭重新布置", ArrowCombat.Find(_source)?.Remaining, 2);
        if (Id == "JA17") Check("下回合能量登记", player.Creature.GetPower<EnergyNextTurnPower>()?.Amount, 2);
        if (Id == "JK37B" && _arrow != null) Check("命中注定转移已有飞矢目标", ArrowCombat.Find(_arrow)?.Target == _enemies[0], true);
        if (Id == "JK84B" && _arrow != null) Check("论辩伤害后的加速仅一次", ArrowCombat.Find(_arrow)?.Remaining, 2);
        if (Id == "JH24") Check("最大能量增加", player.PlayerCombatState!.MaxEnergy, 4);
        if (Id == "JA105" && _arrow != null)
        {
            Check("安装时已有飞矢保持三回合", ArrowCombat.Find(_arrow)?.Remaining, 3);
            Check("安装时已有飞矢保留关键词", _arrow.Keywords.Contains(CardKeyword.Exhaust), false);
        }
        if (Id == "U_CARD_005") Check("星屑衍生布置", ArrowCombat.Deployed.Count, 2);
        if (Id is "JA43" or "JA177")
        {
            int acceleration = Id == "JA43" ? upgraded ? 2 : 1 : upgraded ? 2 : 1;
            if (_arrow != null) Check("原飞矢加速", ArrowCombat.Find(_arrow)?.Remaining, 3 - acceleration);
        }
        if (Id == "U_CARD_009") Check("三类别最大配对数量", player.PlayerCombatState!.Hand.Cards.Count - (_beforeHand - 1), Empty ? 0 : Math.Min(3, CardPile.MaxCardsInHand - _beforeHand + 1));
        if (definition.Type != "能力")
        {
            var draw = Regex.Match(Text, @"抽(\d+)张牌");
            if (Id == "JK365") draw = Match.Empty; // Its ordinary draw and linked draws are checked together below.
            if (draw.Success)
                Check("普通抽牌数量", _beforeDraw - player.PlayerCombatState!.DrawPile.Cards.Count,
                    Empty ? 0 : Math.Min(int.Parse(draw.Groups[1].Value), CardPile.MaxCardsInHand - _beforeHand + 1));
            var energy = Regex.Match(Text, @"获得(\d+)点能量");
            if (energy.Success && !Text.Contains("下回合") && Id is not ("JH24" or "JK137"))
                Check("即时能量", player.PlayerCombatState!.Energy - _beforeEnergy,
                    int.Parse(energy.Groups[1].Value) - (upgraded ? definition.UpgradedCost : definition.Cost));
        }
        Check("手牌容量", player.PlayerCombatState!.Hand.Cards.Count <= CardPile.MaxCardsInHand, true);
        if (Id == "JA53")
        {
            var drawn = AuditObserver.Drawn.ToArray();
            Check("备箭只抽一张实际零费牌", drawn.Length, Empty ? 0 : 1);
            Check("备箭排除正费用及X费牌", drawn.All(c => CardActions.HasNumericCost(c) && CardActions.Cost(c) == 0), true);
        }
        if (Id == "JA108")
        {
            Check("抽牌堆飞矢全部离开原牌堆", player.PlayerCombatState.DrawPile.Cards.Any(CardCatalog.IsArrow), false);
            Check("抽牌堆实际飞矢布置", _helpers.Where(CardCatalog.IsArrow).All(c => ArrowCombat.Find(c) != null), true);
        }
        if (Id == "JA145")
            Check("瞄准射击生成基础更近一步", player.PlayerCombatState.Hand.Cards.Count(c => CardCatalog.DesignId(c) == "JA119" && !c.IsUpgraded),
                Math.Min(upgraded ? 2 : 1, CardPile.MaxCardsInHand - _beforeHand + 1));
        if (Id is "JK206B" or "JK235B")
        {
            var received = player.PlayerCombatState.Hand.Cards.Except(_helpers.Where(c => c == _selected)).ToArray();
            if (variant != "capacity") Check("知识检索限定类别", received.All(CardCatalog.IsKnowledge), true);
            if (Id == "JK206B") Check("查阅为移牌而非抽牌", AuditObserver.Drawn.Count, 0);
        }
        if (Id == "JK126")
        {
            Check("抽取动态关联的全部同名实际牌", AuditObserver.Drawn.Count, Empty ? 0 : Math.Min(4, CardPile.MaxCardsInHand - _beforeHand + 1));
            Check("动态关联抽牌排除无关牌", AuditObserver.Drawn.All(c => CardCatalog.DesignId(c) is "JB01" or "JA38"), true);
        }
        if (Shine.GetState(_source) is { } shine)
            Check("闪耀完整结算扣一次", shine.Remaining, shine.Maximum - 1);
    }
    internal async Task CheckAdditional()
    {
        var selection = new Selector(_selected);
        using var selector = TestGameApi.UseSelector(selection);
        if (definition.Type == "能力")
        {
            var power = player.Creature.Powers.OfType<CatalogPower>().First(p => p.DesignId == Id);
            if (Id is "JK32" or "JA35" or "JA144" or "JH59" or "U_CARD_010")
            {
                int hand = player.PlayerCombatState!.Hand.Cards.Count;
                int energy = player.PlayerCombatState.Energy;
                player.PlayerCombatState.IncrementTurnNumber();
                if (Id == "JA144") await power.BeforeHandDraw(player, _context, player.Creature.CombatState!);
                else await power.AfterPlayerTurnStart(_context, player);
                if (Id == "JH59") Check("北极星回合触发", player.PlayerCombatState.Energy - energy, 1);
                if (Id == "JK32") Check("掌舵回合抽牌", player.PlayerCombatState.Hand.Cards.Count - hand,
                    Empty ? 0 : Math.Min(upgraded ? 2 : 1, CardPile.MaxCardsInHand - hand));
                if (Id == "JA144") Check("稳步前进生成", player.PlayerCombatState.Hand.Cards.Count - hand, 1);
                if (Id == "JA35" && _arrow != null)
                    Check("回合开始飞矢加速", ArrowCombat.Find(_arrow)?.Remaining ?? 0,
                        Math.Max(0, 3 - Regex.Match(Text, @"加速(\d+)回合").Groups[1].ValueAsInt()));
                if (Id == "U_CARD_010")
                {
                    int expected = Empty ? 0 : Math.Min(1, CardPile.MaxCardsInHand - hand);
                    Check("首次知识检索", player.PlayerCombatState.Hand.Cards.Count - hand, expected);
                    foreach (int category in new[] { 0, 2, 1 })
                    {
                        var before = player.PlayerCombatState.Hand.Cards.ToHashSet();
                        expected = Empty ? 0 : Math.Min(1, CardPile.MaxCardsInHand - before.Count);
                        player.PlayerCombatState.IncrementTurnNumber();
                        await power.AfterPlayerTurnStart(_context, player);
                        var added = player.PlayerCombatState.Hand.Cards.Where(card => !before.Contains(card)).ToArray();
                        Check("循环类别 " + category, added.Length == expected && added.All(card => CardTutors.Matches(card, category)), true);
                    }
                }
            }
            if (Id is "JK235" or "JK260" or "JA118" or "JK37C")
            {
                var trigger = await Add(Id is "JA118" or "JK37C" ? "JA38" : "JK49B");
                int block = player.Creature.Block;
                int hp = _enemies.Sum(enemy => enemy.CurrentHp);
                await Play(trigger);
                if (Id == "JK235") Check("教科书知识触发", hp - _enemies.Sum(enemy => enemy.CurrentHp),
                    upgraded ? 4 : 3);
                if (Id == "JK260") Check("每回合首次知识力量", player.Creature.GetPower<StrengthPower>()?.Amount, upgraded ? 3 : 2);
                if (Id == "JA118")
                {
                    Check("箭阵打出飞矢格挡", player.Creature.Block - block, upgraded ? 3 : 2);
                    var description = new MegaCrit.Sts2.Core.Localization.LocString("powers", "JUNNA_CATALOG.smartDescription");
                    power.DynamicVars.AddTo(description);
                    string powerText = Regex.Replace(description.GetFormattedText(), @"\[[^\]]+\]", "");
                    Check("箭阵能力说明数值", powerText.Contains($"获得{(upgraded ? 3 : 2)}点格挡"), true);
                }
                if (Id == "JK37C") Check("零费牌加速一次", ArrowCombat.Deployed.Single().Remaining, 1);
            }
            if (Id == "A")
            {
                var before = player.PlayerCombatState!.DrawPile.Cards.ToArray();
                await power.BeforeSideTurnEnd(_context, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [player.Creature]);
                Check("回合末心得生成数量", player.PlayerCombatState.DrawPile.Cards.Count - before.Length, upgraded ? 2 : 1);
                Check("随机入堆保留原牌序", player.PlayerCombatState.DrawPile.Cards.Where(before.Contains).SequenceEqual(before), true);
            }
            if (Id == "JK67" && _arrow != null)
            {
                await power.BeforeSideTurnEnd(_context, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [player.Creature]);
            }
            if (Id == "JA87" && _selected != null)
            {
                await power.BeforeFlush(_context, player);
                Check("回合末手牌布置", ArrowCombat.Find(_selected)?.Remaining, 5);
            }
            if (Id == "JA22")
            {
                var arrow = await Add("JA38");
                await Play(arrow); await Finish(arrow);
                Check("飞矢完成返回同一手牌", arrow.Pile?.Type, PileType.Hand);
                var exhausted = await Add("JA81B");
                await Play(exhausted); await Finish(exhausted);
                Check("消耗优先于返回手牌", exhausted.Pile?.Type, PileType.Exhaust);
            }
            if (Id == "JA105")
            {
                Check("加速能力说明的触发时点", power.Description.GetFormattedText().Contains("打出"), true);
                var arrow = await Add("JB03");
                await Play(arrow);
                Check("新飞矢初始加速", ArrowCombat.Find(arrow)?.Remaining, 2);
                await Finish(arrow);
                Check("加速赋予消耗", arrow.Pile?.Type, PileType.Exhaust);
                if (Enhanced)
                {
                    await Play(await Add("JA105", upgrade: !upgraded));
                    if (_arrow != null) Check("再次安装保持旧飞矢计数", ArrowCombat.Find(_arrow)?.Remaining, 3);
                    int hp = _enemies[0].CurrentHp;
                    var shortArrow = await Add("JA38");
                    await Play(shortArrow);
                    Check("多层加速使新飞矢立即完成", ArrowCombat.Find(shortArrow) == null, true);
                    Check("立即完成的伤害", hp - _enemies[0].CurrentHp, 18);
                    Check("立即完成后消耗", shortArrow.Pile?.Type, PileType.Exhaust);
                    var material = await Add("JB01");
                    HandArrow.Adapt(material);
                    await Play(material);
                    Check("改编飞矢按后续打出加速", ArrowCombat.Find(material)?.Remaining, 3);
                    await Finish(material);
                    Check("改编飞矢完成后消耗", material.Pile?.Type, PileType.Exhaust);
                }
            }
            if (Id == "U_CARD_008")
            {
                int before = player.Deck.Cards.Count;
                await KnowledgeProgress.WonCombat();
                Check("胜利永久故事数量", player.Deck.Cards.Count - before, 1);
                await KnowledgeProgress.WonCombat();
                Check("胜利回调幂等", player.Deck.Cards.Count - before, 1);
            }
            if (Id == "JK27")
            {
                await ClearCards();
                var drawn = await Add("JK11B", PileType.Draw);
                var partner = await Add("JK11", PileType.Draw);
                var unrelated = await Add("JB02", PileType.Draw);
                await CardPileCmd.Draw(_context, player);
                Check("关联阅读抽取直接关联", partner.Pile?.Type, PileType.Hand);
                Check("关联阅读保留原实例", drawn.Pile?.Type, PileType.Hand);
                Check("关联阅读隔离无关牌", unrelated.Pile?.Type, PileType.Draw);
            }
            if (Id == "JZ05")
            {
                await Play(await Add("JB01"));
                Check("即兴首次事件生成一张", ArrowCombat.Deployed.Count, 1);
                await Play(await Add("JB01"));
                Check("即兴本回合次数上限", ArrowCombat.Deployed.Count, upgraded ? 2 : 1);
            }
            if (Id == "JA96" && _arrow != null)
            {
                await TestGameApi.Damage(_context, player.Creature, 1, ValueProp.Move, _enemies[0]);
                Check("受到攻击后敌人飞矢加速", ArrowCombat.Find(_arrow)?.Remaining, 2);
            }
        }
        if (Id == "JH19")
        {
            player.PlayerCombatState!.Energy = Empty ? 1 : 10;
            int hp = _enemies[0].CurrentHp;
            var attack = await Add("JB01");
            await Play(attack);
            Check("付费重放伤害", hp - _enemies[0].CurrentHp, Empty ? 6 : 12);
            Check("付费重放能量", player.PlayerCombatState.Energy, Empty ? 0 : 8);
        }
        if (Id == "U_CARD_009" && Enhanced) await CheckMatching();
        if (Id == "U_CARD_010" && Enhanced) await CheckCycle();
        if (Id == "JK137")
        {
            selection.Choice = "JUNNA_NOTES_ENERGY";
            int before = player.PlayerCombatState!.Energy;
            await Play(await Add(Id, upgrade: upgraded));
            Check("手札选择能量分支", player.PlayerCombatState.Energy - before, upgraded ? 3 : 2);
            selection.Choice = null;
        }
        if (Id == "JS33")
        {
            selection.Choice = "JUNNA_PREPARE_KNOWLEDGE";
            int eligible = player.PlayerCombatState!.DrawPile.Cards.Count(CardCatalog.IsKnowledge);
            int hand = player.PlayerCombatState.Hand.Cards.Count;
            await Play(await Add(Id, upgrade: upgraded));
            Check("整备选择知识分支", player.PlayerCombatState.Hand.Cards.Count - hand,
                Math.Min(eligible, Math.Min(upgraded ? 3 : 2, CardPile.MaxCardsInHand - hand)));
        }
        if (Id is "JA104" or "JA119" && _arrow != null)
        {
            selection.Choice = "JUNNA_ARROW_TARGET_ENEMY";
            int remaining = ArrowCombat.Find(_arrow)!.Remaining;
            await Play(await Add(Id, upgrade: upgraded));
            Check("选择敌人飞矢分支", ArrowCombat.Find(_arrow)?.Remaining, remaining - 1);
        }
        if (Enhanced && definition.Knowledge)
        {
            var companions = KnowledgeCombat.Group(_source)?.Members.ToArray() ?? [];
            Check("知识组牌名有效", companions.All(member => CardCatalog.Get(member.Id).Id == member.Id), true);
            await CheckKnowledgeCombination();
        }
        if (Enhanced && Regex.Match(definition.Text, @"闪耀(\d+)") is { Success: true } shine)
            await CheckLastShine(int.Parse(shine.Groups[1].Value));
        if (Enhanced && Id == "JA82")
        {
            await ClearCards();
            var one = await Add(Id, upgrade: upgraded);
            player.PlayerCombatState!.Energy = 1;
            await Play(one);
            Check("X等于一的流星数量", ArrowCombat.Deployed.Count, 1);
            Check("X等于一恰好支付", player.PlayerCombatState.Energy, 0);
        }
        if (Enhanced && Id == "JK82")
        {
            await ClearCards();
            var learning = await Add(Id);
            await CardUpgrades.Upgrade(learning, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None);
            await CardUpgrades.Upgrade(learning, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None);
            Check("连续升级等级", learning.CurrentUpgradeLevel, 2);
            Check("连续升级维持零费", CardActions.Cost(learning), 0);
            Check("每次升级单独生成新知", player.PlayerCombatState!.DrawPile.Cards.Count(card => CardCatalog.DesignId(card) == "JK82B"), 2);
            int before = player.PlayerCombatState.Energy;
            await Play(learning);
            Check("连续升级后的能量", player.PlayerCombatState.Energy - before, 4);
        }
        if (Enhanced)
        {
            await CheckAdaptedCard();
            if (definition.Type == "飞矢") await CheckArrowCombination();
            if (Id == "JK314") await CheckDesirePile();
        }
        if (variant == "normal") await CheckCurrentPoolEffects();
    }

    private async Task ClearCards()
    {
        foreach (var card in CardActions.All(player).Concat(player.PlayerCombatState!.PlayPile.Cards).Distinct().ToArray())
        { ArrowCombat.Cancel(card); await CardPileCmd.RemoveFromCombat(card, skipVisuals: true); }
    }

    private async Task ClearEffects()
    {
        await ClearCards();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        await TestGameApi.LoseBlock(_context, player.Creature, player.Creature.Block, null);
        player.PlayerCombatState!.Energy = 40;
    }

    private async Task CheckAdaptedCard()
    {
        await ClearEffects();
        var card = await Add(Id, upgrade: upgraded);
        HandArrow.Adapt(card);
        if (Id == "JA81B") ((CatalogCard)card).SetGeneratedDamage(13);
        int hp = _enemies.Sum(enemy => enemy.CurrentHp);
        await Play(card);
        Check("与改编配合保留实际牌", ArrowCombat.Find(card)?.Card == card, true);
        Check("与改编配合等待五回合", ArrowCombat.Find(card)?.Remaining, 5);
        Check("改编后延迟伤害", _enemies.Sum(enemy => enemy.CurrentHp), hp);
        Check("改编后延迟格挡", player.Creature.Block, 0);
        await Finish(card);
        Check("改编后完整结算结束等待", ArrowCombat.Find(card) == null || Id == "JA67", true);
        if (definition.Type == "能力")
            Check("改编能力在完成时安装", player.Creature.Powers.OfType<CatalogPower>().Any(power => power.DesignId == Id), true);
        if (Regex.IsMatch(definition.Text, @"闪耀\d+"))
            Check("改编牌闪耀仅扣一次", Shine.GetState(card)?.Remaining, Shine.GetState(card)!.Maximum - 1);
    }

    private async Task CheckArrowCombination()
    {
        await ClearEffects();
        await Play(await Add("JA105"));
        await Play(await Add("JA22"));
        var card = await Add(Id, upgrade: upgraded);
        if (Id == "JA81B") ((CatalogCard)card).SetGeneratedDamage(13);
        int turns = HandArrow.Turns(card);
        await Play(card);
        var deployment = ArrowCombat.Find(card);
        Check("与加速配合打出即推进", deployment?.Remaining ?? 0, Math.Max(0, turns - 1));
        if (deployment != null)
        {
            await ArrowCombat.CopyAll(_context, player);
            var copy = ArrowCombat.Deployed.Single(arrow => arrow.Card != card);
            Check("星群复制保持倒计时", copy.Remaining, deployment.Remaining);
            Check("星群复制保持升级版本", copy.Card.IsUpgraded, upgraded);
            Check("星群复制保持消耗去向", copy.ExhaustAfterResolve, true);
            await Finish(card);
        }
        Check("加速飞矢完成时消耗优先于上弦", card.Pile?.Type, PileType.Exhaust);
    }

    private async Task CheckDesirePile()
    {
        await ClearEffects();
        using var single = KnowledgeCombat.SingleCard();
        var main = player.RunState.CreateCard(CardCatalog.Model(Id), player);
        if (upgraded) { main.UpgradeInternal(); main.FinalizeUpgradeInternal(); }
        int before = Shine.GetDepletedCards(player).Count(entry => entry.ModelId == CardCatalog.Model("JK122B").Id.ToString());
        var desires = Enumerable.Range(0, 4).Select(_ => player.RunState.CreateCard(CardCatalog.Model("JK122B"), player)).ToArray();
        var group = desires.Prepend(main).ToArray();
        foreach (var member in group) await CardPileCmd.Add(member, PileType.Deck, skipVisuals: true);
        var active = player.Creature.CombatState!.CloneCard(main);
        active.DeckVersion = main;
        await CardPileCmd.Add(active, PileType.Hand, skipVisuals: true);
        for (int index = 0; index < desires.Length; index++)
        {
            Shine.SetState(desires[index], 1, 3);
            var desire = player.Creature.CombatState.CloneCard(desires[index]);
            desire.DeckVersion = desires[index];
            Shine.SetState(desire, 1, 3);
            await CardPileCmd.Add(desire, PileType.Hand, skipVisuals: true);
            await Play(desire);
            Check("直接统计耗尽牌堆 " + (index + 1), DepletedPartnerCount(active), before + index + 1);
            await KnowledgeProgress.Depleted((CatalogCard)desire, new(desire, desires[index], player, "audit-repeat"));
            Check("重复回调不会改变耗尽牌堆数量 " + (index + 1), DepletedPartnerCount(active), before + index + 1);
        }
        int hp = _enemies[0].CurrentHp;
        await Play(active);
        Check("耗尽牌堆次数结算", hp - _enemies[0].CurrentHp, (upgraded ? 12 : 8) * (2 + before + desires.Length));
    }

    private static int DepletedPartnerCount(CardModel card) =>
        Shine.GetDepletedCards(card.Owner).Count(entry => entry.ModelId == CardCatalog.Model("JK122B").Id.ToString());

    private async Task CheckMatching()
    {
        int[][] configurations = [[7], [1, 3, 4], [3, 3, 4], [7, 7, 7], [3, 3, 3], [1, 1, 1], [0, 0]];
        int[] expectedCounts = [1, 3, 3, 3, 2, 1, 0];
        for (int index = 0; index < configurations.Length; index++)
        {
            await ClearCards();
            var candidates = new List<CardModel>();
            foreach (int mask in configurations[index])
            {
                var candidate = await Add("JB01", PileType.Draw, upgrade: true);
                if ((mask & 1) != 0) HandArrow.Adapt(candidate);
                if ((mask & 2) != 0) candidate.AddKeyword(JunnaKeywords.Knowledge);
                if ((mask & 4) != 0) Shine.SetState(candidate, 2, 3);
                candidates.Add(candidate);
            }
            var unrelated = await Add("JB02", PileType.Draw);
            var source = await Add(Id, upgrade: upgraded);
            player.PlayerCombatState!.Energy = 20;
            AuditObserver.Drawn.Clear();
            await Play(source);
            var actual = player.PlayerCombatState.Hand.Cards.ToArray();
            Check("多标签配置 " + string.Join(',', configurations[index]), actual.Length, expectedCounts[index]);
            Check("匹配保留实际实例和升级", actual.All(card => candidates.Contains(card) && card.IsUpgraded), true);
            Check("不抽无关牌", unrelated.Pile?.Type, PileType.Draw);
            Check("经过正常抽牌事件", AuditObserver.Drawn.Count, expectedCounts[index]);
        }
        await ClearCards();
        var blocked = await Add("JK133", PileType.Draw);
        var sourceBlocked = await Add(Id, upgrade: upgraded);
        await PowerCmd.Apply<NoDrawPower>(_context, player.Creature, 1, player.Creature, null);
        await Play(sourceBlocked);
        Check("禁止抽牌保留原牌", blocked.Pile?.Type, PileType.Draw);
        Check("禁止抽牌不入手", player.PlayerCombatState!.Hand.Cards.Count, 0);
        await PowerCmd.Remove(player.Creature.GetPower<NoDrawPower>()!);
    }

    private async Task CheckLastShine(int maximum)
    {
        await ClearCards();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        using var single = KnowledgeCombat.SingleCard();
        var permanent = player.RunState.CreateCard(CardCatalog.Model(Id), player);
        if (upgraded) { permanent.UpgradeInternal(); permanent.FinalizeUpgradeInternal(); }
        await CardPileCmd.Add(permanent, PileType.Deck, skipVisuals: true);
        Shine.SetState(permanent, 1, maximum);
        var card = player.Creature.CombatState!.CloneCard(permanent);
        card.DeckVersion = permanent;
        Shine.SetState(card, 1, maximum);
        await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
        player.PlayerCombatState!.Energy = 30;
        await Play(card);
        if (ArrowCombat.Find(card) != null)
            Check("最后一次闪耀布置阶段保留预算", Shine.GetState(card)?.Remaining, 1);
        await Finish(card);
        Check("最后一次完整结算耗尽", Shine.GetState(card)?.Remaining ?? 0, 0);
        Check("耗尽移除对应永久牌", player.Deck.Cards.Contains(permanent), false);
        Check("耗尽后清理布置", ArrowCombat.Find(card) == null, true);
    }

    private async Task CheckCycle()
    {
        await ClearCards();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        player.PlayerCombatState!.Energy = 20;
        var source = await Add(Id, upgrade: upgraded);
        await Play(source);
        var cycle = player.Creature.Powers.OfType<CatalogPower>().Single();
        AuditObserver.Drawn.Clear();
        player.PlayerCombatState.IncrementTurnNumber();
        await cycle.AfterPlayerTurnStart(_context, player);
        var arrow = await Add("JA38", PileType.Draw, upgrade: true);
        var knowledge = await Add("JK11", PileType.Draw);
        player.PlayerCombatState.IncrementTurnNumber();
        await cycle.AfterPlayerTurnStart(_context, player);
        Check("空知识阶段仍推进到飞矢", arrow.Pile?.Type, PileType.Hand);
        Check("取牌保留升级", arrow.IsUpgraded, true);
        Check("非当前类别保持抽牌堆", knowledge.Pile?.Type, PileType.Draw);
        await Play(await Add(Id));
        var smart = cycle.SmartDescription;
        cycle.DynamicVars.AddTo(smart);
        string rendered = Regex.Replace(smart.GetFormattedText(), @"\[[^\]]+\]", "");
        Check("独立循环叠加说明保持每次一张", rendered.Contains("选择2张"), false);
        Check("独立循环叠加说明显示两次结算", rendered.Contains("结算") && rendered.Contains('2'), true);
        int before = player.PlayerCombatState.Hand.Cards.Count;
        player.PlayerCombatState.IncrementTurnNumber();
        await cycle.AfterPlayerTurnStart(_context, player);
        Check("新安装独立从知识开始", knowledge.Pile?.Type, PileType.Hand);
        Check("已有实例无闪耀时不取其他类别", player.PlayerCombatState.Hand.Cards.Count - before, 1);
        var pending = await Add("JS35", PileType.Draw, upgrade: true);
        while (player.PlayerCombatState.Hand.Cards.Count < CardPile.MaxCardsInHand) await Add("JB02");
        await PowerCmd.Apply<NoDrawPower>(_context, player.Creature, 1, player.Creature, null);
        player.PlayerCombatState.IncrementTurnNumber();
        await cycle.AfterPlayerTurnStart(_context, player);
        Check("满手牌保持目标", pending.Pile?.Type, PileType.Draw);
        await CardPileCmd.RemoveFromCombat(player.PlayerCombatState.Hand.Cards[0], skipVisuals: true);
        player.PlayerCombatState.IncrementTurnNumber();
        await cycle.AfterPlayerTurnStart(_context, player);
        Check("直接移牌不受禁止抽牌影响", pending.Pile?.Type, PileType.Hand);
        Check("循环检索不产生抽牌事件", AuditObserver.Drawn.Count, 0);
    }

    private async Task CheckKnowledgeCombination()
    {
        await ClearCards();
        foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
        player.PlayerCombatState!.Energy = 99;
        foreach (string id in new[] { "JK254", "JK235", "JK260" }) await Play(await Add(id));
        var source = await Add(Id, upgrade: upgraded);
        var group = KnowledgeCombat.Group(source);
        CardModel? partner = null;
        if (group?.GrantedCardIds.FirstOrDefault() is { } partnerId)
        {
            partner = await Add(partnerId);
            partner.EnergyCost.SetThisCombat(4);
        }
        int hp = _enemies.Sum(enemy => enemy.CurrentHp);
        await Play(source);
        Check("配合知识的力量首次触发", player.Creature.GetPower<StrengthPower>()?.Amount, 2);
        Check("配合教科书造成伤害", hp - _enemies.Sum(enemy => enemy.CurrentHp) >= 3, true);
        if (partner?.Pile?.Type == PileType.Hand) Check("配合融会贯通下一次零费", CardActions.Cost(partner), 0);
        var other = await Add("JK49B");
        await Play(other);
        Check("同回合知识力量上限", player.Creature.GetPower<StrengthPower>()?.Amount, 2);
        player.PlayerCombatState.IncrementTurnNumber();
        await Play(await Add("JK49B"));
        Check("下回合知识力量刷新", player.Creature.GetPower<StrengthPower>()?.Amount, 4);
    }
}

[CustomID("JUNNA_AUDIT_OBSERVER")]
#if STS2_BETA
public sealed class AuditObserver : AbstractModel
#else
public sealed class AuditObserver : SingletonModel
#endif
{
    internal static readonly List<CardModel> Drawn = [];
    public override bool ShouldReceiveCombatHooks => true;
    public override Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        Drawn.Add(card);
        return Task.CompletedTask;
    }
}

internal static class AuditNumber
{
    internal static int ValueAsInt(this Group group) => int.Parse(group.Value);
}
