# 追逐着你而去：两张与三张的循环检查

## Original user input

### 用户消息 1：工作区说明

# AGENTS.md instructions for <repository-root>

<INSTRUCTIONS>
# 项目概况

- 《杀戮尖塔 2》星见纯那角色 Mod，ID：`ShoujoKagekiJunna`；核心机制为飞矢、知识与伤害加成。
- 技术栈：C# / .NET 9、Godot 4.5.1（MegaDot）、BaseLib 3.4.8
- `src/Combat/` 按飞矢、卡牌、伤害、知识分组，`src/UI/` 存放 HUD、卡面与动画，`src/Models/` 定义游戏模型。目录调整沿用已有命名空间及 Godot UID，保持类型引用兼容。
- `data/card-pool.json` 为编译时嵌入的运行卡池，`ShoujoKagekiJunna/` 为游戏资源，`docs/design/` 为工作台，`docs/implementation/` 为实现记录，`tests/Integration/` 为独立测试模组。公共构建配置在 `Directory.Build.props`。
- 构建：`./build.ps1 -CorePackage <Core发行目录>`；产物为 `dist/ShoujoKagekiJunna/`，运行时依赖独立 Core 与 BaseLib 工坊条目。`tools/prepare-workshop.ps1 -CoreWorkshopId <Core工坊ID> -PreviewImage <预览PNG> -OutputDirectory <新目录>` 生成仅含 Junna 的工坊工作区及必需物品配置。

# 卡牌效果实现

- 实现卡牌效果时，优先查找游戏本体中的类似效果，参考其实现。

# 卡牌入堆位置

- 生成或复制到抽牌堆时必须显式指定位置。未明确置顶或置底的生成、复制及知识附带发牌使用 `CardPilePosition.Random`，复用原生生成事件与同步随机数。定向抽取先置顶再走正常抽牌，永久牌组发放按牌组规则处理。修改时按[卡牌入堆位置约定](docs/implementation/card-pile-placement.md)核对位置、数量、版本、原牌序及实际入口，保留同类回归结果。

# 工作台编辑

