# 箭矢特效性能审查

## Original user input

### 用户工作区约定

```text
# AGENTS.md instructions for <repository-root>

<INSTRUCTIONS>
# Global workspace conventions

- Create Git worktrees under `../worktrees/<projectName>/<task-slug>` relative to the repository root. Do not place worktrees directly alongside the repository in its parent directory.

<!-- set-global-rules:start -->
## 用户协作约定

### 表达偏好

请在所有回答及你撰写的内容中遵循以下表达偏好：

1. 直接说明结论、事实、理由和必要步骤，语气自然、明确。详细程度按任务需要展开，保留有价值的解释、推导、依据、例子和细节。精简的对象是赘述。

2. 对准备添加的反面解释、范围声明、保留说明和风险提醒，先判断：删去这句话，是否会实质改变我对答案、结果状态或下一步行动的理解？如果不会，且它主要用于预防假想误解、提前免责或凑齐注意事项，就直接删除。不要换一种委婉说法保留同样的冗余信息。

3. 优先正面陈述“是什么、能做什么、已经做了什么、为何如此”。避免无端追加“这只是……不代表……”“这并不意味着……”，避免主动罗列没做什么、未涉及什么和不保证什么。“不是……而是……”仅在对比或纠错确有帮助时使用，避免成为惯用句式。

4. 让结论的强度与证据一致。将必要的对象、条件、数据范围和实际状态直接写进结论。证据充分时明确判断；确有不确定性时，指出具体不确定之处及其影响。避免反复堆叠“可能、一定程度上、相对而言”等弱化词。

5. 直接回答问题、解释机制、纠正错误所需的否定句正常使用。对于我明确要求说明的事项，以及省略后会造成实质误解的重要限制、失败或未完成事项，应准确交代。根据重要性放在相关结论旁或简短的状态说明中，优先用一句话说清具体事实及影响，下一步按需补充。避免孤立地写“未验证”“不保证”“仅供参考”，也不要机械增加“风险提示”“局限性”等段落。

6. 撰写文档、介绍、邮件等成稿时，按目标读者的需求组织正文。必要的制作过程说明放在交付说明中；成稿聚焦读者需要的内容，避免混入对自身工作范围的解释、假想争议和例行免责声明。

7. 输出前自行检查并清理上述赘述，直接交付整理后的答案。无需复述这些偏好，也无需宣布你已遵守。

### 修改、设计与测试

- 进行修改任务时使用最小改动。
- 进行设计类任务时，文本内容面向使用者生成。明确区分“实现要求”和“面向用户的文本”，实现要求只用于指导实现，不写入面向用户的文本；用户侧内容保持简洁、易理解。少用标语和 slogan。界面文案只保留名称、数量、状态和必要的按钮文字。操作提示、辅助说明、副标题、帮助文案及设计解释，仅在我明确要求时添加。功能通过布局、图标和交互体现。交付前删除所有自行添加的多余说明。
- 进行任务时，只自动执行验证当前改动所必需的最小测试；完整、全面的测试等全部工作完成后，再向用户说明测试范围并请求同意，取得同意后执行。
<!-- set-global-rules:end -->

--- project-doc ---
# 项目概况

- 《杀戮尖塔 2》星见纯那角色 Mod，ID：`ShoujoKagekiJunna`；核心机制为箭矢、知识与伤害加成。
- 技术栈：C# / .NET 9、Godot 4.5.1（MegaDot）、BaseLib 3.4.7
- `src/Combat/` 按箭矢、卡牌、伤害、知识分组，`src/UI/` 存放 HUD、卡面与动画，`src/Models/` 定义游戏模型。目录调整沿用已有命名空间及 Godot UID，保持类型引用兼容。
- `data/card-pool.json` 为编译时嵌入的运行卡池，`ShoujoKagekiJunna/` 为游戏资源，`docs/design/` 为工作台，`docs/implementation/` 为实现记录，`tests/Integration/` 为独立测试模组。公共构建配置在 `Directory.Build.props`。
- 构建：`./build.ps1 -CorePackage <Core发行目录>`；产物为 `dist/ShoujoKagekiJunna/`，运行时依赖独立 Core 与 BaseLib 工坊条目。`tools/prepare-workshop.ps1 -CoreWorkshopId <Core工坊ID> -PreviewImage <预览PNG> -OutputDirectory <新目录>` 生成仅含 Junna 的工坊工作区及必需物品配置。

# 工作台编辑

工作台内容统一使用项目 MCP `junna_workbench` 读取和修改。具体流程见 [docs/design/AGENTS.md](docs/design/AGENTS.md)。

- 主台部署在 `vipcloud`，由 `junna-workbench.service` 运行。程序入口为 `<workbench-service-directory>`，主台数据为 `<workbench-state-database>`，内部连接配置为 `<workbench-private-backend-config>`。图片保存在 `<workbench-assets-directory>/`，通过 HTTP 哈希地址读取；历史和请求回执与条目一起由 SQLite 事务持久化。`state.json` 为迁移前备份。
- 公开只读页为 `https://junna.17181718.xyz/`，编辑页与 MCP 使用该域名下的 HTTPS 长路径，未设置 IP 白名单。服务器网关与 MCP 分别监听 `127.0.0.1:54828`、`127.0.0.1:54829`，HTTPS 路由位于 `<workbench-nginx-config>`。
- 当前长路径及密钥读取主目录 `docs/design/workbench/local-save.config.json` 的 `remoteURL`、`remoteToken`。编辑页地址为 `remoteURL + '/'`，MCP 接口为 `remoteURL + '/mcp'`。私有连接配置保存在 Git 忽略范围内。其他工作树复用主目录客户端及其连接配置。
- 当前会话尚未加载新工具时，使用 `node docs/design/workbench/mcp-client.cjs <工具名> <参数JSON文件>` 连接同一个 MCP 服务。入口轮换后，已启动的 STDIO 客户端需要重新连接，命令行客户端会读取最新配置。源码修改使用普通文件工具，工作台内容由云端 MCP 持久化。部署流程见 [docs/design/workbench/deploy/README.md](docs/design/workbench/deploy/README.md)。
- 网页首次加载 `/snapshot.json`，之后通过 MCP 按 revision 同步增量。公开快照由独立发布进程更新；完整图片与历史在导出时读取。迁移后继续复用原有条目版本、assetId 和 requestId。

