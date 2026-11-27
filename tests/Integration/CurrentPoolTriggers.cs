using MegaCrit.Sts2.Core.Combat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

internal sealed partial class AuditScenario
{
    private async Task CheckDistantStars()
    {
        Check("遥远星辰是独立能力牌", !CardCatalog.IsKnowledge(_source) && KnowledgeCombat.Group(_source) == null, true);
        Check("遥远星辰牌面显示本回合的力量和敏捷", _source.GetDescriptionForPile(PileType.Hand).Contains("在当回合获得"), true);
        await ClearCards();
        await Play(await Add("JB01"));
        Check("支付正费用不触发力量", player.Creature.GetPower<StrengthPower>()?.Amount ?? 0, 0);
        Check("支付正费用不触发敏捷", player.Creature.GetPower<DexterityPower>()?.Amount ?? 0, 0);
        var free = await Add("JB01");
        CostChanges.SetFixed(free, 0);
        await Play(free);
        var strength = player.Creature.GetPower<JunnaDistantStarsStrengthPower>();
        var dexterity = player.Creature.GetPower<JunnaDistantStarsDexterityPower>();
        Check("零费获得1点临时力量", player.Creature.GetPower<StrengthPower>()?.Amount, 1);
        Check("零费获得1点临时敏捷", player.Creature.GetPower<DexterityPower>()?.Amount, 1);
        Check("使用原生临时力量和敏捷的能力期限", strength != null && dexterity != null, true);
        if (strength == null || dexterity == null) return;
        await strength.AfterSideTurnEnd(_context, CombatSide.Player, [player.Creature]);
        await dexterity.AfterSideTurnEnd(_context, CombatSide.Player, [player.Creature]);
        Check("回合结束收回临时力量", player.Creature.GetPower<StrengthPower>()?.Amount ?? 0, 0);
        Check("回合结束收回临时敏捷", player.Creature.GetPower<DexterityPower>()?.Amount ?? 0, 0);
    }

