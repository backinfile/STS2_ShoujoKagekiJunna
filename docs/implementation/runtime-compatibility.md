# 伤害修正接入

## 正式版与测试版

发布包使用共同入口与两份实现程序集，分别适配正式版 0.107.1、测试版 0.111.0。入口按游戏版本加载实现，并将实现类型加入游戏的模组反射发现。`src/Compatibility/` 集中处理伤害签名、出牌记录、复制与确认快捷键等 API 差异。正式版模型编号表的类型扫描使用禁止内联的桥接调用，使先行安装的第三方补丁也能扫描随后加载的实现程序集。

与 RitsuLib 联用时，攻击批次补丁在其命中钩子之后安装，保留并调用 `DamageWithAttackHitHooks`，再围住同一批次的伤害次数处理。原先直接替换原版伤害调用会使 RitsuLib 找不到目标，从而中断纯那的补丁安装，表现为能量保留等后续钩子失效。

## 实例与来源

`DamageModifiers` 将伤害加成、额外伤害次数绑定到实际卡牌实例，战斗开始和结束时清理。移动牌区保留同一份数据；复制牌继承当前数值，之后各自变化。新生成的其他牌从自己的数值开始。

纯那的持续能力由 `CatalogPower` 保存每次施加时的来源牌副本、已支付 X 值和独立计数。合唱练习、星芒使用该副本的伤害修正快照；后续强化原牌保持已施加效果的数值。移除能力时，其施加记录随能力实例结束。

状态、充能球和遗物的异步方法携带来源作用域，覆盖毒伤这类自行创建 `PlayerChoiceContext` 的结算。嵌套反击使用自身来源。此部分在每场战斗开始时安装，覆盖届时已加载的模组程序集。

## 伤害与段数

- `Hook.ModifyDamage` 的加法阶段加入伤害加成，力量、易伤、格挡及最终伤害继续由本体处理。
- `Hook.ModifyAttackHitCount` 为 `AttackCommand` 增加段数。原本 3 段、额外 1 次时执行 4 段。
- `AttackCommand` 内部伤害调用标记次数已经处理，避免底层再扩一次。新增段跳过 `BeforeDamage` 等非伤害委托，保留本体命中结算与攻击触发。
- 单独调用 `CreatureCmd.Damage` 的直接伤害也会应用加成和额外次数。自伤与友方伤害保持原数值和次数。
- 等待中的满弦射击仍持有实际牌，到期读取该牌当前修正；已建立的状态读取施加时快照。

## 其他模组

其他模组卡牌直接使用本体 `AttackCommand`、`CreatureCmd.Damage` 时走上述通路。伤害资格通过牌模型、辅助方法和异步状态机识别，不依赖“攻击牌”类型或 `DamageVar`。独立状态、充能球与遗物使用自身伤害来源；第三方持续效果的来源快照由对应适配器建立。

自定义调度器、原生实现、直接修改生命值和手动循环多个伤害命令需要对应适配。每次独立的 `CreatureCmd.Damage` 默认视作一条伤害指令；若外部模组将多个调用定义为同一多段攻击，应由其适配器先组合段数，并逐次标记已经处理次数的底层调用。

公开接口：

```csharp
DamageCapability.Register<MyCard>(card => /* 此形态是否具有伤害能力 */ true);
DamageModifiers.Add(card, damageBonus: 2, extraHits: 1);
var snapshot = DamageModifiers.CaptureDamageModifierSnapshot(card);

using (DamageExecution.UseDamageSource(effect))
{
    // 调用本体伤害指令；来源跨 await 保留。
}

// 保存持续牌效时使用未注册到牌堆的副本；复制会继承当前两种修正。
var sourceSnapshot = (CardModel)card.MutableClone();
using (DamageExecution.BeginCardResolution(sourceSnapshot))
{
    // 结算该次持续牌效，伤害指令的 cardSource 使用 sourceSnapshot。
}

// 外部适配器已组合命中次数时，围住每一次实际的底层伤害调用。
using (DamageExecution.BeginDamageBatch(source, countHandled: true))
{
    // await CreatureCmd.Damage(...);
}

using (DamageExecution.MarkResourceLoss())
{
    // 通过伤害指令表达的支付生命成本。
}
```

自定义调度器通过 `Register` 声明实际伤害资格；直接卡牌结算使用 `BeginCardResolution`，独立效果使用 `UseDamageSource` 区分来源。持续能力适配器负责按实际施加保存与管理副本。

## 必要验证范围

集成测试使用游戏真实伤害命令和可变卡牌：本体 TwinStrike、DeadlyPoison；纯那上挑、引路星与耐力训练；独立测试程序集中的无 `DamageVar` 技能。检查非伤害效果次数、力量/易伤顺序、原生状态独立伤害、复制隔离、自伤和无绑定效果。华恋 v0.1.6 的实际 `KAREN_STRIKE` 联用结果独立记录。完整卡池的持续伤害快照、各牌两版结算与事件边界见 [卡池实现记录](card-effects.md)。

录像检查真实选牌交互、04/02 立绘切换、星矢牌区和 3→2→1 倒计时。结果保存在 `artifacts/dev-game/`。