# 契合设计

以下精选自工作台的长期约定。设计前通过 MCP 读取 `styles` 中对应规则、已确认用词及相关卡牌的当前内容。

- 以当前牌效选择卡名、契合和图片，核对名称、类型、效果、体系主题、图片、组内关系与来源；契合调整采用最小改动，保留当前机制、数值及用户正在调整的牌面。
- 箭矢可用物理箭、言叶或星：分别表现瞄准与命中、话语的传递与抵达、追寻与飞行轨迹。按实际子类型选图，攻击箭矢体现主动攻击，准备与辅助箭矢体现准备、等待及完成后的收益。
- 伤害加成的通用契合是纯那的日常锻炼，围绕体能、基本功、射术、反复练习与训练成果；带伤害的训练牌同时体现主动出手。
- 知识契合由学习行为或有明确内容与来源的知识承担。典籍先确认纯那在原作或角色访谈中阅读、引用、演绎的直接联系，再选择内容；人物档案围绕对应人物的身份、经历与专长。同组主牌与关联牌围绕同一知识关系，可呈现释义、例证、推导、应用与成果。
- 能量的通用契合使用星星，以点亮、汇聚、留存和延续对应收益；箭矢中的星突出轨迹与抵达。有直接原作或知识来源的牌可沿用原名，以具体内容衔接收益。跨体系牌以主体系契合为主。
- 卡名优先选对应场景中已核对的连续原词，保持原意；缺少合适原词时选自然、完整的常用概念、动作或物件名。普通卡名通常7字以内，保留原句优先于机械限字；核对常见谐音及用户已否定的名称。原文、译名、概括名和设计解释分别记录。不要使用自造词。
- 卡名、契合与卡图共同体现牌的用途：攻击突出出手、弹道、命中或进攻性的论辩与言叶运用；防御突出抵挡、保护与应对；准备和积累采用相应动作与物件。配图优先用原作画面，在实际卡框与箭矢遮罩中检查脸、手、武器和动作方向，清理字幕、水印及录制界面干扰。不同牌采用有区分的构图，相邻帧或同一动作的换裁切也纳入重复检查。
- 改名、换图或重选契合后，同步基础与升级文本的牌名引用、关联牌、知识组标题、契合池、出处及提示，并复查相关卡面、引用跳转、重名和重复图。复用备用或退役素材时，按新牌当前效果更新解释与关联，保留素材出处。

