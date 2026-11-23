using MegaCrit.Sts2.Core.Combat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Combat;
using ShoujoKagekiJunna.Models;

namespace JunnaIntegration;

internal sealed partial class AuditScenario
{
    private async Task CheckCurrentPoolEffects()
    {
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
            Check("沸腾的热情将飞矢伤害提高五成", hp - _enemies[0].CurrentHp, 27);
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
            await Play(card);
            Check("温柔的星光同回合重打仍有格挡", player.Creature.Block - block, upgraded ? 4 : 2);
            Check("同一张温柔的星光只在首次返能", state.Energy, energy);
            state.IncrementTurnNumber();
            await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            await Play(card);
            Check("温柔的星光下回合恢复首次返能", state.Energy, energy + 1);
        }
        if (Id == "U_CARD_047")
        {
            int hp = _enemies[0].CurrentHp;
            await Play(await Add("JB01"));
            Check("两人的心跳力量作用于后续攻击", hp - _enemies[0].CurrentHp, upgraded ? 15 : 13);
        }
    }
}
