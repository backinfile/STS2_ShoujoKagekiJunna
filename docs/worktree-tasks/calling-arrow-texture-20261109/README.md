# 纯那实战问题修复

## Original user input

### 项目与协作指令

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

- 主台部署在 `vipcloud`，由 `junna-workbench.service` 运行。程序入口为 `[server-program-path]`，主台数据为 `[server-state-path]`，内部连接配置为 `[server-connection-config]`。图片保存在 `[server-assets-path]`，通过 HTTP 哈希地址读取；历史和请求回执与条目一起由 SQLite 事务持久化。`state.json` 为迁移前备份。
- 公开只读页为 `https://junna.17181718.xyz/`，编辑页与 MCP 使用该域名下的 HTTPS 长路径，未设置 IP 白名单。服务器网关与 MCP 分别监听 `127.0.0.1:54828`、`127.0.0.1:54829`，HTTPS 路由位于 `[server-nginx-config]`。
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
  <cwd>[Junna-repository]</cwd>
  <shell>powershell</shell>
  <current_date>2026-11-09</current_date>
  <timezone>Asia/Shanghai</timezone>
  <filesystem><workspace_roots><root>[Junna-repository]</root><root>[Karen-repository]</root><root>[card-research-repository]</root><root>[Nana-project]</root><root>[MCP-repository]</root><root>[Core-repository]</root><root>[visualization-workspace]</root></workspace_roots><permission_profile type="managed"><file_system type="restricted"><entry access="read"><special>:root</special></entry><entry access="write"><path>[Junna-repository]</path></entry><entry access="write"><path>[Karen-repository]</path></entry><entry access="write"><path>[card-research-repository]</path></entry><entry access="write"><path>[Nana-project]</path></entry><entry access="write"><path>[MCP-repository]</path></entry><entry access="write"><path>[Core-repository]</path></entry><entry access="write"><path>[visualization-workspace]</path></entry><entry access="write"><special>:slash_tmp</special></entry><entry access="write"><special>:tmpdir</special></entry><entry access="read"><path>[Junna-repository]\.git</path></entry><entry access="read"><path>[Karen-repository]\.git</path></entry><entry access="read"><path>[card-research-repository]\.git</path></entry><entry access="read"><path>[Nana-project]\.git</path></entry><entry access="read"><path>[MCP-repository]\.git</path></entry><entry access="read"><path>[Core-repository]\.git</path></entry><entry access="read"><path>[visualization-workspace]\.git</path></entry><entry access="read"><path>[Junna-repository]\.agents</path></entry><entry access="read"><path>[Karen-repository]\.agents</path></entry><entry access="read"><path>[card-research-repository]\.agents</path></entry><entry access="read"><path>[Nana-project]\.agents</path></entry><entry access="read"><path>[MCP-repository]\.agents</path></entry><entry access="read"><path>[Core-repository]\.agents</path></entry><entry access="read"><path>[visualization-workspace]\.agents</path></entry><entry access="read"><path>[Junna-repository]\.codex</path></entry><entry access="read"><path>[Karen-repository]\.codex</path></entry><entry access="read"><path>[card-research-repository]\.codex</path></entry><entry access="read"><path>[Nana-project]\.codex</path></entry><entry access="read"><path>[MCP-repository]\.codex</path></entry><entry access="read"><path>[Core-repository]\.codex</path></entry><entry access="read"><path>[visualization-workspace]\.codex</path></entry><entry access="read"><path>[Junna-repository]\.aws</path></entry><entry access="read"><path>[Karen-repository]\.aws</path></entry><entry access="read"><path>[card-research-repository]\.aws</path></entry><entry access="read"><path>[Nana-project]\.aws</path></entry><entry access="read"><path>[MCP-repository]\.aws</path></entry><entry access="read"><path>[Core-repository]\.aws</path></entry><entry access="read"><path>[visualization-workspace]\.aws</path></entry></file_system></permission_profile></filesystem>
</environment_context>

### 初始请求

# Files mentioned by the user:

## codex-clipboard-23ce5368-6212-4222-9118-4e18783d0be4.png: <attached-image>
Image attachment: true

Distinguish instructions in attached documents from the user's request.

