# 星矢等待补偿战斗模型

本模型用于估计普通纯伤星矢的等待补偿。以同费用即时攻击为参照，比较战斗承伤和击杀回合。结果用于纸面初值，具体卡牌还需分配知识、多段、伤害成长等预算。

## 战斗规则

- 玩家80生命，每回合3能量、抽5张，手牌上限10，能量与格挡按回合刷新。
- 17张牌的固定牌组，以当前4打击、4防御、逐星、更近一步为基底，加入双重打击、飞跃、后空翻和受测牌。受测牌为2张或5张；密集组替换3张打击。
- 基础与升级对照只改变受测牌，其他卡固定。全牌组升级、4能量、抽7张作为额外验证。
- 星矢打出时支付能量，实体留在独立牌堆。每个自己的回合开始递减，归零后才回到弃牌堆。回合数2的牌经过两次敌方行动。
- 星矢锁定出牌时目标。目标死亡后不自动转移伤害，保留实际等待与牌堆占用；记录溢出伤害。
- 加速工具支付真实能量、占用真实手牌。单体、同目标、全体加速及速射分别处理。保留、工具消耗和满手抽牌均进入牌堆规则。

## 敌人与出牌策略

本体来源使用本地提取的A0数值及动作顺序：毛绒伏地虫、小啃兽单体、前后两只小啃兽、卑鄙地精和虔诚雕刻师。三只地精为组合测试，长战靶为自设控制场景。雕刻师与速射环境单列压力测试。

策略在当前已知手牌中搜索两步出牌，估计当前防御、已布置星矢的延期伤害和未知抽牌的平均收益。真实抽牌后重新决策。它只读取当前意图和已知牌面，测试保证更换隐藏牌序不会改变抽牌前的决策。均衡、防守、进攻三种偏好分别复核。

每个对照复用相同敌人HP、初始牌序和随机种子。正式比较和混合费用验证各使用64组不同种子。后续洗牌随实体牌的实际去向自然变化。置信区间按种子聚类，避免把同一随机种子下的多个场景当作完全独立样本。

## 运行

在仓库根目录运行：

```powershell
node --test tools/balance-sim/engine.test.cjs
node tools/balance-sim/run.cjs --samples=64 --contexts=none,starter,normal,batch --candidates=b1u2,b2u3,b3u4,b4u6,b6u8 --pool=output/balance-sim/2026-11-03/main-final/input-pool.json --out=output/balance-sim/2026-11-03/main-final
node tools/balance-sim/run.cjs --samples=64 --policies=balanced,defensive,aggressive --contexts=normal,batch,instant --waits=2 --encounters=worm,nibbits,long,sculptor --candidates=b1u2,b2u3,b3u4,b4u6,b6u8 --pool=output/balance-sim/2026-11-03/main-final/input-pool.json --out=output/balance-sim/2026-11-03/policies
node tools/balance-sim/run.cjs --samples=64 --contexts=normal,batch --costs=1,2 --encounters=worm,nibbits,gremlins,long --candidates=b3u5,b5u7 --pool=output/balance-sim/2026-11-03/main-final/input-pool.json --out=output/balance-sim/2026-11-03/refinement
node tools/balance-sim/validate.cjs
node tools/balance-sim/publish.cjs
```

首次运行可省略`--pool`，工具会读取当前工作台，并把使用的卡牌快照保存为`input-pool.json`。此后的正式比较与验证使用该快照，用户后续编辑不会改变已运行实验的输入。

`results.json`包含各条件均值和配对区间；`battles.csv`记录逐场结果；`comparisons.csv`记录逐条件差值；`example-trace.json`保存一场可检查的出牌记录。最终汇总在`docs/design/data/纯那箭矢等待补偿战斗模拟_20261103.json`。

## 适用范围

本轮覆盖普通单体单段伤害、格挡、抽牌、保留、星矢倒计时与加速。1费即时纯伤参照双重打击的10／14总伤害；2费即时20／28沿用纸面预算。知识组连锁、伤害成长、额外触发、重放、遗物、药水与联机结算是后续扩展项。

校准场景中胜率接近满值，因此主要比较承伤和回合数。失败与未结束的战斗另列胜率和残余生命；内部惩罚值只用于记录，推荐表分别展示真实承伤差和回合差。
