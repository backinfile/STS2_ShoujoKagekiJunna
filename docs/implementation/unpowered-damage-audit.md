# 能力触发伤害的原生修正审查

日期：2026-11-12。起因：核对理想的国语教科书buff是否错误接受力量、虚弱、易伤，并审查同类入口。

## 规则与根因

原生StrengthPower、WeakPower、VulnerablePower按ValueProp.IsPoweredAttack判断攻击伤害。ValueProp.Move属于攻击，ValueProp.Unpowered用于能力、遗物等独立效果伤害；Unpowered仍正常受到格挡阻挡。

能力来源的卡牌仍保留为cardSource，继续通过DamageExecution的结算作用域应用纯那的伤害加成和额外次数。修正只改变原生伤害属性，不删除卡牌上的投入。

## 审查结果

| 入口 | 原行为 | 修正与结论 |
| --- | --- | --- |
| JK235 理想的国语教科书，打出知识牌后 | GameApi.Damage传Move | 直接调用原生CreatureCmd.Damage（经GameApi适配），传Unpowered |
| JG34 合唱练习，回合开始时 | CardEffects.DealDamage创建AttackCommand | 按原生水银沙漏的写法调用CreatureCmd.Damage，传Unpowered，对所有存活敌人结算；移除普通攻击入口中的JG34特例 |
| JH30 星芒，能量变化的保留兼容逻辑 | EnergyDamage传Move | 直接调用原生CreatureCmd.Damage（经GameApi适配），传Unpowered；当前卡池及生成模型不存在该牌，保留处理器也修正 |
| CatalogCard能力牌Damage预览 | 全部DamageVar传Move | 能力牌改为Unpowered，预览与触发结算一致 |
| 普通攻击、已布置攻击箭矢及生成流星 | AttackCommand、Move | 属于实际卡牌攻击，保留原生攻击修正 |
| JA82 箭雨及保留JA77 齐射 | 名义为技能或技能箭矢，实际生成攻击或直接攻击 | 保留Move，避免按技能类型误改实际攻击 |
| KnowledgeDamageVar | 用于JK48、JK206的实际攻击 | 保留Move |
| CatalogPower.Block | 已使用Unpowered | 无同类问题 |
| 遗物、耐力标记、职业药水 | 未发现独立直接伤害入口 | 运动饮料投入伤害加成；箭雨药水打出箭矢，箭矢沿用各自攻击规则 |
| GameApi、DamageExecution | 转发调用方伤害属性 | 保留转发规则，避免全局改变普通攻击 |

代码审查覆盖src中的DamageCmd、GameApi.Damage、CreatureCmd.Damage、ValueProp.Move及伤害预览创建处。

## 实机证据

修正前通过正常打出教科书及无直接伤害的知识牌复现：基础3点伤害在9力量下变12，敌人1易伤时变4，自身1虚弱时变2；预览也受到力量及虚弱影响。原始结果：artifacts/task-deliverables/knowledge-pile-anchor-20261112/textbook-damage-before.json。

新增定向命令junna_power_damage check，结果保存至out/tmp/power-damage-isolation/results.json，并归档至同一交付目录。20项全部通过：

- JK235、JG34、保留能量伤害各测基础、9力量、1易伤、1虚弱、投入2伤害加成且三种原生修正同时存在、敌人2格挡，共18项。JK235/JG34分别造成3、3、3、3、5、1点生命损失；能量伤害以2为基础，分别造成2、2、2、2、4、0。格挡正常消耗，能力伤害均未计入攻击事件。
- JK235通过正常PaidPlay安装并正常打出知识牌触发；JG34正常安装后调用实际回合开始处理器；保留能量伤害以真实安装记录直接调用实际EnergyDamage处理器，不假称当前可获取的牌。
- JK235与JG34的预览分别保持3，投入2后为5。普通打击与满弦射击在9力量、1虚弱、1易伤同时存在时分别造成16与38点伤害，各产生1个攻击事件，确认攻击规则保留。
- Beta、Stable及定向测试模组以-warnaserror构建通过，零警告、零错误。

教科书力量／虚弱／易伤同时存在时的正常打牌录像及前后状态归档至artifacts/task-deliverables/knowledge-pile-anchor-20261112/。此前知识光点及buff文案修复证据也保存在该目录。

## 游戏本体同类效果对照

2026-11-12直接检查当前安装游戏的sts2.dll：

| 原生模型及处理器 | 调用方式 | 对应纯那效果 |
| --- | --- | --- |
| JuggernautPower.AfterBlockGained（势不可挡） | CombatTargets随机选敌人，CreatureCmd.Damage传ValueProp.Unpowered | 教科书及保留能量触发的随机伤害 |
| FlameBarrierPower.AfterDamageReceived（火焰屏障） | CreatureCmd.Damage传ValueProp.Unpowered | 能力附带伤害的原生属性依据 |
| MercuryHourglass.AfterPlayerTurnStart（水银沙漏） | DamageVar(3, Unpowered)，CreatureCmd.Damage对HittableEnemies结算 | 合唱练习的回合开始全体伤害及能力伤害预览 |

删除上次新增的DealTriggeredDamage私有封装，三个效果直接写明伤害属性和原生命令。GameApi.Damage仅适配Beta七参数／Stable六参数签名，直接转发给CreatureCmd.Damage；没有另建伤害算法。既有DamageExecution.BeginCardResolution用于读取能力安装时保存的卡牌伤害投入，保留纯那伤害加成和额外次数；原生力量、虚弱、易伤由本体依据Unpowered排除。

原生代码及指纹证据：artifacts/task-deliverables/native-power-damage-20261112/中的native-source.json与native-power-reference-20261112/。后续验证见[本体对照任务记录](../worktree-tasks/native-power-damage-20261112/README.md)。
