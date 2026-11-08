# 后台报错逐项分析

快照：2026-11-08T02:59:36.939515+00:00。生产 SQLite 使用只读连接，解码独立报错环境表；完整载荷位于忽略目录 `artifacts/error-reports/received-errors.json`。

共32组、39条报告、累计44次。消息或调用栈直接出现 Junna 的为8组、9条；其余来自安装 Junna 的环境。旧协议的 `mod_version` 表示 Karen 版本，Junna 版本以 `mods` 清单为准。

| 问题 | 组 / 报告 / 次数 | 指纹前缀 | 处理结论 |
| --- | --- | --- | --- |
| 箭矢类型显示与战斗卡死 | 4 / 4 / 8 | `ffb728cdaae9`, `bde4f78c74c0`, `5a96fbd4cdc2`, `d6ad01bdace2` | 修复 RitsuLib 出牌边界导致的初始化中断；同一 -652551411 类型的原生/转化箭矢 NCard 预览通过，真实战斗推进并结算25点伤害。 |
| 旧闪耀存档字段重名 | 2 / 2 / 2 | `4754a2fa7019`, `3a4b2c56b1ef` | 旧发行组合重复注册公共字段；当前 Core 1.0.1/1.0.2、Karen、Junna、RitsuLib 联用启动无此错误，保留旧字段迁移兼容。 |
| 旧 Karen 洗牌补丁失败 | 1 / 1 / 1 | `195bbbbd65ab` | 栈位于旧 Karen GlobalMoveSystem。当前发行组合启动正常，未重现重复替换；公共洗牌补丁由 Core 维护。 |
| 旧模型网络编号缺失 | 3 / 4 / 4 | `c3fd273557a5`, `d7aa4239eb7a`, `c9ed3ef9809a` | 现有加载桥及禁止内联的类型扫描覆盖后加载程序集。当前 beta 联用建表正常，stable 编译通过，本轮未运行 stable 游戏。 |
| 启动失败汇总提示 | 2 / 3 / 3 | `f89a10cb6a14`, `cda3a84fa12a` | 前置加载失败的汇总，随根因处理；当前联用启动通过。 |
| 知识获得补丁目标缺失 | 1 / 1 / 1 | `426e580e5bee` | 现有 AcquireGroup 按 STS2_BETA 选择5/6参数重载；两版编译通过，beta 实机目标解析非空。 |
| Godot Invalid Task ID | 3 / 6 / 7 | `20c17cbe15df`, `236c1e2066d0`, `44a11aedf685` | 已独立缩小到 Core 1.0.1 Common 资源预载，单独 Core 即出现3次；Core 1.0.2 单独及与 Junna 联用对照无错误。带强制绘制探针的完整组合仍有1次，保留为预载调查项。 |
| 游戏与模组版本不匹配 | 5 / 6 / 6 | `68ba2b7dcaf7`, `e58b30577e0c`, `2c9b19e2db41`, `ee9da1e6c90f`, `81d023e1d46e` | 版本拒绝符合当时清单。Junna 当前最低版本0.107.1并提供 Stable/Beta 实现；Rarity、Watcher 需匹配同一游戏分支。 |
| 旧查看卡牌输入补丁缺失 | 1 / 1 / 1 | `15137b3595fe` | 该补丁类型已移除；当前知识预览使用自身 Control._Input 处理滚轮，并使用对应版本确认按键。 |
| 开发探针依赖缺失 | 1 / 1 / 1 | `143654a1397b` | UpgradePreviewProbe 因前置 Junna 加载失败被拒绝；当前独立回归探针的 Junna 依赖已正常满足。 |
| 旧 RitsuLib 命中钩子匹配失败 | 3 / 4 / 4 | `097f54f6026b`, `389722959457`, `8d346a433a61` | 现有 AttackBatchPatch 置于 RitsuLib 命中钩子后并保留其桥；真实 DLL 联用 IL 回归通过，实机普通攻击6点正常。 |
| 旧续玩音乐控制器空引用 | 2 / 2 / 2 | `57ed126847bc`, `84aad93c6154` | 栈位于原生 NRunMusicController.UpdateTrack。当前真实存档续玩成功，未重现该异常。 |
| 调试跳房间事件索引越界 | 2 / 2 / 2 | `2d93948bfa2a`, `9ccbf363a0fd` | 来自 EventRoom.Exit/EventSynchronizer，经 EnterRoomDebug 和 MCP 控制台进入。正常地图操作、首战及续玩通过；报错载荷不含可还原原始事件状态的存档。 |
| 跨版本存档被拒绝 | 1 / 1 / 1 | `924d53cfab06` | 原生存档版本20高于运行引擎版本16；使用匹配的游戏版本或独立档案，不修改存档版本号。 |
| 旧 MCP 游戏类型缺失 | 1 / 1 / 1 | `b2fd28bc843b` | MCP 二进制引用的 StartRunLobbyPlayer 不存在于当时游戏程序集，属于工具与游戏版本不匹配；本轮 beta 工具链启动成功。 |

