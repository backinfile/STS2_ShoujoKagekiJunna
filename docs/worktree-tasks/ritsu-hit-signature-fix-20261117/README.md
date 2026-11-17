# RitsuLib 命中接口签名适配

## 用户要求与证据

- 用户原文：“有一个朋友好了，另一个朋友还是打开图鉴出错，是加载顺序有问题”。
- 用户先后提供图鉴空白截图、模组顺序截图与游戏日志。
- 用户原文：“合并并清理”。
- 日志环境为游戏 v0.113.1、BaseLib 3.4.8、Core 1.9.6、RitsuLib 0.6.14。原始日志含私人运行信息，保留在用户提供的位置，不纳入 Git。

## 根因与修改

Core 正常初始化后，纯那的 `AttackBatchPatch.Transpiler` 在绑定 `DamageWithAttackHitHooks` 委托时抛出签名不兼容异常，中断纯那初始化。实际 RitsuLib 攻击补丁调用新增的 `IEnumerable<DamageTargetData>`、`ModifyDamageHookType` 重载，纯那原先只支持旧 `IEnumerable<Creature>` 签名。

- `DamagePatches.cs` 按返回类型和完整参数列表选择桥接方法。
- `DamageExecution.cs` 增加 Beta 目标伤害数据入口，原样传递参数，保留旧入口、命中钩子与批次计数。
- `tests/CompatibilityChecks/Program.cs` 检查实际签名、旧入口，以及两个补丁在真实游戏程序集上的共同安装。
- `docs/implementation/runtime-compatibility.md` 记录新旧入口兼容方式。

## 验证与交付

- 旧包在 `tests/CombatPatchChecks` 精确复现日志中的 `ArgumentException: Cannot bind to the target method because its signature is not compatible with that of the delegate type`。
- 修复后正式版 v0.107.1 与 Beta v0.113.1 的同一专项检查均通过，共 23 项断言；Beta 同时验证旧入口。
- 两套角色实现与加载器构建均为 0 警告、0 错误。本次没有启动游戏执行完整图鉴或多人测试，朋友的实际完整组合需覆盖修复包后确认。
- 修复合包：`dist/Junna-r5427-Core-1.9.6-ritsu-hit-fix-20261117-231839.zip`，包含纯那与 Core 1.9.6。
- ZIP SHA256：`ab04f83b76eca5eb2760ef29ebd212131f547337ce67508dd760a74c8d12c7dc`。

## 集成与清理

修复直接在 `main` 工作目录完成，起点为 `7e6ae87a91dad1141afdb34d28f2dd4a31c47171`；没有创建任务分支或工作树。用户已明确批准合并并清理，将上述四个文件与本记录整理为一个提交。原有未跟踪的 `knowledge-refit-20261117` 素材目录保持原样。

清理前核对 ZIP 与 `artifacts/ritsu-hit-fix-20261117-231839/` 的逐文件哈希一致，再删除该暂存目录；修复 ZIP、SHA256 文件和公共构建产物继续保留。最终提交与清理实况记录于忽略目录 `artifacts/task-deliverables/ritsu-hit-signature-fix-20261117/integration-result.json`。
