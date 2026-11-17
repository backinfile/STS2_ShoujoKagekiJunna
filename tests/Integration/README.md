# 逐牌效果回归

`card-pool-expectations.json` 固定工作台 r5294 的112张启用牌及基础、升级预期。预期与运行程序集内的 `Junna.CardPool` 分开保存；卡池版本或牌名集合不一致时停止执行，先复核设计变更。

## 构建与运行

先构建 Junna，再编译本测试模组：

```powershell
dotnet build tests/Integration/JunnaIntegration.csproj -c Release -p:CoreApiPath=<Core-API-DLL>
```

将测试 DLL 和 `JunnaIntegration.json` 放入独立游戏的 mods 目录后启动。游戏使用随机端口、满解锁、最小化及 `silent_test=true`；Core 与 BaseLib 版本沿项目构建包。进入战斗后执行：

```powershell
python tests/Integration/run-card-pool.py --port <port> --scope effects
python tests/Integration/run-card-pool.py --port <port> --scope full
python tests/Integration/run-card-pool.py --port <port> --scope full --id JK78
python tests/Integration/run-copy-reward.py --port <port> --record --output <task-output>
python tests/Integration/run-copy-reward.py --port <port> --upgrade --record --output <task-output>
```

- `effects`：39组最新牌效专项，包含独立版本预期、实际支付、各类牌区、三选一、重复强化、独立轮换及8种手牌类别组合。
- `full`：112张牌×基础/升级×正常/空资源/强化组合/满手牌，共896组。逐项比较伤害、格挡、抽牌、能量、实际对象、牌堆、关键词、倒计时、复制和关联状态。
- `CurrentPoolTriggers.cs` 补充每回合首次复制、开局布置、单次降费、胜利复制、全牌区自动追随和动态减伤。
- `run-copy-reward.py` 通过真实耗尽、胜利、领取及选牌验证待领取Power、1/2张永久副本、预览、飞入、飞完与牌组计数通知；观察器只记录原生事件。

测试出牌通过 `TestGameApi.PaidPlay` 进入 Core 当前付费出牌接口，再走原生支付与 `CardCmd.AutoPlay`。持续效果中的直接钩子调用及回合号准备属于钩子级回归。真实回合另由 `tests/StageNotesTurns/run-native-turns.py` 使用结束回合、敌方行动与原生选牌验证。

## 结果与录像

- 逐牌结果：`out/tmp/full-card-tests/results/`，每张牌每个版本和变体独立保存；异常作为失败记录。
- 最新专项：`artifacts/latest-card-pool/results/latest-effects.json`。
- 录像：`record-card-pool.py --port <port> --output <task-output>`，准备完成后只使用悬停、出牌、选牌与结束回合；记录器保留游戏内音轨。
- 当前逐牌观测表见 `docs/implementation/card-tests-r5294.md`。

测试失败时先核对固定牌效、准备对象与实际事件，保留失败文件，再修复实现或具体预期。禁止通过跳过卡牌、删除失败断言或把当前输出直接复制为预期来消除失败。

## 工作台 r5324 定向检查

在独立双敌人战斗中执行 `junna_loop_sync check`，验证五张新循环牌、备箭、齐射及宝石数值；结果写入 `artifacts/task-deliverables/sync-workbench-latest-20261116/targeted-results.json`。`junna_loop_sync prepare` 准备五张循环牌和弦声齐鸣，供正常出牌录像使用。

## 工作台 r5406 定向检查

在隔离战斗中执行 `junna_sync_5383 check`，检查本轮32个基础／升级场景；结果写入 `out/tmp/sync-workbench-20261117/targeted-results.json`。`junna_sync_5383 prepare` 准备9张正常出牌演示牌。详见 `docs/worktree-tasks/sync-workbench-20261117/README.md`。

## 自动打牌目标定向检查

双敌人环境运行 `python tests/Integration/run-auto-target.py --port <port>`，检查伏击基础及升级版、共用免费打牌、论据与知识关联打牌、保留原目标及自身目标。探针在旧手动选敌人入口直接报错，避免选牌测试替身掩盖问题。加 `--record` 准备伏击后，通过正常出牌与结束回合录制；录像和结果保存于 `out/tmp/auto-target/`。