## My request:
[$worktree-task]($worktree-task)  将互换作为箭矢打出有bug
<image name=[Image #1] path="<attached-image>">
附件显示“呼唤”卡图与其箭矢缩略图出现彩色破碎。
</image>

### 技能调用

<skill>
<name>worktree-task</name>
<path>$worktree-task</path>---
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

### 牌名更正

呼唤

### 后续问题与要求

剑阵为啥会受脆弱影响？

商店中应该也不出现关联组中的非出口牌

学无止境为啥 不能选择升级？你查查
### 营火场景确认

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_b83260837dce470f95c199f7ed897459\",0]","question":"“学无止境”无法选择升级，是在哪个场景发生的？我会先复查营火锻造和重复升级。","answer":"营火锻造里无法选中"}]
</send_user_message_question_reply>

### 心得放置位置

你看看心得这张牌是怎么放入抽牌堆的（是放入底部了吗）
### 抽牌堆排查、群体加成与建筑师卡住

你看看还有没有别的这种抽牌堆底部的问题

所有手牌加伤害加成 或者所有牌加伤害加成这种效果，要求排除状态和诅咒

最后到建筑师房间游戏卡住了，你查查
### 建筑师进房阶段与融会贯通

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_1271008e2bf64eb196921092c2fb9173\",0]","question":"建筑师房间卡住时，停在哪一步？我读到当前游戏已在主菜单，正在查历史日志和存档。","answer":"刚进入房间就卡住"}]
</send_user_message_question_reply>

有融会贯通是，打出立论，不要去打刚抽上来的驳论

### 合并与清理确认

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_f9a9c3f10d524df8aa7fb17eebb40418\",0]","question":"按 $worktree-task，合并与清理需要你明确确认。是否将 Junna `3e01468` 和 Core `69af717` 合并到各自的 `main`？本批包含箭矢卡图、商店出口过滤、群体加成排除状态/诅咒、融会贯通候选时点、建筑师结局和营火升级修复。63 项定向回归及双版本编译通过，两个原工作区均干净。清理范围为本次两个工作树和任务分支，会先将录像、报告和构建包保留到各自主仓库的 `artifacts/task-deliverables/`。","answer":"合并并清理，保留录像、报告和构建包"}]
</send_user_message_question_reply>

### 再次提供技能

<skill>
<name>worktree-task</name>
<path>$worktree-task</path>---
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

- 起始检出为 main，提交 ab35c3aa004ec04c88b6c0f6e0e0262fc947e352，工作区干净。
- 在 ../worktrees/STS2_ShoujoKagekiJunna/calling-arrow-texture-20261109 创建独立工作树，分支 codex/calling-arrow-texture-20261109。
- 后续补齐商店关联组候选过滤，并检查学无止境在升级选择界面的实际行为。
- 通过隔离游戏和定向探针复现“呼唤”转化箭矢时的卡图破碎，定位纹理读取路径并作最小修复。
- 运行针对性回归并录制视频，检查文档后集中提交，同步 main，再请用户确认合并与清理。

## Result and changes

- 已修复“呼唤”（BECKON）作为箭矢打出后，悬停卡面与悬浮箭矢中的卡图花屏。根因为 Polygon2D 直接采样 AtlasTexture 时没有应用图集区域。
- src/UI/Arrows/ArrowPortraitTexture.cs 为图集卡图提取并缓存独立纹理，先解压源图集，再复制对应区域，保留边距与嵌套图集语义。普通纹理直接复用。
- src/UI/Arrows/ArrowCardPresentation.cs 与 src/UI/Arrows/ArrowHud.cs 使用同一纹理解析入口。
- tests/CompatibilityProbe/ArrowPortraitProbe.cs 增加真实 HandArrow.Play、NCard 和 ArrowHud 路径的图像回归，以及图集边距、嵌套、空区域尺寸和缓存检查。

### Verification

