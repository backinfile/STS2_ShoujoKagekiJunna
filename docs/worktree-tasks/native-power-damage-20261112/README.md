# 对照游戏本体简化能力伤害调用

## 原始输入

统一能力伤害入口是啥？你不能参考游戏本体的类似效果修改吗？

沿用本聊天已有合并与清理授权：

当前没有对话正在处理的，你来合并且清理，注意不要覆盖掉主干已经有的最新的内容

## 起点与计划

原始目录为项目主目录，目标分支main，起点7880a48141e8950008e80273c2397b07d1798a5e，工作区干净。隔离位置为../worktrees/STS2_ShoujoKagekiJunna/native-power-damage-20261112。

1. 反编译当前已安装游戏的势不可挡、火焰屏障及水银沙漏，对照伤害属性、命令与预览。
2. 删除上次新增的DealTriggeredDamage私有封装，在三个能力效果中明确调用原生伤害命令；仅保留Beta／Stable签名适配和既有纯那伤害加成作用域。
3. 完成双API构建、定向回归和正常出牌录像，保留最新main后合入并清理本任务工作树。

## 结果

原生对照已完成：JuggernautPower.AfterBlockGained与FlameBarrierPower.AfterDamageReceived直接调用CreatureCmd.Damage，并传ValueProp.Unpowered；MercuryHourglass的DamageVar同样为Unpowered，回合开始调用CreatureCmd.Damage对全部可攻击敌人结算。

原生反编译文件与游戏DLL指纹保存在主目录artifacts/task-deliverables/native-power-damage-20261112/。这次不改变已验证的伤害规则，仅使调用方式直接可读。

删除DealTriggeredDamage后，教科书、合唱练习、保留能量伤害的三个调用点都直接写明ValueProp.Unpowered，并通过既有GameApi转发至原生CreatureCmd.Damage。纯那伤害投入与额外次数继续由原有BeginCardResolution作用域处理，合唱练习保留基础次数和存活目标筛选。

- Beta、Stable及定向测试模组使用-warnaserror编译通过，零警告、零错误。本次仅改C#调用方式，沿用已验证资源包。
- 重新执行junna_power_damage check，18项能力伤害场景与2项普通攻击／箭矢对照共20项全部通过。基础能力伤害不受9力量、1虚弱、1易伤影响；投入2伤害加成及敌人2格挡的场景通过；普通打击与满弦射击保留16／38的攻击修正结果。原始结果归档为results.json。
- 独立随机端口8277、满解锁、最小化静默运行；启动前台命中0/120。录制前准备环境，录制中仅正常悬停和出牌；教科书在三种原生修正同时存在时造成3点伤害，敌人生命10000→9997。
- 录像native-power-damage.mp4共7.03秒、1280×720、30FPS、H.264/AAC、engine_mix音源，封装完成，完整解码和画面抽查通过。
- 全部449份跟踪及新增文档完成本地绝对路径与敏感字段检查，无发现。证据、构建和录像均保存在主目录artifacts/task-deliverables/native-power-damage-20261112/；最终main提交与清理以同目录integration-receipt.json为回执。
