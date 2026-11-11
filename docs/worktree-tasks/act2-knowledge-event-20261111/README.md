# 二幕专属知识事件

## 用户输入

完整任务记录见 [user-input.md](user-input.md)。

## 起点

- 原分支为 `main`，起始提交为 `29cad37e4805d6f5eafdadd6495da2140bf07ed3`。
- 任务分支为 `codex/act2-knowledge-event-20261111`。
- 工作树位置为 `../worktrees/STS2_ShoujoKagekiJunna/act2-knowledge-event-20261111`。
- 原目录的7项已修改文件和8项未跟踪文件记录在 [original-worktree-state.json](original-worktree-state.json)，其中14项内容保持一致；DynamicKnowledge由并行提交更新。当前主目录的4项未提交修改已另存 [同步保护记录](sync-original-worktree-state.json)。

## 实现计划与结果

1. 通过工作台读取事件、九张牌及其直接关联列表，核对原版事件图片规格、导入参数与默认布局，整理 [配图规格](event-art.md)。
2. 实现 `JUNNA_STAGE_GIRL_NOTES`，仅在第二幕纯那队伍中出现，每局一次。选项为“研读手册：获得这组9张知识牌”“反复推敲：升级1张知识牌”和“合上手册：离开”。
3. 入口取得翡翠弓后一次附带其余八张，沿用原生永久牌组修正与直接关联发放。已有同名实际牌保留，各张分别占位、升级。
4. 反复推敲仅列出尚可升级的知识牌，选择恰好1张。普通牌和已升级牌不列入候选；没有候选时禁用选项。
5. 事件完成后记录玩家结果，移出本幕事件池，并使用原生完成房间存档入口。重启后恢复同一事件结果页，奖励数量保持一致。
6. 九张知识牌均为0费、1回合事件箭矢，各自指向其余八张，沿用工作台当前牌面，并从普通奖励、商店和战斗随机生成中排除。定向抽取包含同名实际牌，从当前抽牌堆逐张抽取。
7. 卡图采用工作台选定的同系列官方ReLIVE卡面。事件插图参考KINEMA CITRUS公开的纯那书桌设定，使用内置imagegen重绘为全屏书桌场景。Artifact Cleaner 0.25清理后输出3440×1616，使用原版事件默认布局。原图、最终提示词、来源和参数保存在 [source.json](source.json)、[official-art-source.json](official-art-source.json) 与 [desk-art-cleanup.json](desk-art-cleanup.json)。
8. 工作台已同步最终选项文案、知识筛选和重绘配图，已完成合并。

## 验证

- Beta、Stable与资源打包通过，0警告、0错误；独立定向测试项目通过编译。
- 原有九张牌基础与升级共18个实际布置、结算场景保持通过，覆盖伤害、群伤、多段、格挡、加速、能量、伤害加成、普通抽牌与关联抽牌。本次仅重验修改后的事件选项与配图。
- 零张可升级知识牌时禁用；只有一张时自动升级该知识；两张知识候选中选择一张，普通牌与已升级知识均保持原状。
- 原有同名升级牌保留，新增九张各一份。原生完成事件存档重启后恢复原结果，奖励不重复。
- 与主目录未提交的知识取得修改进行独立兼容编译，通过。
- 定向检查合计153项通过；明细见 [verification.json](verification.json)。综合测试项目存在3处既有调用签名编译错误，本次采用独立的 `tests/StageGirlNotes/JunnaStageGirlNotesChecks.csproj` 执行定向检查。
- 提交前审计所有已跟踪文本及新增任务文档的本机路径和常见密钥格式，文档采用仓库相对路径和描述性占位符。

## 同步

任务修改已归并为提交 `0820511`。随后无冲突同步主分支 `4addaed`，Beta、Stable、资源包和独立测试项目重新构建通过；针对整组取得和单张知识升级的16项检查通过。主目录当前4项未提交修改逐项SHA-256一致。

## 交付物

- 游戏源码及资源随任务提交保存。
- `artifacts/task-deliverables/act2-knowledge-event-20261111/ShoujoKagekiJunna-event.zip` 包含构建后的Junna Mod，依赖项目现行Core与BaseLib版本。
- 同目录 `knowledge-event.mp4` 为整组知识取得演示，`upgrade-knowledge.mp4` 为知识升级选择演示，`event.png` 为最终事件界面。测试使用独立满解锁存档、随机端口、静音内部混音录制。
- 用户已明确批准合并并清理。已将同步结果 `e8a153f` 快进合并到 `main`，提交树一致，原有4项未提交修改的SHA-256保持一致。
- 5份交付文件及摘要清单已保存到主目录同名交付目录，逐份SHA-256一致。
- 清理范围为本任务工作树、任务分支及其中临时构建、测试文件；完成回执保存在交付目录的 `completion.json`。