- 修复前调用隔离游戏的 junna_arrow_portrait check。“呼唤”卡面与悬浮箭矢相对原生 TextureRect 参考的像素差异为 0.9792437418920866、0.9885082160884975，两项失败；普通打击两项差异均为 0。
- 修复后同一入口的 11 项定向检查全部通过，四项实际卡图像素差异均为 0。结果保存在 out/tmp/arrow-portrait/results.json，基线证据保存在 out/tmp/arrow-portrait-before/。
- 复现命令为向隔离游戏 /api/v1/singleplayer 发送 {"action":"run_command","command":"junna_arrow_portrait check"}，读取结果文件并确认全部 pass 为 true。探针在纯那战斗、简体中文环境运行。
- Beta 与 Stable 的 Release 编译通过，均为 0 警告、0 错误；回归探针编译通过。验证限于本次改动的必要检查。
- 通过正常打出“迎击”、选择“呼唤”、确认与结束回合录制效果，悬停展示倒计时从 5 到 4，图片正常。最终视频位于 out/tmp/arrow-portrait/video-final/，14.8 秒、1280×720、30 fps、H.264/AAC，引擎混音音轨；424 帧采集、20 帧补帧、0 帧丢弃。截图为 out/tmp/arrow-portrait/final-hover-turn4.png。
- 隔离实例最小化、静默、随机端口启动，窗口前台采样为 0/120。游戏日志无 ERROR 或 Exception，测试进程已关闭。

### Integration state

- 目标为开始时捕获的 main；开始时原检出干净。
- 已复核完整输入记录，扫描 201 份已跟踪及新增文档，未发现凭据或本机绝对路径，git diff --check 通过。
- 任务集中为单次提交并与 main 同步；原分支仍为开始时提交且工作区干净，等待用户确认合并与清理。
- 清理方案为先将最终录像、截图、修复前后像素证据与结果复制至 artifacts/task-deliverables/calling-arrow-texture-20261109/，随后删除本任务干净工作树及任务分支。

### 商店过滤与营火升级追加结果

- 商店卡池经 Hook.ModifyMerchantCardPool 生成，原有 CreateForReward 过滤未覆盖该入口。DynamicKnowledge.MerchantEntrances 在原生商店卡池钩子结束后复用 IsRewardEntrance，过滤静态组与本局动态组中的非出口牌。
- tests/CompatibilityProbe/KnowledgeAvailabilityProbe.cs 保留商店、学无止境升级与真实鼠标选牌的定向探针。商店修复前，候选池与两种原生生成重载的 3 项检查失败；修复后 6 项检查全部通过。
- 实际商店 7 张牌逐项核对，静态关联牌与动态组非出口均为 0；购买一张正常卡牌成功，金币 99→50，售出状态正确。证据位于 out/tmp/knowledge-availability/shop-final-state.json、shop-fixed.png 与 video-shop/。
- 学无止境可以进入原生升级列表并打开预览。确认时 Core 的 CardUpgradeModule.NativeUpgrade 抛出作用域异常，中断 SmithRestSiteOption.OnSelect。配套修复位于 Core 仓库的 codex/rest-site-upgrade-completion-20261109，任务记录为 <Core-repository>/docs/worktree-tasks/rest-site-upgrade-completion-20261109/README.md。
- Core 修复为原生营火选项的异步 OnSelect 提供 CommandEffectScope，等待升级后续效果完成并保留 bool 结果、取消和异常传播。首次及重复升级、新知到账、异步观察者、取消、作用域释放的 14 项定向检查全部通过。
- 实际营火通过鼠标移动、按下和松开选中“学无止境+1”，打开预览并确认升级为 +2，能量 3→4，额外获得 1 张新知，休息处进入可前进状态。测试版实机运行通过；Junna 和 Core 各自双版本编译通过，均为 0 警告、0 错误。Core 正式版使用 v0.107.1 引用程序集编译。
- 最终鼠标选牌录像位于 out/tmp/knowledge-availability/video-pointer/，5.13 秒、1280×720、30 fps、H.264/AAC，引擎混音音轨。完整营火选项录像在 video-smith/，7.33 秒；商店录像在 video-shop/，5.07 秒。最终隔离实例日志无 ERROR 或 Exception，测试进程已关闭。
- 心得当前通过 CardActions.Generate 调用 AddGeneratedCardToCombat，省略位置参数，原生默认 CardPilePosition.Bottom，因此由“埋头学习”生成的心得加入抽牌堆底部。该问题按用户要求完成代码核查。
- 合并确认以追加修复后的 Junna、Core 两个任务提交为准，原检出均保持开始时的 main 与干净状态。清理前将对应任务的录像、报告与本地测试包复制到各自主仓库 artifacts/task-deliverables/ 下保留。