少女歌剧工作台的前端、MCP 服务和部署统一由 [Core 仓库](https://github.com/backinfile/STS2_ShoujoKagekiAijoCore/blob/main/tools/workbench/README.md) 维护，本仓库的旧工作台源码作为历史基线，不再用于更新部署。工作台内容使用 Core 的 `shoujo_workbench` 并指定 `modId: "junna"` 读取和修改；旧 `junna_workbench` 连接兼容纯那默认工作区。具体流程见 [docs/design/AGENTS.md](docs/design/AGENTS.md)。

- 主台部署在 `vipcloud`，由 `junna-workbench.service` 运行。程序入口为 `<workbench-service-directory>`，主台数据为 `<workbench-state-database>`，内部连接配置为 `<workbench-private-backend-config>`。图片保存在 `<workbench-assets-directory>/`，通过 HTTP 哈希地址读取；历史和请求回执与条目一起由 SQLite 事务持久化。`state.json` 为迁移前备份。
- 公开只读页为 `https://junna.17181718.xyz/`，编辑页与 MCP 使用该域名下的 HTTPS 长路径，未设置 IP 白名单。服务器网关与 MCP 分别监听 `127.0.0.1:54828`、`127.0.0.1:54829`，HTTPS 路由位于 `<workbench-nginx-config>`。
- 当前长路径及密钥读取主目录 `docs/design/workbench/local-save.config.json` 的 `remoteURL`、`remoteToken`。编辑页地址为 `remoteURL + '/'`，MCP 接口为 `remoteURL + '/mcp'`。私有连接配置保存在 Git 忽略范围内。其他工作树复用主目录客户端及其连接配置。
- 当前会话尚未加载新工具时，使用 `node docs/design/workbench/mcp-client.cjs <工具名> <参数JSON文件>` 连接同一个 MCP 服务。入口轮换后，已启动的 STDIO 客户端需要重新连接，命令行客户端会读取最新配置。源码修改使用普通文件工具，工作台内容由云端 MCP 持久化。部署流程以 [Core 部署说明](https://github.com/backinfile/STS2_ShoujoKagekiAijoCore/blob/main/tools/workbench/deploy/README.md) 为准。
- 网页首次加载 `/snapshot.json`，之后通过 MCP 按 revision 同步增量。公开快照由独立发布进程更新；完整图片与历史在导出时读取。迁移后继续复用原有条目版本、assetId 和 requestId。

# 契合设计

以下精选自工作台的长期约定。设计前通过 MCP 读取 `styles` 中对应规则、已确认用词及相关卡牌的当前内容。

- 以当前牌效选择卡名、契合和图片，核对名称、类型、效果、体系主题、图片、组内关系与来源；契合调整采用最小改动，保留当前机制、数值及用户正在调整的牌面。
- 飞矢可用物理箭、言叶或星：分别表现瞄准与命中、话语的传递与抵达、追寻与飞行轨迹。按实际子类型选图，攻击飞矢体现主动攻击，准备与辅助飞矢体现准备、等待及完成后的收益。
- 伤害加成的通用契合是纯那的日常锻炼，围绕体能、基本功、射术、反复练习与训练成果；带伤害的训练牌同时体现主动出手。
- 知识契合由学习行为或有明确内容与来源的知识承担。典籍先确认纯那在原作或角色访谈中阅读、引用、演绎的直接联系，再选择内容；人物档案围绕对应人物的身份、经历与专长。同组主牌与关联牌围绕同一知识关系，可呈现释义、例证、推导、应用与成果。
- 能量的通用契合使用星星，以点亮、汇聚、留存和延续对应收益；飞矢中的星突出轨迹与抵达。有直接原作或知识来源的牌可沿用原名，以具体内容衔接收益。跨体系牌以主体系契合为主。
- 卡名优先选对应场景中已核对的连续原词，保持原意；缺少合适原词时选自然、完整的常用概念、动作或物件名。普通卡名通常7字以内，保留原句优先于机械限字；核对常见谐音及用户已否定的名称。原文、译名、概括名和设计解释分别记录。不要使用自造词。
- 卡名、契合与卡图共同体现牌的用途：攻击突出出手、弹道、命中或进攻性的论辩与言叶运用；防御突出抵挡、保护与应对；准备和积累采用相应动作与物件。配图优先用原作画面，在实际卡框与飞矢遮罩中检查脸、手、武器和动作方向，清理字幕、水印及录制界面干扰。不同牌采用有区分的构图，相邻帧或同一动作的换裁切也纳入重复检查。
- 改名、换图或重选契合后，同步基础与升级文本的牌名引用、关联牌、知识组标题、契合池、出处及提示，并复查相关卡面、引用跳转、重名和重复图。复用备用或退役素材时，按新牌当前效果更新解释与关联，保留素材出处。

# 图片素材处理

- 查找原作卡图时，优先在 [Xenodude Scribbles](https://xenodude.com/) 检索对应作品与话数，查看无字幕原帧并读取原尺寸图片。已确认 TV 第9话拥抱镜头来源：[第9话截图与评论](https://xenodude.com/2018/09/09/revue-starlight-ep-9-original/)。

所有生成的图片在交付前都必须使用本地去 AI 摩尔纹工具处理一遍，去除摩尔纹与生成伪影。优先复用项目已安装的 Artifact Cleaner，按原尺寸对比处理前后，检查眼镜、发丝、弓弦和服装边缘；保留生成原图，并记录本地处理工具、参数与成品路径。

# STS2 MCP 连接与使用
使用 MCP 操作 STS2 时，需要读取其mcp readme文档。默认使用最小化+静音来启动游戏进行测试, 需要选择一个随机端口号避免和其他任务冲突。

不测试教程，解锁等内容时，直接以满解锁（unlock all）状态启动游戏即可。

使用mcp进行任务时，需要录制视频给用户看效果， 录制前准备好环境，录制中尽量不要调用cmd等其他直接修改的操作了，应使用游戏合法操作继续进行。
</INSTRUCTIONS><environment_context>
  <cwd><repository-root></cwd>
  <shell>powershell</shell>
  <current_date>2026-11-17</current_date>
  <timezone>Asia/Shanghai</timezone>
  <filesystem><workspace_roots><root><repository-root></root><root><karen-repository-root></root><root><card-research-repository-root></root><root><nana-project-root></root><root><sts2-mcp-repository-root></root><root><core-repository-root></root><root><visualization-output-directory></root></workspace_roots><permission_profile type="managed"><file_system type="restricted"><entry access="read"><special>:root</special></entry><entry access="write"><path><repository-root></path></entry><entry access="write"><path><karen-repository-root></path></entry><entry access="write"><path><card-research-repository-root></path></entry><entry access="write"><path><nana-project-root></path></entry><entry access="write"><path><sts2-mcp-repository-root></path></entry><entry access="write"><path><core-repository-root></path></entry><entry access="write"><path><visualization-output-directory></path></entry><entry access="write"><special>:slash_tmp</special></entry><entry access="write"><special>:tmpdir</special></entry><entry access="read"><path><repository-root>/.git</path></entry><entry access="read"><path><karen-repository-root>/.git</path></entry><entry access="read"><path><card-research-repository-root>/.git</path></entry><entry access="read"><path><nana-project-root>/.git</path></entry><entry access="read"><path><sts2-mcp-repository-root>/.git</path></entry><entry access="read"><path><core-repository-root>/.git</path></entry><entry access="read"><path><visualization-output-directory>/.git</path></entry><entry access="read"><path><repository-root>/.agents</path></entry><entry access="read"><path><karen-repository-root>/.agents</path></entry><entry access="read"><path><card-research-repository-root>/.agents</path></entry><entry access="read"><path><nana-project-root>/.agents</path></entry><entry access="read"><path><sts2-mcp-repository-root>/.agents</path></entry><entry access="read"><path><core-repository-root>/.agents</path></entry><entry access="read"><path><visualization-output-directory>/.agents</path></entry><entry access="read"><path><repository-root>/.codex</path></entry><entry access="read"><path><karen-repository-root>/.codex</path></entry><entry access="read"><path><card-research-repository-root>/.codex</path></entry><entry access="read"><path><nana-project-root>/.codex</path></entry><entry access="read"><path><sts2-mcp-repository-root>/.codex</path></entry><entry access="read"><path><core-repository-root>/.codex</path></entry><entry access="read"><path><visualization-output-directory>/.codex</path></entry><entry access="read"><path><repository-root>/.aws</path></entry><entry access="read"><path><karen-repository-root>/.aws</path></entry><entry access="read"><path><card-research-repository-root>/.aws</path></entry><entry access="read"><path><nana-project-root>/.aws</path></entry><entry access="read"><path><sts2-mcp-repository-root>/.aws</path></entry><entry access="read"><path><core-repository-root>/.aws</path></entry><entry access="read"><path><visualization-output-directory>/.aws</path></entry></file_system></permission_profile></filesystem>
</environment_context>

### 用户消息 2：任务与技能调用

[$worktree-task]($worktree-task)  两张追逐者你而去会不会无限循环？三张呢

### 用户消息 3：技能全文

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
- For merge and cleanup confirmation, use `request_user_input_async` when available and permitted; otherwise ask in chat. Ask one short, direct question with the action choices. One explicit confirmation may approve both merging and the listed cleanup; the original task request alone is not approval.
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
6. Show the target branch, task commit, change summary, verification results, any original uncommitted state, and proposed cleanup items once; reuse this summary if already shown. Ask directly whether to merge and clean up, merge and keep, or defer.
7. Only after confirmation, merge the synchronized task branch back without creating an additional integration commit when possible. If integration would overwrite uncommitted state, preserve the worktree and report that integration is pending instead.
8. Verify the committed trees match, checks pass, the original branch contains the consolidated task commit, and pre-existing uncommitted state was preserved.
9. After successful integration verification, remove this run's clean worktree, task branch, and disposable temporary files covered by the cleanup approval, after proving needed content is preserved. Reuse combined approval from step 6 without asking again. If only merging was approved, retain these items until the user approves cleanup; request further confirmation for any additional cleanup items.

Report the resulting branch, commit, verification, cleanup state, and task document path.
</skill>

## Brief task plan

1. 固定原检出 main、起始提交 9b66ca5f6e018ece4c923c2d11300f6defec46ee；保护原检出已有两处代码改动和未跟踪目录。
2. 在 ../worktrees/STS2_ShoujoKagekiJunna/chase-loop-20261117 的 codex/chase-loop-20261117 分支读取卡牌数据、追随钩子与飞矢生命周期，推演两张、三张和下一次独立触发。
3. 执行必要的最小验证，记录结论。若当前实现已阻断循环，保留游戏行为，只提交调查记录。
4. 扫描 Git 跟踪及新增文档中的敏感内容和本机绝对路径；提交、同步后按 $worktree-task 请求合并及清理确认。

## Result and changes

### 结论

当前实现下，两张和三张「追逐着你而去」均不会互相无限自动触发。

| 场景 | 追加打出的同名牌 | 本轮出牌总数 |
| --- | --- | --- |
| 有 A、B，手动打出 A | B 一次 | 2 |
| 有 A、B、C，手动打出 A | B、C 各一次 | 3 |
| 有 A、B，打出另一张飞矢 X | A、B 各一次 | 3 |
| 有 A、B、C，打出另一张飞矢 X | A、B、C 各一次 | 4 |

表中数量按卡牌可正常打出、战斗持续中的情形计算。

### 实现依据

- `src/Combat/JunnaCombatHooks.cs:56`：`FollowingPlayers` 按玩家标记正在执行的一轮追随。
- `src/Combat/JunnaCombatHooks.cs:61`：追随期间嵌套出牌再次到达该钩子时，`FollowingPlayers.Add(owner)` 返回 false，直接结束其追随部分。
- `src/Combat/JunnaCombatHooks.cs:64`：候选列表排除触发牌自身，并一次性 `ToArray()`；`CardActions.All` 用 `Distinct()` 去重跨牌区候选。
- `src/Combat/JunnaCombatHooks.cs:70`：重新打出已布置的追逐牌前，取消其原有布置；每张实际牌仍只有一份布置记录，倒计时重新从 1 回合开始。
- `src/Combat/JunnaCombatHooks.cs:72`：逐张等待原生 `CardCmd.AutoPlay` 完成；循环完成或抛出异常后才在 `finally` 中解除标记。保护范围覆盖整轮追加出牌，不只保护单张牌。
- 本地游戏本体 `CardCmd.AutoPlay` 等待 `CardModel.OnPlayWrapper`；后者等待 `Hook.AfterCardPlayed`。该原生钩子继续等待 `AfterCardPlayedLate`，所以嵌套追随钩子执行时标记仍有效。
- `src/Combat/Arrows/ArrowDeployment.cs` 的 `Resolve` 直接结算牌效，再经 `ArrowNativeHooks.Complete` 补发蜷缩能力的伤害结束处理；飞矢完成伤害不重放出牌钩子。该牌规则也没有 `LoopAsArrow`，不会在完成后自行重新布置。

下一次独立打出飞矢会重新开启一轮追随；这属于新的外部触发。多张追逐牌的自动打出只形成一轮批量布置。

### 验证与变更

- 本次采用源码与本地游戏本体调用链核对，覆盖两张、三张、其他飞矢触发、后续独立触发和延迟完成；没有启动游戏或重新运行集成测试。
- 核对既有回归代码 `tests/WorkbenchFitSync/Workbench5000Probe.cs:114`：包含双追逐牌防递归及再次布置。
- 核对既有回归代码 `tests/Integration/CurrentPoolTriggers.cs:98`：包含五个牌区中的五张追逐牌、无重复布置、单次完成伤害和完成不再次触发。历史全卡池回归结果见 `docs/implementation/card-tests-r5294.md`；此处区分既有记录与本次源码核对。
- 只新增本任务 `README.md`，游戏代码及牌面保持现状。
- 已扫描全部 585 份 Git 跟踪及新增文本类文档，敏感信息和本机绝对路径模式检查无命中。
- 起始检出的两处代码改动及未跟踪目录保持原位，合并和清理等待用户按 $worktree-task 确认。
