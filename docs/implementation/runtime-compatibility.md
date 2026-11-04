# 伤害修正接入

## 实例与来源

`DamageModifiers` 将伤害加成、额外伤害次数绑定到实际卡牌实例，战斗开始和结束时清理。移动牌区保留同一份数据；复制牌继承当前数值，之后各自变化。新生成的其他牌从自己的数值开始。

`PowerCmd.Apply` / `ModifyAmount` 在实际增加层数时登记施加快照，嵌套包装调用只登记一次。多个实际施加分别记录并相加。后续强化原牌不回写已有状态，完全移除状态时清除记录。充能球在生成时绑定来源。

状态、充能球和遗物的异步方法携带来源作用域，覆盖毒伤这类自行创建 `PlayerChoiceContext` 的结算。嵌套反击使用自身来源。此部分在每场战斗开始时安装，覆盖届时已加载的模组程序集。

## 伤害与段数

- `Hook.ModifyDamage` 的加法阶段加入伤害加成，力量、易伤、格挡及最终伤害继续由本体处理。
- `Hook.ModifyAttackHitCount` 为 `AttackCommand` 增加段数。原本 3 段、额外 1 次时执行 4 段。
- `AttackCommand` 内部伤害调用标记次数已经处理，避免底层再扩一次。新增段跳过 `BeforeDamage` 等非伤害委托，保留本体命中结算与攻击触发。
- 单独调用 `CreatureCmd.Damage` 的直接伤害也会应用加成和额外次数。自伤与友方伤害保持原数值和次数。
- 等待中的满弦射击仍持有实际牌，到期读取该牌当前修正；已建立的状态读取施加时快照。

## 其他模组

使用本体 `AttackCommand`、`CreatureCmd.Damage`、`PowerCmd`、`OrbCmd` 的牌走上述通路。伤害资格通过牌模型、辅助方法、异步状态机及其建立的效果调用关系识别，不依赖“攻击牌”类型或 `DamageVar`。

自定义调度器、原生实现、直接修改生命值和手动循环多个伤害命令需要对应适配。每次独立的 `CreatureCmd.Damage` 默认视作一条伤害指令；若外部模组将多个调用定义为同一多段攻击，应由其适配器先组合段数，并逐次标记已经处理次数的底层调用。

公开接口：

```csharp
DamageCapability.Register<MyCard>(card => /* 此形态是否具有伤害能力 */ true);
DamageModifiers.Add(card, damageBonus: 2, extraHits: 1);
var snapshot = DamageModifiers.CaptureDamageModifierSnapshot(card);
DamageModifiers.BindDamageSource(effect, snapshot, applicationId);

using (DamageExecution.UseDamageSource(effect))
{
    // 调用本体伤害指令；来源跨 await 保留。
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

`applicationId` 在一次实际施加的多个包装层之间复用；下一次真实施加使用新编号。无法解析的伤害能力可通过 `Register` 补齐，独立效果可通过 `BindDamageSource` 与 `UseDamageSource` 补齐来源。

## 必要验证范围

集成测试使用游戏真实伤害命令和可变卡牌：本体 TwinStrike、DeadlyPoison；纯那回身斩、引路星与耐力训练；独立测试程序集中的无 `DamageVar` 技能。检查非伤害效果次数、力量/易伤顺序、状态叠加去重、复制隔离、自伤和无绑定效果。华恋 v0.1.6 的实际 `KAREN_STRIKE` 联用结果独立记录。

录像检查真实选牌交互、04/02 立绘切换、箭矢牌区和 3→2→1 倒计时。结果保存在 `artifacts/dev-game/`。