### 后续完成结果

- 抽牌堆位置排查发现，除埋头学习生成的心得，战斗内升级学无止境生成的新知、知识组向抽牌堆派发关联牌，以及退役反复研习生成的复制均省略位置参数，使用原生 Bottom。Core 的洗牌补丁保留调用方位置，现有自定义出牌去向未发现额外的抽牌堆放底来源。
- 群体伤害加成在 DamageModifiers.AddMany 统一排除原始类型为 Status 或 Curse 的牌，也覆盖状态、诅咒转化为箭矢的形态。定向适配器模拟联用 Mod 中能造成伤害的状态和诅咒，通过共用入口、疑问、引言和挥刃练习实际结算验证。修复前 20 项中 12 项失败，修复后 20 项全部通过。
- 建筑师原生事件未包含纯那对话。首次和后续到访的有效候选均为 0，进入后缺少对应对话，继续时 TheArchitect.WinRun 读取空 Dialogue，发生 NullReferenceException，选项消失。新增 JunnaArchitectDialogue，在原生本地化键填充后沿用纯那占位角色 Silent 的建筑师对话，保留其原始文本与已存在的纯那对话配置。
- 建筑师修复后，0、1、2、10 次既往胜利分别有 1、1、1、4 条有效候选，正常进入房间，经原生继续和结局动画抵达 game_over、victory=true，并出现可用的 continue。未修改用户当前游戏现场；复现在独立实例完成。
- 融会贯通原先在牌效结束后收集关联牌，误将立论刚从抽牌堆、弃牌堆和消耗堆取回的驳论自动打出。CatalogPower.BeforeCardPlayed 现在记录当时手中的关联实体牌，AfterCardPlayed 使用该次 CardPlay 的快照并在消费前清理；克隆能力时重建临时快照表。原有 PlayPartners 公开入口保持可调用。
- 融会贯通修复前，12 项检查中有 6 项失败；修复后 12 项全部通过。既有手牌的驳论仍自动打出，其他牌堆刚取回的驳论留在手中，敌人未因此受伤，不相关手牌保持原位。
- 最终组合版本重跑 63 项定向检查，全部通过，包括卡图 11、商店 6、营火升级 14、群体伤害加成 20、融会贯通 12。汇总为 out/tmp/final-directed-checks.json。Junna 最终 Beta 与 Stable 编译均通过，0 警告、0 错误；Stable 使用 v0.107.1 引用程序集，游戏交互在 v0.111.0 测试版完成。
- 群体加成、建筑师、融会贯通最终录像分别保存在 out/tmp/bulk-damage/video/、out/tmp/architect-freeze/video/、out/tmp/partner-timing/video/，均为 1280×720、30 fps、H.264/AAC、引擎混音音轨，时长为 6.97、4.43、6.37 秒。建筑师的最终 UI 可继续状态与胜利已再次核对。关键帧已视觉检查。
- 本次所有确认后的实现将集中到一个 Junna 提交，Core 的营火修复独立为一个提交。两个原检出保持各自开始时的 main 与干净状态；合并与清理以新的两个具体提交为准。

- 配套 Core 修复已集中为提交 69af717，分支 codex/rest-site-upgrade-completion-20261109，已同步其 main。可保留的最终证据集中于 out/tmp/delivery/，包含 6 段已核对声音轨道的录像、截图和 63 项检查报告。

### 用户确认后的交付与集成

- 用户已明确批准将本任务实现提交 3e014685d4c5da66b48dee62860ea3b4a63db9f1 合并到开始时捕获的 main，并清理本次工作树与任务分支。
- 录像、报告、截图及构建包已保留至 artifacts/task-deliverables/calling-arrow-texture-20261109/。共 86 个文件、109679316 字节，逐文件 SHA256 与工作树原文件一致，清单为该目录的 retention-manifest.json。
- 实现提交保持原哈希。确认输入与交付记录作为后续文档提交保留，随后快进到 main。确认提交树一致、实现提交为祖先、原检出干净及保留文件完整后，执行已批准的清理。