# 图片素材处理

所有生成的图片在交付前都必须使用本地去 AI 摩尔纹工具处理一遍，去除摩尔纹与生成伪影。优先复用项目已安装的 Artifact Cleaner，按原尺寸对比处理前后，检查眼镜、发丝、弓弦和服装边缘；保留生成原图，并记录本地处理工具、参数与成品路径。

# STS2 MCP 连接与使用
使用 MCP 操作 STS2 时，需要读取其mcp readme文档。默认使用最小化+静音来启动游戏进行测试, 需要选择一个随机端口号避免和其他任务冲突。

不测试教程，解锁等内容时，直接以满解锁（unlock all）状态启动游戏即可。

使用mcp进行任务时，需要录制视频给用户看效果， 录制前准备好环境，录制中尽量不要调用cmd等其他直接修改的操作了，应使用游戏合法操作继续进行。

</INSTRUCTIONS>
```

### 审查请求

```text
[$worktree-task]($worktree-task)  你来review下箭矢特效相关，看看是否有性能影响，是否会造成卡顿
```

### 用户提供的技能

<skill>
<name>worktree-task</name>
<path>$worktree-task</path>
---
name: worktree-task
description: Use when completing a repository change in an isolated Git worktree, consolidating its task changes into one commit, and merging it back to the starting branch.
---

# Worktree Task

Complete the requested change in a temporary worktree, then integrate it safely into the branch that was current when the task began.

## Rules

- Capture the original checkout, branch, and revision once; never re-infer the integration target.
- Refuse an active Git operation. If the original checkout is dirty, warn that its uncommitted changes will not enter the worktree, preserve them untouched, and proceed from the current branch's latest commit by default.
- Place the worktree at `../worktrees/<projectName>/<task-slug>` relative to the repository root, unless repository instructions override it.
- Perform all modification and verification work inside the worktree.
- Keep a task document under `docs/worktree-tasks/<task-slug>/` inside the worktree and include it in the task commit, so it survives integration and worktree cleanup.
- Sanitize every document tracked by Git or being added to Git, including existing unchanged documents. Replace sensitive data such as private personal details and credentials with descriptive placeholders while preserving meaning.
- Use repository-relative paths in persisted documents and express worktree locations relative to the repository root. Never persist local absolute paths, including in quoted user input or Markdown links. Use descriptive placeholders for paths outside the repository and `$skill-name` for local skill references. Tool calls and safety checks may use absolute paths internally.
- Do not perform remote operations or rewrite existing original-branch history unless explicitly requested.
- For merge and cleanup confirmation, use `request_user_input_async` when available and permitted; otherwise ask in chat. One explicit confirmation may approve both merging and the listed cleanup; the original task request alone is not approval.
- Preserve the task branch and worktree whenever a safety check or integration fails.

## Task Document

Use `docs/worktree-tasks/<task-slug>/README.md` by default, with the same task slug as the worktree. Create it before implementation. Keep the input record complete and the plan and results concise:

- **Original user input:** Record the full text of every task-related user message in chronological order, including introductory guidance, skill invocations, constraints, and follow-up corrections. Preserve the original wording and formatting, applying only the sanitization above. Append later messages in full.
- **Brief task plan:** Record the intended approach and main steps; update it when the approach changes.
- **Result and changes:** After implementation, summarize the actual completion status, changed files and behavior, verification results, and any remaining work or limitations. If work stops early, record partial progress accurately.

Before each commit, check the input record against the user messages, finalize the result section, and review all Git-tracked documents and documents being added for sensitive data and local absolute paths, including quoted input and link targets. If synchronization changes the implementation or verification results, update the document in the synchronization commit. Preserve the document with the task changes and include its path in the final report.

## Workflow

1. Check the repository state and refuse conflicting worktree paths or branches. Record any original uncommitted state so it can be protected and verified later.
2. Create a uniquely named task branch and worktree from the captured original branch's latest commit. Create the task document with the sanitized user input and brief plan.
3. Establish a clean baseline, implement the task, and verify it. Complete the task document's result section, then consolidate all task changes, including the document, into one commit before synchronization unless the user or repository policy requires multiple commits.
4. Merge the latest local original branch into the task branch. Resolve conflicts only in the worktree, then verify and commit the synchronized result.
5. Confirm the original checkout is still on the captured branch, its committed HEAD is unchanged since synchronization, and any pre-existing uncommitted state remains intact. If HEAD advanced, repeat synchronization; stop after three races.
6. Show the target branch, task commit, change summary, verification results, any original uncommitted state, and proposed cleanup items. Request one confirmation with choices to merge and clean up, merge and keep, or defer.
7. Only after confirmation, merge the synchronized task branch back without creating an additional integration commit when possible. If integration would overwrite uncommitted state, preserve the worktree and report that integration is pending instead.
8. Verify the committed trees match, checks pass, the original branch contains the consolidated task commit, and pre-existing uncommitted state was preserved.
9. After successful integration verification, remove this run's clean worktree, task branch, and disposable temporary files covered by the cleanup approval, after proving needed content is preserved. Reuse combined approval from step 6 without asking again. If only merging was approved, retain these items until the user approves cleanup; request further confirmation for any additional cleanup items.

Report the resulting branch, commit, verification, cleanup state, and task document path.

</skill>

### 后续用户指示

```text
你来实机验证并修复
```

```text
可以用重构的方式来进行修复，务必做到效率最好
```

### 用户合并与清理确认

```text
<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_0af064e2cb32490b94423d424a8346b9\",0]","question":"修复已提交为 9671bcc，Beta/Stable 构建和定向实机回归已通过，原 main 工作区仍干净。按 worktree-task 技能要求，合并和清理前需要确认。是否合入 main，并清理本任务分支、工作树及临时测试环境？验证结果和录像归档会保留。","answer":"合入 main 并清理本任务工作树和分支"}]
</send_user_message_question_reply>
```

## Brief task plan

1. 固定 main 起点和干净工作区状态，在 `../worktrees/STS2_ShoujoKagekiJunna/arrow-vfx-perf-review-20261110` 审查。
2. 追踪箭矢部署、待机、悬停、释放、回收及共用卡面特效，确认热路径、资源生命周期和规模增长。
3. 使用最小必要验证支持具体发现，记录代码位置与触发条件。
4. 在独立、最小化、静默游戏实例测量修复前后的 HUD CPU、托管分配、帧间隔和节点回收。
5. 根据实测重构绘制缓冲、目标快照及装饰档位处理，复测部署、悬停和释放。
6. 录制定向验证视频，提交结果后等待合并及清理确认。

## Result and changes

已完成实机复现、重构和定向复测。审查基线为 `main` 的 `03c2a3b3e8b3df6b84508aa61801c894da44054d`。开始时原工作区干净。

### 修复

- `src/UI/Arrows/ArrowHud.Guides.cs` 将渐变扇面、群体线和单体线制作为三个共享几何模型，通过持久化 `MultiMesh` 缓冲批量提交。线宽使用独立的法向基，目标距离改变时保持线宽，边缘使用透明渐变。缓冲按容量增长，稳定帧复用。
- `src/UI/Arrows/ArrowHud.Snapshot.cs` 每帧统一采集角色位置与目标顺序，复用列表、字典及移除缓冲。同一所有者的群体目标查询共享结果，布局、朝向和绘制使用同一帧目标快照。
- `ArrowHud.cs`、`ArrowHud.Layout.cs` 和 `ArrowHud.Hover.cs` 提前命中稳定布局缓存，复用显示与悬停顺序，仅在数值变化时更新倒计时颜色和重复次数文本。
- 装饰绘制独立使用 Core 的特效预算。Reduced 档位按 50 ms 预算刷新，卫星与待机摆动仅在 Full 开启。Off 隐藏装饰层，保留箭矢主体和计数。
- `ArrowHud.Feedback.cs` 为 Off 释放分支补上节点回收，保留牌堆退出位置。正常开启特效时继续由释放动画父节点负责回收。
- `tests/Integration/ArrowPerformanceProbe.cs` 提供真实引擎性能、节点回收、目标快照、实例矩阵及档位切换检查。

### 实机结果

隔离游戏采用 1280 × 720、Compatibility 渲染、Full 特效、Core 1.4.0 和 BaseLib 3.4.7。使用 `SLIMES_NORMAL` 的四目标场景，每组预热 90 帧，采样 360 帧。游戏最小化且静默，探针强制完成真实渲染。性能采样与视频录制分开进行。

50 支群体箭矢、4 个目标的结果如下。修复后采用最终构建再次测量的数据。

| 指标 | 修复前 | 修复后 |
| --- | --- | --- |
| 平均帧间隔 | 30.077 ms | 16.669 ms |
| 平均帧率，按帧间隔换算 | 33.25 FPS | 59.99 FPS |
| 帧间隔 P95 | 32.267 ms | 17.324 ms |
| 最大帧间隔 | 40.468 ms | 35.096 ms |
| HUD 更新平均耗时 | 0.722 ms | 0.249 ms |
| 装饰绘制平均耗时 | 8.881 ms | 0.077 ms |
| 装饰绘制 P95 | 9.836 ms | 0.090 ms |
| HUD 更新托管分配 | 178,685 B/帧 | 8,528 B/帧 |
| 装饰绘制托管分配 | 700,400 B/帧 | 0 B/帧 |
| 整个视口平均渲染 draw call | 5,085 | 890 |

HUD 更新和装饰绘制合计平均 CPU 耗时由约 9.60 ms 降至 0.33 ms，装饰绘制平均耗时下降约 99.1%。最终样本中 HUD 更新最大 0.478 ms，装饰绘制最大 0.180 ms。

20 支群体箭矢、4 个目标的装饰绘制平均耗时由 3.511 ms 降至 0.032 ms，绘制分配由 280,160 B/帧降至 0 B/帧。

50 支箭矢的档位检查中，Full 在 360 个采样帧绘制 360 次，Reduced 绘制 100 次，Off 绘制 0 次。主体与计数在 Off 下仍可见，档位恢复后装饰重新显示。

### 定向回归

- 修复前使用 `junna_arrow_perf prepare 20 Off`、`junna_arrow_perf release` 复现失败，部署列表清空后仍有 20 个箭矢根节点。修复后 50 支箭矢全部释放，剩余根节点和部署数均为 0。
- 混合 12 支群体与单体箭矢，以及删除其中一支后的两次检查全部通过，覆盖显示顺序、目标点一致性、实例矩阵、Off 主体保留与装饰恢复。
- 现有 `junna_arrow_pivot check` 的 11 项断言通过，覆盖旋转中心、箭尖半径、数字位置与直立、目标死亡改向、移动槽位和释放交接。
- `tests/ArrowLayout` 的 8 个最少位移用例、6 类阵型与跨行顺序、目标分区及 10 类密集布局检查通过。
- Beta、Stable 和集成探针构建均为 0 警告、0 错误。最终运行 DLL 与构建产物的 SHA-256 一致，最终运行日志无错误或异常。
- 正常出牌录像覆盖两支单体箭矢、一支群体箭矢和加速完成。三个箭矢完成伤害结算后，部署列表与箭矢根节点均为 0。录像准备确认模型和可见手牌均为 4 张。
- 17.2 秒正常出牌视频和两段压力场景视频均已完成，含游戏音轨。启动焦点采样为 0/120，测试游戏已在录像收尾后关闭。

### 验证归档与集成

验证结果及视频已保存在原仓库的 `artifacts/task-deliverables/arrow-vfx-perf-20261110/`。

- `baseline-results.json` 保存修复前回收失败和性能基线。
- `final-results.json` 保存批处理版 20/50 支箭矢及三个特效档位的测量。
- `verification-results.json` 保存最终构建 50 支箭矢的复测和可见手牌准备断言。
- `regression-results.json` 与 `pivot-results.json` 保存混合箭矢、删除和转向回归。
- `before-pressure.mp4`、`after-pressure.mp4`、`gameplay.mp4` 为交付视频。

工作树为 `../worktrees/STS2_ShoujoKagekiJunna/arrow-vfx-perf-review-20261110`。测试使用同一份已有资源包和重编译的角色程序集，源码变更集中在箭矢 UI 及定向探针。用户已确认快进合入 `main`，并清理任务分支、工作树及其中临时测试环境。原仓库验证归档保留，已验证的 Beta/Stable 程序集更新到原仓库 `dist/ShoujoKagekiJunna/lib/`。

### 修复前审查记录

以下代码行号对应审查基线提交。

#### P2 关闭特效后，已释放箭矢的隐藏节点持续留在本场战斗

位置为 `src/UI/Arrows/ArrowHud.Feedback.cs` 第 68、85 行，以及 `src/UI/Arrows/ArrowHud.cs` 第 64–68 行。

`ReleaseArrow` 先设置 `view.Releasing = true`，在 `CoreEffects.Enabled == false` 时只隐藏根节点并返回。该分支不会创建通常负责释放整棵子树的 `ArrowReleaseVfx`。箭矢随后从 `ArrowCombat.Deployed` 移除时，HUD 的清理逻辑因为 `Releasing == true` 跳过 `Root.QueueFree()`，仅移除字典记录。隐藏根节点仍是 HUD 的子节点，其中的 `CardBonusVfx` 和 `CardInvestmentMarks` 继续持有卡牌并拥有 `_Process` 回调。

每次关闭特效状态下的箭矢结算都会增加一棵滞留子树，战斗越长，滞留节点和轮询开销越多。这些节点随战斗 HUD 销毁而释放。修复时需要给关闭特效分支安排明确的回收责任，同时保留 `ExitPosition` 给后续原生牌堆动画使用。

通过源码控制流检查确认标记、提前返回及跳过回收三个条件同时成立。实机节点数量曲线尚待测量。

#### P2 群体箭矢待机时，每帧大量分配数组并提交细碎几何

位置为 `src/UI/Arrows/ArrowHud.cs` 第 138–166、218–271 行。

每支群体箭矢每帧调用 30 次 `DrawPolygon` 绘制渐变扇面，再为左右边界及每个目标调用 3 次 `DrawPolylineColors`。扇面每次提交都构建顶点和颜色数组，折线每次提交都创建 17 元素的两类数组。三个目标时，每支箭每帧产生 90 个数组和 45 次绘图 API 调用。

以当前源码原样提取 `GroupGuideFade`、`DrawSoftFan`、`DrawSoftGroupGuide` 和 `DrawFadingGuide`，使用游戏附带的 GodotSharp 数学类型，在 .NET 9 Release 下测量。绘图 API 替换为接收原数组的计数端点。每组预热 100 帧，随后测量 1000 帧的 `GC.GetAllocatedBytesForCurrentThread()` 差值。

| 类型 | 箭矢数量 | 每帧托管分配 | 每帧绘图 API 调用 | 60 FPS 下分配速率 |
| --- | --- | --- | --- | --- |
| 单体 | 1 | 912 B | 2 | 0.052 MiB/s |
| 单体 | 10 | 9,120 B | 20 | 0.522 MiB/s |
| 单体 | 20 | 18,240 B | 40 | 1.044 MiB/s |
| 单体 | 50 | 45,600 B | 100 | 2.609 MiB/s |
| 群体，3 个目标 | 1 | 11,088 B | 45 | 0.634 MiB/s |
| 群体，3 个目标 | 10 | 110,880 B | 450 | 6.345 MiB/s |
| 群体，3 个目标 | 20 | 221,760 B | 900 | 12.689 MiB/s |
| 群体，3 个目标 | 50 | 554,400 B | 2,250 | 31.723 MiB/s |

数据覆盖绘制方法自身的托管数组分配。目标查询、排序、节点属性更新、引擎参数转换和渲染工作还会增加开销。表中的 API 调用次数与最终 GPU draw call 的数量需要分别测量。

此路径随待机箭矢持续运行。群体箭矢累积会显著提高垃圾回收压力和绘制提交工作量，构成卡顿风险。可复用顶点和颜色缓冲，预计算渐变，将扇面合并提交，并缓存同一帧目标点。

#### P2 待机引导线绕过 Core 的低特效档位节流

位置为 `src/UI/Arrows/ArrowHud.cs` 第 109、135–168 行。档位行为参考 Core 包中的 `src/Api.V1/Presentation/CoreEffects.cs`。

HUD 在 `_Process` 末尾无条件 `QueueRedraw()`，`_Draw` 也直接绘制全部扇面与引导线。Core 的 Reduced 档位通过 `Redraw` 将绘制请求限制为每 50 毫秒一次，Off 档位负责抑制装饰绘制，但 HUD 此处未使用该入口。因此调低或关闭特效后，待机引导线仍按游戏帧率执行前述数组分配和几何提交，群体卫星也继续逐帧更新。

修复时应将引导线等装饰放到独立绘制层并应用档位预算，保留箭矢主体、计数及必要的目标反馈。

#### 其他性能观察

- `UpdateLayout` 的缓存判断位于生物列表、每箭目标查询、排序和区域计算之后。稳定待机帧仍会执行这些工作，旧位置查找及移除检查还存在随箭矢数量平方增长的扫描。`TargetPoints` 在布局、姿态更新和绘制三个阶段重复求值。这些开销尚未计入上表。
- `ArrowPortraitTexture` 对已见过的同一 `AtlasTexture` 有弱引用缓存。首次遇到不同 atlas 区域时，会在显示路径中同步调用底图 `GetImage()`、解压、转换、裁切和纹理创建。批量将原版卡转箭时值得测量首次显示尖峰。同一 atlas 底图的 CPU 图像没有共享缓存。
- 部署轨迹通过 `_aimLocked` 只构建一次，部署与释放动画有固定结束时间，普通开启特效的释放路径由动画父节点统一回收。加成边框的持续粒子按箭矢紧凑模式及 Core 档位控制数量。

#### 初始局部验证

最小分配探针命令为 `dotnet run --project artifacts/arrow-vfx-perf-review/RenderAlloc.csproj -c Release --nologo`，8 组输出符合上述初始分配表格。该探针使用修复前的绘制方法，保存在工作树的 Git 忽略目录。后续实机数据和修复结果见本文前述章节。

已复核完整用户输入，所有受版本控制文档及本任务记录经过凭据模式与本地绝对路径扫描。