    private async Task CheckNewWorkbenchCards()
    {
        await ClearEffects();
        await ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>()).BeforeCombatStart();
        var source = await Add(Id, upgrade: upgraded);
        switch (Id)
        {
            case "U_CARD_178":
            {
                var material = await Add("JB02", PileType.Discard, upgrade: true);
                await Play(source);
                var arrow = ArrowCombat.Find(material);
                Check("弃牌中的升级原牌作为三回合飞矢打出", arrow is { Remaining: 3 } && material.IsUpgraded, true);
                Check("格挡等到飞矢完成", player.Creature.Block, 0);
                await Finish(material);
                Check("到期结算升级防御", player.Creature.Block, 8);
                Check("材料回到弃牌堆", material.Pile?.Type, PileType.Discard);
                break;
            }
            case "U_CARD_179":
            {
                using var single = KnowledgeCombat.SingleCard();
                var permanent = player.RunState.CreateCard(CardCatalog.Model(Id), player);
                var first = player.RunState.CreateCard(CardCatalog.Model("JK49B"), player);
                var second = player.RunState.CreateCard(CardCatalog.Model("JK49B"), player);
                var plain = player.RunState.CreateCard(CardCatalog.Model("JB01"), player);
                var cards = new[] { permanent, first, second, plain };
                foreach (var card in cards) await CardPileCmd.Add(card, PileType.Deck, skipVisuals: true);
                try
                {
                    var knowledge = player.Deck.Cards.Where(card => CardCatalog.IsKnowledge(card) && card.IsUpgradable).ToArray();
                    source.DeckVersion = permanent;
                    Shine.SetState(permanent, 1, 6);
                    Shine.SetState(source, 1, 6);
                    await Play(source);
                    Check("耗尽后升级不同的永久知识牌", knowledge.Count(card => card.IsUpgraded), upgraded ? 2 : 1);
                    Check("普通牌保留原版本", plain.IsUpgraded, false);
                }
                finally { await CardPileCmd.RemoveFromDeck(cards.Where(player.Deck.Cards.Contains).ToArray(), showPreview: false); }
                break;
            }
            case "U_CARD_181":
            {
                await Play(source);
                Check("飞矢自身出牌保留六回合", ArrowCombat.Find(source)?.Remaining, 6);
                await Play(await Add("JB02"));
                Check("一费牌不加速", ArrowCombat.Find(source)?.Remaining, 6);
                var free = await Add("JB02");
                CostChanges.SetFixed(free, 0);
                await Play(free);
                Check("零费牌加速一回合", ArrowCombat.Find(source)?.Remaining, 5);
                int before = _enemies[0].CurrentHp;
                await Finish(source);
                Check("完成造成五十点伤害", before - _enemies[0].CurrentHp, 50);
                break;
            }
            case "U_CARD_184":
            {
                Check("战斗初始四费", CardActions.Cost(source), 4);
                foreach (var (cost, remaining) in new[] { (0, 3), (1, 2), (0, 2), (2, 1), (3, 0), (4, 0) })
                {
                    var card = await Add("JB02");
                    CostChanges.SetFixed(card, cost);
                    await Play(card);
                    Check($"打出{cost}费后牌面耗能", CardActions.Cost(source), remaining);
                }
                break;
            }
        }
    }

    private async Task CheckDreamButton()
    {
        var arrows = ArrowCombat.Deployed.ToArray();
        Check("纽扣实际布置一张而非仅抽牌", arrows.Length, Empty ? 0 : 1);
        if (arrows.Length != 1) return;
        var arrow = arrows[0];
        Check("纽扣部署实际可选原件", _choiceCandidates.Contains(arrow.Card) || AuditObserver.Drawn.Contains(arrow.Card), true);
        if (variant == "normal") Check("手中无其他材料时部署刚抽出的原牌", AuditObserver.Drawn.SingleOrDefault() == arrow.Card, true);
        Check("纽扣保持完整三回合", arrow.Remaining, 3);
        int hp = _enemies[0].CurrentHp;
        int block = player.Creature.Block;
        await ArrowCombat.Accelerate(_context, arrow, 2);
        Check("两步后材料效果仍未结算", arrow.Remaining == 1 && hp == _enemies[0].CurrentHp &&
            block == player.Creature.Block, true);
        await Finish(arrow.Card);
        Check("材料到期才产生原效果", CardCatalog.DesignId(arrow.Card) == "JB02"
            ? player.Creature.Block - block == 5 : hp - _enemies[0].CurrentHp == 6, true);
        Check("材料结算后离开飞矢区", arrow.Card.Pile?.Type, PileType.Discard);
    }

    private async Task CheckCurrentPoolEffects()
    {
        if (Id is "U_CARD_178" or "U_CARD_179" or "U_CARD_181" or "U_CARD_184")
            await CheckNewWorkbenchCards();

        if (Id == "U_CARD_044")
        {
            var original = player.Deck.Cards.ToArray();
            var tome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
            tome.AncientCard = CardCatalog.Model(Id).Id;
            await RelicCmd.Obtain(tome, player);
            var added = player.Deck.Cards.Except(original).ToArray();
            Check("火花四溅获得两张实体知识牌", added.Length, 2);
            Check("新取得的成员均为火花四溅", added.All(card => CardCatalog.DesignId(card) == Id && CardCatalog.IsKnowledge(card)), true);
            await CardPileCmd.RemoveFromDeck(added, showPreview: false);
            await RelicCmd.Remove(tome);
        }
        if (Id == "JK37")
        {
            await ClearEffects();
            KnowledgeProgress.ResetCombat();
            var source = await Add(Id, PileType.Draw, upgraded);
            var state = player.PlayerCombatState!;
            int turn = state.TurnNumber;
            var turnProperty = AccessTools.Property(state.GetType(), nameof(state.TurnNumber));
            int hp = _enemies[0].CurrentHp, energy = state.Energy;
            var hooks = ModelDb.GetById<JunnaCombatHooks>(ModelDb.GetId<JunnaCombatHooks>());
            try
            {
                // A hook-level opening fixture; native-turn evidence is recorded separately.
                turnProperty.SetValue(state, 1);
                await hooks.BeforeHandDraw(player, _context, player.Creature.CombatState!);
                Check("开局钩子自动布置实际飞矢", ArrowCombat.Find(source)?.Card == source, true);
                Check("开局布置保留完整倒计时", ArrowCombat.Find(source)?.Remaining,
                    upgraded ? definition.Values.Single(v => v.Name == "Turns").Upgraded : definition.Values.Single(v => v.Name == "Turns").Base);
                Check("开局飞矢等待完成才造成伤害", hp - _enemies[0].CurrentHp, 0);
                Check("开局自动打出不支付能量", state.Energy, energy);
                await hooks.BeforeHandDraw(player, _context, player.Creature.CombatState!);
                Check("开局钩子重复调用只布置一次", ArrowCombat.Deployed.Count, 1);
                await Finish(source);
                Check("开局飞矢完成伤害", hp - _enemies[0].CurrentHp, upgraded ? 60 : 50);
                Check("开局飞矢结算后弃置实际牌", source.Pile?.Type, PileType.Discard);
            }
            finally { turnProperty.SetValue(state, turn); }
        }
        if (Id == "JK15")
        {
            await ClearEffects();
            await Play(await Add(Id, upgrade: upgraded));
            Check("回到那一页安装不复制自身", player.PlayerCombatState!.DrawPile.Cards.Count, 0);
            await Play(await Add("JB01"));
            Check("非知识不触发复制", player.PlayerCombatState.DrawPile.Cards.Count, 0);
            var trigger = await Add("JK122", upgrade: true);
            await Play(trigger);
            var copy = player.PlayerCombatState.DrawPile.Cards.Single();
            Check("首次知识产生独立实际副本", copy != trigger && copy.Id == trigger.Id && copy.IsUpgraded, true);
            await Play(await Add("JK49B"));
            Check("同回合只复制一次", player.PlayerCombatState.DrawPile.Cards.Count, 1);
            player.PlayerCombatState.IncrementTurnNumber();
            await Play(await Add("JK49B"));
            Check("下一回合恢复首次复制", player.PlayerCombatState.DrawPile.Cards.Count, 2);
        }
        if (Id == "JK254")
        {
            await ClearEffects();
            await Play(await Add(Id, upgrade: upgraded));
            var partner = await Add("JK11B"); partner.EnergyCost.SetThisCombat(2);
            var inDraw = await Add("JK11B", PileType.Draw);
            var ordinary = await Add("JB01");
            await Play(await Add("JK11"));
            Check("融会贯通关联手牌归零", CardActions.Cost(partner), 0);
            Check("融会贯通无关手牌保持费用", CardActions.Cost(ordinary), 1);
            Check("融会贯通不影响抽牌堆", CardActions.Cost(inDraw), 1);
            int energy = player.PlayerCombatState!.Energy;
            await Play(partner);
            Check("关联牌下次实际零费", player.PlayerCombatState.Energy, energy);
            Check("下次打出后恢复战斗费用", CardActions.Cost(partner), 2);
        }
        if (Id == "JK127")
        {
            await ClearEffects();
            KnowledgeProgress.ResetCombat();
            var target = player.RunState.CreateCard(CardCatalog.Model("JK122"), player);
            target.UpgradeInternal(); target.FinalizeUpgradeInternal();
            using var single = KnowledgeCombat.SingleCard();
            await CardPileCmd.Add(target, PileType.Deck, skipVisuals: true);
            var selection = (Selector)TestGameApi.GetSelector(player)!;
            selection.Preferred = target;
            var beforeCards = player.Deck.Cards.ToHashSet();
            int before = beforeCards.Count;
            await Play(await Add(Id, upgrade: upgraded));
            Check("铭刻安装时不永久发牌", player.Deck.Cards.Count, before);
            var room = new CombatRoom((CombatState)player.Creature.CombatState!);
            var power = player.Creature.Powers.OfType<JunnaPowerJK127>().Single();
            await power.AfterCombatEnd(room);
            var rewards = room.ExtraRewards[player].OfType<KnowledgeCopyReward>().ToArray();
            Check("战斗结束登记一项可选知识复制奖励", rewards.Length, 1);
            Check("领取前没有复制知识", player.Deck.Cards.Count, before);
            var reward = rewards.Single();
            Check("铭刻基础和升级均最多复制一张", reward.Maximum, 1);
            Check("选择已升级知识并领取", await reward.SelectUnsynchronized(), true);
            var added = player.Deck.Cards.Where(c => !beforeCards.Contains(c)).ToArray();
            Check("铭刻领取后单独复制一张知识", added.Length, 1);
            Check("铭刻复制保留升级及独立对象", added.SingleOrDefault() is { } copy && copy != target && copy.Id == target.Id && copy.IsUpgraded, true);
            await reward.SelectUnsynchronized();
            Check("重复领取已结算奖励不会复制", player.Deck.Cards.Count, before + 1);
            selection.Preferred = null;
        }
        if (Id == "U_CARD_012")
        {
            await ClearEffects();
            var deployed = await Add(Id, upgrade: upgraded);
            await Play(deployed);
            var deployment = ArrowCombat.Find(deployed)!;
            var target = deployment.Target;
            int remaining = deployment.Remaining;
            var followers = new List<CardModel> { deployed };
            foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
                followers.Add(await Add(Id, pile, upgraded));
            await Play(await Add("JA38"));
            Check("布置中的追逐保留原本的部署", ReferenceEquals(ArrowCombat.Find(deployed), deployment), true);
            Check("布置中的追逐保留目标和倒计时", deployment.Target == target && deployment.Remaining == remaining, true);
            Check("追逐自动打出其他牌区的实际对象", followers.Skip(1).All(c => ArrowCombat.Find(c)?.Remaining == 1), true);
            Check("追逐无递归重复布置", ArrowCombat.Deployed.Count, 6);
            int hp = _enemies[0].CurrentHp;
            int damage = deployed.DynamicVars.Damage.IntValue;
            await Finish(followers[0]);
            Check("追逐完成只造成一次本牌伤害", hp - _enemies[0].CurrentHp, damage);
            Check("追逐完成不再次触发自动打出", ArrowCombat.Deployed.Count, 5);
        }
        if (Id == "U_CARD_013")
        {
            await ClearEffects();
            await Play(await Add(Id, upgrade: upgraded));
            async Task<int> Damage()
            {
                int hp = player.Creature.CurrentHp;
                await TestGameApi.Damage(_context, player.Creature, 10, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, _enemies[0]);
                return hp - player.Creature.CurrentHp;
            }
            Check("牢笼没有飞矢目标时无减伤", await Damage(), 10);
            var first = await Add("JA38"); await Play(first);
            Check("牢笼动态接纳新飞矢目标", await Damage(), 5);
            var second = await Add("JA38"); await Play(second);
            await Play(await Add(Id, upgrade: upgraded));
            Check("同目标飞矢及重复牢笼不重复减伤", await Damage(), 5);
            ArrowCombat.Cancel(first); ArrowCombat.Cancel(second);
            Check("飞矢离场动态解除减伤", await Damage(), 10);
            var power = player.Creature.GetPower<JunnaCagedPower>()!;
            await power.AfterSideTurnEnd(_context, CombatSide.Enemy, _enemies);
            Check("敌方回合结束移除牢笼", player.Creature.GetPower<JunnaCagedPower>() == null, true);
        }
        if (Id == "JA205")
        {
            int hp = _enemies[0].CurrentHp;
            await Play(await Add("JB01"));
            Check("沸腾的热情不修改普通攻击", hp - _enemies[0].CurrentHp, 6);
            var arrow = await Add("JA38");
            await Play(arrow);
            hp = _enemies[0].CurrentHp;
            await Finish(arrow);
            Check("沸腾的热情按版本提高飞矢伤害", hp - _enemies[0].CurrentHp, upgraded ? 27 : 22);
            var power = player.Creature.GetPower<JunnaPowerJA205>()!;
            string description = power.HoverTips.OfType<HoverTip>().First().Description;
            Check("沸腾的热情提示显示实际增伤", description.Contains(upgraded ? "50[/blue]%" : "25[/blue]%"), true);
        }
        if (Id == "JK400B")
        {
            var power = player.Creature.Powers.OfType<JunnaPowerJK400B>().Single();
            Check("翱翔在天际安装时不布置流星", ArrowCombat.Deployed.Count, 0);
            for (int turn = 1; turn <= 2; turn++)
            {
                await power.BeforeSideTurnEnd(_context, CombatSide.Player, [player.Creature]);
                Check("结束阶段各打出一张流星 " + turn, ArrowCombat.Deployed.Count, turn);
                player.PlayerCombatState!.IncrementTurnNumber();
            }
            Check("翱翔在天际的流星均为八点伤害", ArrowCombat.Deployed.All(arrow =>
                CardCatalog.DesignId(arrow.Card) == "JA81B" && arrow.Card.DynamicVars.Damage.BaseValue == 8), true);
        }
        if (Id == "U_CARD_016")
        {
            for (int i = 0; i < 8; i++) await Add("JB01", PileType.Draw);
            foreach (var (trigger, hasShine, expectedDraw) in new[]
                { ("JB01", false, 0), ("JK49B", false, 1), ("JK49B", false, 0),
                    ("JA38", false, 1), ("JB02", true, 1), ("JK49B", false, 1) })
            {
                var card = await Add(trigger);
                if (hasShine) Shine.SetState(card, 2, 3);
                AuditObserver.Drawn.Clear();
                await Play(card);
                Check($"按轮换类别出牌 {trigger} {hasShine}", AuditObserver.Drawn.Count, expectedDraw);
            }
        }
        if (Id == "U_CARD_033")
        {
            int hp = _enemies[0].CurrentHp;
            await Play(await Add("JB01"));
            Check("超常发挥使普通攻击翻倍", hp - _enemies[0].CurrentHp, 12);
            await Play(await Add(Id, upgrade: upgraded));
            Check("同回合重复超常发挥不会延长持续时间", player.Creature.GetPower<DoubleDamagePower>()?.Amount, 1);
        }
        if (Id == "U_CARD_045")
        {
            var state = player.PlayerCombatState!;
            var card = _source;
            int block = player.Creature.Block;
            int energy = state.Energy;
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            Check("温柔的星光同回合重打前取消金色边框", card.ShouldGlowGold, false);
            await Play(card);
            Check("温柔的星光同回合重打仍有格挡", player.Creature.Block - block, upgraded ? 4 : 2);
            Check("同一张温柔的星光只在首次返能", state.Energy, energy);
            state.IncrementTurnNumber();
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            Check("温柔的星光下回合恢复金色边框", card.ShouldGlowGold, true);
            await Play(card);
            Check("温柔的星光下回合恢复首次返能", state.Energy, energy + 1);
            var material = await Add(Id, upgrade: upgraded);
            Check("本回合尚未打出的温柔的星光显示金色边框", material.ShouldGlowGold, true);
            energy = state.Energy;
            block = player.Creature.Block;
            await HandArrow.Play(_context, material);
            Check("温柔的星光成为三回合飞矢", ArrowCombat.Find(material)?.Remaining, 3);
            Check("首次作为飞矢打出立即返能", state.Energy, energy + 1);
            Check("作为飞矢打出后消耗本回合的金色高亮", material.ShouldGlowGold, false);
            Check("飞矢等待时不获得格挡", player.Creature.Block, block);

            state.IncrementTurnNumber();
            energy = state.Energy;
            await Finish(material);
            Check("飞矢完成时获得格挡", player.Creature.Block - block, upgraded ? 4 : 2);
            Check("跨回合完成飞矢不返能", state.Energy, energy);

            energy = state.Energy;
            await CardPileCmd.Add(material, PileType.Hand, skipVisuals: true);
            Check("跨回合完成飞矢后回手仍显示金色边框", material.ShouldGlowGold, true);
            await Play(material);
            Check("飞矢完成后本回合首次普通打出仍返能", state.Energy, energy + 1);
            energy = state.Energy;
            await CardPileCmd.Add(material, PileType.Hand, skipVisuals: true);
            await HandArrow.Play(_context, material);
            Check("普通打出后同回合转飞矢不重复返能", state.Energy, energy);
            await Finish(material);
            Check("同回合完成飞矢不重复返能", state.Energy, energy);

            var copy = CardCopies.ForCombat(material, player, CardCopyPurpose.TemporaryCopy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
            Check("温柔的星光的副本独立显示金色边框", copy.ShouldGlowGold, true);
            await Play(copy);
            Check("温柔的星光的副本独立返能", state.Energy, energy + 1);
        }
        if (Id == "U_CARD_047")
        {
            int hp = _enemies[0].CurrentHp;
            await Play(await Add("JB01"));
            Check("两人的心跳力量作用于后续攻击", hp - _enemies[0].CurrentHp, upgraded ? 16 : 14);
        }
    }
}
