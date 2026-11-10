# 关联牌独立列表与谬误知识组

## Original user input

### 用户消息 1

# AGENTS.md instructions for <repository>

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

- 主台部署在 `vipcloud`，由 `junna-workbench.service` 运行。程序入口为 `/opt/junna-workbench/current`，主台数据为 `/var/lib/junna-workbench/state.sqlite`，内部连接配置为 `/var/lib/junna-workbench/backend.json`。图片保存在 `/var/www/junna/assets/`，通过 HTTP 哈希地址读取；历史和请求回执与条目一起由 SQLite 事务持久化。`state.json` 为迁移前备份。
- 公开只读页为 `https://junna.17181718.xyz/`，编辑页与 MCP 使用该域名下的 HTTPS 长路径，未设置 IP 白名单。服务器网关与 MCP 分别监听 `127.0.0.1:54828`、`127.0.0.1:54829`，HTTPS 路由位于 `/etc/nginx/conf.d/junna-https.conf`。
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

<environment_context>
  <cwd><repository></cwd>
  <shell>powershell</shell>
  <current_date>2026-11-10</current_date>
  <timezone>Asia/Shanghai</timezone>
  <filesystem><workspace_roots><root><repository></root><root><related-workspaces></root></workspace_roots><permission_profile type="managed"><file_system type="restricted"><entry access="read"><special>:root</special></entry><entry access="write"><path><authorized-workspaces></path></entry><entry access="write"><special>:slash_tmp</special></entry><entry access="write"><special>:tmpdir</special></entry><entry access="read"><path><workspace-git-and-agent-configuration></path></entry></file_system></permission_profile></filesystem>
</environment_context>

### 用户消息 2

[$worktree-task]($worktree-task)  修改关联牌规则，每张牌都有自己的关联牌列表，卡牌与卡牌之间互不影响。 然后多在工作台中创建多组带着谬误这张牌的知识

### 用户消息 3

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

## Brief task plan

1. 从 main 的 ab35c3aa004ec04c88b6c0f6e0e0262fc947e352 创建 codex/independent-card-relations-20261110，工作树位于 ../worktrees/STS2_ShoujoKagekiJunna/independent-card-relations-20261110。开始时主目录干净。
2. 将知识关联改为每张牌的有向列表，修正标准化、编辑和展示，保留单次附带发放规则。
3. 经 MCP 更新知识机制，新增六组复用 JK122B 谬误的知识草案。
4. 执行定向回归，检查文档敏感数据，整理单个提交并同步 main，等待合并与清理确认。

## Result and changes

- 独立关联程序与知识规则已发布并验证，工作台 revision 为 r4461。六组知识已通过 MCP 写入备用区，代码整合目标为 main。
- 关联列表按入口单向保存。支持共用关联牌、互相引用与关联牌自己的列表，入池状态独立保留。编辑一张牌的关联列表时保留其他牌的列表。
- 编辑器允许所有卡牌配置关联，保留当前预览来源，复制卡牌时创建独立列表。卡池统计只沿入池牌的直接关联展开。
- 六组草案为 JK273 辨误、JK274 审辩、JK275 证伪、JK276 明察、JK277 逻辑训练、JK278 纠错，分别附带 1、1、2、1、1、2 张现有 JK122B 谬误。草案已放入备用区，使用默认占位图。新增数量、备用状态与占位图由本次实现选定。
- 内容差异保存在 workbench-changes.json，通过云端 MCP 读取版本后提交。知识机制、独立列表风格约定与 meta 中相关规则一并更新。
- 17 项领域、展示与统计定向测试，以及 8 项 MCP 字段合并、冲突、并发与幂等定向测试通过。最初 8 项基线通过。
- 最新云端 r4459 数据在本地标准化后加入草案，966 张已有卡牌内容保持原值，新增 6 张牌与 6 个列表，通过 JSON 往返和幂等标准化。只读预览构建成功，包含 972 张牌、171 个列表。
- 60 份跟踪与新增文本文档经过敏感字段与本地绝对路径审查，无发现。
- 第一次生产构建命令被自动审批拒绝，理由为将连接密钥写入临时配置文件。该命令未执行。改用不含凭据的源码增量包，复用现有服务器连接配置。
- 随后的增量发布命令被自动审批拒绝，理由为生产部署与共享服务重启缺少明确授权。该命令未执行。已通过异步问题请求用户授权，内容将在新逻辑部署后通过 MCP 提交。
- 本次按工作台与设计数据范围实施。


## 同步状态

- 开始时固定的整合目标为 main，原始提交为 ab35c3aa004ec04c88b6c0f6e0e0262fc947e352，原目录干净。
- 工作期间 main 推进至 4c8b1f37b11e185faf9034f1b5a2c0f70b701a9c，新增改动位于游戏运行逻辑和其他任务文档，与本次工作台源码无重叠。整理任务提交后合并该本地 main 到任务分支。
- 用户消息7已明确批准生产发布、服务重启、合并与清理。


## 用户后续输入

### 用户消息 4

<external_codex_apps_open_page>{"page_id":null}</external_codex_apps_open_page>

### 用户消息 5

<external_codex_apps_selected_page_part_1_of_1>Companion Page snapshot part 1 of 1:
{"page_id":null,"document_view":null}</external_codex_apps_selected_page_part_1_of_1>

### 用户消息 6

允许加到工作台的备用区

## 备用区提交结果

