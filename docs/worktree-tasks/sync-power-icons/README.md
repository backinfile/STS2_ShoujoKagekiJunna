# Sync Power icons and energy HUD

## Original user input

- `$worktree-task` 去掉工作台中power中 属于游戏本体的power，然后将这些power图标 标题修改到mod中
- 当有下回合能量或者保留能量的power的时候，要求在战斗界面的能量图标处添加比较明显的标记。下回合2能量显示为+2。保留2能量显示为(旋转箭头icon)2 现在在能量图标右侧，你可以先用imagegen设计下
- 允许更换位置，或者更换样式。你来用imagegen设计多种我看看
- B，只有一种存在的时候就居中显示
- 不要这个背景了，你重新设计一个朴素的背景（底板）
- 现在这个过于显眼了，你继续设计设计
- 使用2

## Brief task plan

- 原分支 `main`，起始提交 `6b770e210cc6830b1c98fcfbc36396f78adc85ec`。
- 工作树：`../worktrees/STS2_ShoujoKagekiJunna/sync-power-icons`；任务分支 `codex/sync-power-icons`。
- 保留原目录已有未提交改动；实现与验证放在任务工作树中。
- 通过工作树的工作台 MCP 逐条读取版本，移除本体 Power；同步纯那 Power 的现有图标并核对标题。
- 复用已有完成清理的图片，进行当前改动所需的最小验证，合并与清理分别等待用户确认。
- 能量标记采用最新选定的 2「无底板」：球下显示小号暖灰 `+N` 与蓝灰循环箭头加 `N`，双项左右排列、单项居中、无项隐藏。接入当前玩家的真实 Power 状态并录制最小实机验证。

## Result and changes

已通过 `6751b34` 合入 `main`；2026-11-05 完成剩余内容整理与旧工作树归档清理。

- 工作台通过 MCP 原子提交移除了下回合能量、易伤、虚弱 3 个本体条目，保留 28 个纯那 Power。原有卡牌、规则、图像资产及其余 Power 内容均保持一致。
- `tools/assets/register-power-workbench.cjs` 仅注册纯那 Power，并迁移删除旧本体条目，避免重新生成它们。
- `ShoujoKagekiJunna/images/powers/` 新增 28 张图标及对应 Godot 导入配置。26 个通用 Power 按设计 ID 取图，耐力训练与能量保留分别使用独立图标。
- 已逐一核对 `zhs` 和 `eng` 的 28 个标题；现有标题均与工作台一致，无需重写本地化文件。
- 图标逐字节复用 `docs/design/assets/powers/2026-11-05/native-v3/icons/`。原图及清理记录保留在同级 `originals/`、`cleanup.json`：GPT Image 2 Artifact Cleaner，strength 0.25、interior_blend 0.25，256×256，透明轮廓保持一致。接入的 Power 图标未重新生成或处理。
- 最小验证通过：工作台语义差异仅为 3 个本体条目删除；28 张图标与清理记录哈希一致；两种语言标题逐项一致；`build.ps1` 成功，0 警告、0 错误；成品 PCK 中全部 28 张纹理可加载且尺寸正确；浏览器显示 28 个条目、全部图标加载、0 页面脚本错误；`git diff --check` 通过。
- `src/UI/EnergyStatusBar.cs` 落实最终选定的 2「无底板」：能量球下方显示小号暖灰 `+N` 与蓝灰循环箭头加 `N`。移除底板、边框和分隔线；透明布局范围 128×30，沿用能量数字字体，默认字号 22，较长数值自动缩小，描边减至 2。循环箭头由 Godot 绘制并按 22×22 缩放，保留明确箭头尖端。
- 设计预览使用内置 imagegen 生成，保留原图，并通过本地 Artifact Cleaner 以 strength 0.25、原尺寸处理；提示词和清理记录位于视觉预览工作区的 `energy-hud-quiet/`。最终界面使用原生控件与绘制图形。
- 显示当前能量计所属玩家的 `EnergyNextTurnPower` 与 `EnergyReserve` 层数。单项整组居中、双项左右分列、无项时整体隐藏，数值实时更新，点击穿透；随原生能量计一起出现和退场。
- 工作台预览与录像：`artifacts/power-workbench/workbench-powers.png`、`artifacts/power-workbench/workbench-powers.mp4`。构建产物：`dist/ShoujoKagekiJunna/`。这些交付文件未纳入 Git。
- 最终样式最小实机验证通过：无状态、仅下回合能量、仅保留能量、两种同时存在、双位数更新、清空状态，共 6 组；核对实际文字、可见性、布局中心及鼠标穿透。单项中心为 64，双项中心为 32 和 96，6 组全部通过。
- 在满解锁隔离存档中最小化、静默启动，0 次抢占前台；录像使用引擎内部音轨。实际结束回合后两种 Power 均结算移除，下一回合能量为 7，标记自动隐藏。录像已完成封装，测试游戏已停止。最终样式证据位于 `out/tmp/energy-status-quiet/`，18 秒预览视频 `energy-status-preview.mp4`，单项与双项截图 `quiet-next-only.png`、`quiet-retained-only.png`、`quiet-both.png`，验证记录 `results.json`、`showcase-states.json`。未执行全面回归测试。
- 文档审查覆盖已跟踪文档与任务文档，未检出凭据；本次新增文档使用仓库相对路径。历史来源记录保留原样。
- 原目录的 13 项已跟踪修改、4 项未跟踪文件均已校验内容哈希并保持原样。`CatalogPower.cs` 与 `EnergyReserve.cs` 含有原目录未提交逻辑改动，合并时需暂存后恢复；原改动应用到任务结果的预检已通过。
- 任务进行期间，原目录另有能量球分层素材及 `junna_energy.tscn`、`Junna.cs` 等新改动，已单独记录指纹并保留；包含这些改动的恢复预检通过。能量标记按原生能量数字的实时位置锚定。
- 任务分支已清理，当前仅保留主工作树。旧目录中 134 份独有录屏及验证文件已按原相对路径保存至主目录并校验哈希；差异旧版本与测试配置保存在 `artifacts/main-cleanup-2026-11-05/sync-power-icons-preserved.zip`。遗留目录整体归档至同级 `sync-power-icons-leftovers/`，保留原文件和游戏数据链接，原工作树位置已清空。
