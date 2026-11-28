# 逐牌效果回归

`card-pool-expectations.json` 固定工作台 r6521 的 95 张启用牌及基础、升级预期。预期与运行程序集内的 `Junna.CardPool` 分开保存。卡池版本或牌的编号集合不一致时，先复核设计变更。

`card-effect-cases-r6521.json` 为每张牌独立记录准备状态、操作、预期及关键对照。运行 `node tools/dev/full-card-audit.cjs plan` 会核对三个清单的版本和编号，并在 `docs/implementation/` 生成当前版本的逐牌效果测试条目。

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

- `effects`：39组最新牌效专项，包含独立版本预期、实际支付、各类牌区、三选一、重复强化、共享轮换与叠加抽牌及8种手牌类别组合。
- `full` 覆盖 95 张牌、基础和升级版各四种场景，共 760 组。逐项比较伤害、格挡、抽牌、能量、实际对象、牌堆、关键词、倒计时、复制和关联状态。
- `CurrentPoolTriggers.cs` 补充每回合首次复制、开局布置、单次降费、胜利复制、全牌区自动追随和动态减伤。
- `run-copy-reward.py` 通过真实耗尽、胜利、领取及选牌验证待领取Power、1/2张永久副本、预览、飞入、飞完与牌组计数通知；观察器只记录原生事件。

测试出牌通过 `TestGameApi.PaidPlay` 进入 Core 当前付费出牌接口，再走原生支付与 `CardCmd.AutoPlay`。持续效果中的直接钩子调用及回合号准备属于钩子级回归。真实回合另由 `tests/StageNotesTurns/run-native-turns.py` 使用结束回合、敌方行动与原生选牌验证。

## 结果与录像

- 逐牌结果保存在 `out/tmp/full-card-tests/results/<运行编号>/`，运行编号和卡池版本同时写入逐场景结果。每张牌的版本与变体分别保存，异常计为失败。汇总读取 `latest-run.json` 指定的一次运行，校验完成记录、场景和非空断言。使用多个牌号可在一次运行中验证指定卡牌。
- 最新专项：`artifacts/latest-card-pool/results/latest-effects.json`。
- 批量录像使用 `node tools/dev/full-card-audit.cjs record-primary <port> [牌号...]`。每次准备结束之后开始录制，使用悬停、出牌、选牌、结束回合和领取奖励。以卡牌 ID 续录未完成项，保留游戏内音轨。
- `node tools/dev/full-card-audit.cjs record-upgrade <port> <牌号...>` 录制指定卡的升级版，另存于 `videos-upgrade/`。相同卡的基础版和升级版可同时进入索引。
- `node tools/dev/full-card-audit.cjs record-opening <port>` 从实际地图进入战斗，录像验证绝美的凋零的自动布置和自然完成。
- `node tools/dev/full-card-audit.cjs report` 生成可按卡名或 ID 搜索的录像与结果索引。可在测试游戏和启动脚本中设置相同的 `JUNNA_CARD_AUDIT_OUTPUT`，集中保存本轮结果。

测试失败时先核对固定牌效、准备对象与实际事件，保留失败文件，再修复实现或具体预期。禁止通过跳过卡牌、删除失败断言或把当前输出直接复制为预期来消除失败。

## 工作台 r5324 定向检查

在独立双敌人战斗中执行 `junna_loop_sync check`，验证五张新循环牌、备箭、齐射及宝石数值；结果写入 `artifacts/task-deliverables/sync-workbench-latest-20261116/targeted-results.json`。`junna_loop_sync prepare` 准备五张循环牌和弦声齐鸣，供正常出牌录像使用。

## 工作台 r5406 定向检查

在隔离战斗中执行 `junna_sync_5383 check`，检查本轮32个基础／升级场景；结果写入 `out/tmp/sync-workbench-20261117/targeted-results.json`。`junna_sync_5383 prepare` 准备9张正常出牌演示牌。详见 `docs/worktree-tasks/sync-workbench-20261117/README.md`。

## 自动打牌目标定向检查

双敌人环境运行 `python tests/Integration/run-auto-target.py --port <port>`，检查伏击基础及升级版、共用免费打牌、论据与知识关联打牌、保留原目标及自身目标。探针在旧手动选敌人入口直接报错，避免选牌测试替身掩盖问题。加 `--record` 准备伏击后，通过正常出牌与结束回合录制；录像和结果保存于 `out/tmp/auto-target/`。

## 克隆附魔关联知识牌定向检查

`junna_cloning_knowledge run` 准备固定牌组并调用原生休息处克隆，输出 `out/tmp/cloning-linked-knowledge-20261117/results.json`。11 项断言覆盖知识主牌、关联成员、后天关联与跨角色牌、数量、升级与闪耀继承、原牌顺序及单张复制控制。

实机录像先取得佩尔的增生组织，运行 `junna_cloning_knowledge prepare`，进入休息处后只点击原生“克隆”；停止录像后运行 `junna_cloning_knowledge check` 核对结果。

## 工作台 r5462 定向检查

在隔离战斗中执行 `junna_sync_5462 check`，验证10张数值调整牌的基础与升级版，共20个场景；结果写入 `out/tmp/sync-workbench-20261118/targeted-results.json`。`junna_sync_5462 prepare` 准备正常出牌演示。同步内容见 `docs/implementation/workbench-sync-20261118.md`。