- 用户已允许将六组知识加入工作台备用区。通过 MCP 重新读取12个新条目的 missing 版本后，一批创建6张牌与6个知识组，提交编号为 0dd9d4b3-c88d-4797-8d4f-43578330cda4，工作台 revision 为 r4460。
- JK273 辨误、JK274 审辩、JK275 证伪、JK276 明察、JK277 逻辑训练、JK278 纠错均已保存 reservedAt，分别附带1、1、2、1、1、2张现有 JK122B 谬误。读取回验覆盖全部新增字段及组内数量。
- 现有云端程序仍按旧规则自动构建双向关联，因此本次提交还自动更新了谬误的 partnerCardIds，追加六个入口。其牌面、费用、闪耀和图片保持原值。
- 该次内容写入独立于生产发布。独立关联列表的程序发布、共享服务重启以及合入 main 与清理仍等待明确确认。用户对备用区写入的授权已执行。
- 提交与回读记录保存在 workbench-reserve-receipt.json。六张草案沿用默认占位图。

- 公开 snapshot.json 已回验 r4460，六张牌均处于备用区，组内配置已通过 MCP 逐字段读取回验。

## 用户发布、合并与清理授权

### 用户消息 7

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_70d60ddc3fa04e3d84dbe497274c0ca3\",0]","question":"六组知识已加入备用区 r4460。独立关联源码提交为 6556a97，当前任务分支为 4e35789，已同步 main，25项定向测试通过，主目录干净。自动审批要求明确授权云端发布与服务重启，$worktree-task 还要求确认合并和清理。是否继续发布独立关联逻辑、重启工作台并合入 main？清理仅涉及本次本地工作树、任务分支和其中临时文件，任务记录与MCP回执会随代码保留。","answer":"发布、合并并清理"}]
</send_user_message_question_reply>

### 用户消息 8

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


## 发布与最终验证

- 已发布 20261110-independent-card-relations，保留此前 20261108-initial-config 版本，服务重启后为 active。更新覆盖后端标准化模块、编辑页与公开页的运行脚本，现有连接配置继续由服务器管理。
- 通过 MCP 更新知识机制、关联牌独立列表风格约定与 meta 中的相关规则，提交编号为 7c761b58-97e4-49aa-bb0a-15b6e0f6290d，revision 为 r4461。六组备用知识及171个列表的内容保持原值。
- 972张牌的直接关联列表逐一核对通过，231张原有派生列表按新规则变为入口自己的列表，谬误的列表为空。所有卡牌的其他字段保持原值，Power与遗物也保持原值。
- 17项领域、展示和统计回归及8项MCP定向回归再次通过。公开 snapshot.json、公开运行脚本、服务状态与后端源码摘要回验通过。
- 发布与验证记录保存在 deployment-verification.json，备用区创建回执保存在 workbench-reserve-receipt.json。六张新增候选使用默认占位图。
- 原目录仍为 main 的 4c8b1f37b11e185faf9034f1b5a2c0f70b701a9c，工作区干净。审阅源码提交为6556a97，最终代码与记录整理为单个任务提交，以已同步的本地 main 为父提交，按用户授权快进整合。
- 清理许可覆盖本次 ../worktrees/STS2_ShoujoKagekiJunna/independent-card-relations-20261110 工作树、codex/independent-card-relations-20261110 分支和工作树中的临时文件。任务记录、两份MCP/发布回执随整合代码保留。


## 用户回归反馈与修复计划

### 用户消息 9

<external_codex_apps_open_page>{"page_id":null}</external_codex_apps_open_page>

### 用户消息 10

<external_codex_apps_selected_page_part_1_of_1>Companion Page snapshot part 1 of 1:
{"page_id":null,"document_view":null}</external_codex_apps_selected_page_part_1_of_1>

### 用户消息 11

出问题了，谬误怎么一下子全没了？

### 复现与计划

- 当前 main 为4142bed，原目录干净。沿用已获批准的同名工作树、任务分支、部署与清理范围继续修复。
- MCP历史r4467显示“删除一组 · 辨误”同时给JK273与共享JK122B写入相同trashedAt，其他组继续引用JK122B。公开预览会过滤已删除成员，因此谬误在其他组也消失。
- 三张牌、两组共用成员的最小复现直接调用domain.moveCards，断言“删除A组必须保留C组仍在引用的共享牌B”失败。历史补丁与最小复现一致，直接定位整组生命周期操作遗漏共享成员保护。
- 已通过MCP只移除JK122B误写的trashedAt，提交02ef970a-63ae-4ca1-b0cd-04eec7e2662d，revision为r4473。其他卡牌及用户后续修改保持原值。
- 修复删除、备用与移回整组的目标选择。操作所属入口及独有成员，共享成员与独立入池牌保留自身状态。新增定向回归验证红绿结果，发布后核对数据与前端，再按既有授权合入main并清理。


## 共享牌整组操作修复结果

- 根因为原“整组删除”按全组成员直接写入全局trashedAt。关联列表改为独立后，生命周期操作仍沿用旧行为，遗漏其他入口对同一模型的引用。
- domain.moveCards在整组删除、备用和移回时，保留其他未删除入口仍引用的成员，以及具有独立入口或入池身份的牌。当前入口与独有成员继续沿用既有整组操作。
- 共享成员删除、备用、移回和独立入口四项回归先全部失败，修复后连同相关领域、展示测试共15项通过，原始三牌两组最小复现也通过。
- 已发布20261110-shared-knowledge-lifecycle，后端、编辑页和公开页的模块摘要一致，服务为active。保留上一发布版本。
- 已通过MCP只恢复共享JK122B的trashedAt，原r4467对JK273的删除和用户对JK274等条目的后续修改保持原值。回执为shared-knowledge-recovery.json。
- 浏览器打开公开工作台的备用区，证伪知识组中实际显示证伪、谬误第1张和谬误第2张，共3张。原始“其他组中谬误也消失”的症状已恢复。
- 本次沿用同一任务已批准的发布、合入main与清理范围，源码与回执形成一个修复提交。
