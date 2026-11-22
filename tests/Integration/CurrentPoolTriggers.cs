using MegaCrit.Sts2.Core.Combat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
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
            Check("誊写安装时不永久发牌", player.Deck.Cards.Count, before);
            await KnowledgeProgress.WonCombat();
            var added = player.Deck.Cards.Where(c => !beforeCards.Contains(c)).ToArray();
            Check("誊写胜利单独复制一张知识", added.Length, 1);
            Check("誊写复制保留升级及独立对象", added[0] != target && added[0].Id == target.Id && added[0].IsUpgraded, true);
            await KnowledgeProgress.WonCombat();
            Check("誊写胜利处理一次", player.Deck.Cards.Count, before + 1);
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
    }
}