## 当前缺陷的反馈回路

```powershell
dotnet run --project tests/CompatibilityChecks -c Release -- <Junna-Beta-DLL> <RitsuLib-directory> <Core-API-DLL>
```

在原始 HEAD 产物上已实际运行：

```text
PASS: standalone card effect
PASS: standalone arrow destination
PASS: real RitsuLib replaces OnPlay
System.InvalidOperationException: Junna card boundaries: effects=0, results=1
```

RitsuLib 0.6.10 将 `CardModel.OnPlay` 替换为 `RunCardOnPlayHooks`。原补丁只识别原生调用，因而中断后续 Junna 补丁安装，箭矢类型本地化补丁也未完成。实机基线出现同一初始化错误。

修复保留 RitsuLib 分发器，在实际牌效委托边界接入箭矢；普通牌继续调用传入委托，保留前后监听和跳过牌效逻辑。修复后同一回路10项检查通过。箭矢类型保持 -652551411，未更换类型身份。

## 验证

- Beta、Stable、加载器和资源包构建成功，0警告、0错误。
- 10项真实 DLL 的原版/联用出牌及攻击边界检查通过。
- Core 1.0.1/1.0.2 与当前 Karen、Junna、RitsuLib 联用启动通过；`junna_compatibility check` 的7项真实卡面/目标解析检查通过。
- 真实存档续玩成功；满弦射击到期伤害25、普通打击伤害6，回合继续。
- 最小化启动采样未抢占前台；静默有声录像为 H.264/AAC、1280×720、约28秒，位于 `out/tmp/junna-error-reports-20261108/junna-compatibility.mp4`。
- 本轮仅运行受影响的编译、IL、启动、预览、续玩及首战验证。

## 资源预载对照

| 组合 | 结果 |
| --- | --- |
| BaseLib + MCP | 无 Invalid Task ID |
| BaseLib + MCP + RitsuLib | 主菜单、地图与首战无该错误 |
| BaseLib + MCP + Core 1.0.1 | Common 预载出现3次 |
| BaseLib + MCP + Core 1.0.1 + Junna | Common 预载出现3次 |
| BaseLib + MCP + Core 1.0.2 | 无该错误 |
| BaseLib + MCP + Core 1.0.2 + Junna | 无该错误 |
| Core 1.0.2、Karen、Junna、RitsuLib、强制绘制探针 | 预载出现1次，战斗回归通过 |

Core 1.0.1 满足当前接口要求，保留清单兼容范围；本次上报环境建议使用现有 Core 1.0.2 包。原生入口为 `WorkerThreadPool.wait_for_task_completion`。带绘制探针的完整组合仍有一次记录，本轮未将其宣称为已彻底修复，也没有过滤或吞掉该错误。

更新时使用同一发行包中的入口、Stable/Beta实现和资源，保持 BaseLib → Core → 角色的依赖顺序；不同游戏分支使用各自兼容的第三方模组和档案。服务端历史错误保留。
