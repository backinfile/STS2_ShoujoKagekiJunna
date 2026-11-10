# 箭矢 UI 与特效审查

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
```

### 审查请求

```text
[$worktree-task]($worktree-task)  review 下箭矢相关ui和特效，看看有没有可以优化的地方
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

### 用户确认独立审查

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_cdcdfe1c47854cd58263c1d192c20e3c\",0]","question":"$impeccable 的 critique 流程要求两个独立子代理分别审查设计和实现证据，并规定先取得确认。是否允许本次使用两个子代理？","answer":"允许两个子代理"}]
</send_user_message_question_reply>

### 用户要求完整重构计划

```text
你按重构方式来做，尽量好的重新实现，你来写一个简要计划，全部修复以上内容
```

### 用户确认实施

```text
全部修改吧
```

### 用户修正悬停排列

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_bc78b4ad3a4e492ca573c73542c7a145\",0]","question":"是否将已验证的 058f3f9 快进合入 main？任务提交为 f61ebcf。选择清理时，会先把两轮证据及构建产物保留到主仓库 artifacts/task-deliverables/arrow-ui-review-20261110/，再删除本任务工作树、任务分支、隔离游戏和临时文件。$worktree-task 要求这一步单独确认。","answer":"？你这个预览ui不太对你看看这个hover卡牌排列怎么优化（要求按游戏ui而非webui）"}]
</send_user_message_question_reply>

### 用户再次提供技能

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

1. 继续使用审查工作树及已捕获的 main 集成目标。把攻击覆盖规则作为共享查询，显示目标独立于原始效果目标。
2. 重构箭矢预览管理，按实例缓存卡面，以原生手牌式遮叠展开呈现，支持邻牌悬停、滚轮和键盘逐张浏览。
3. 给稳定分区布局加入纵向容量和超量入口，统一悬停、导线和目标高亮，提高计数可读性。
4. 给批量完成加入独立呈现节奏，保持既定牌效顺序、目标死亡及重新部署语义。
5. 定向验证覆盖、预览复用及翻页、密集布局、批量时序与特效档位，录制合法操作过程。整理提交后请求合入及清理确认。

## Result and changes

### 初次审查结果

审查完成，发现一项 P1 与四项 P2。P1 已实机复现，群星闪耀下满弦射击仍仅指向单个敌人，但四名敌人各受到 25 点伤害。

完整审查位于 [箭矢 UI 与特效审查](../../../.impeccable/critique/2026-11-10T06-39-40Z__src-ui-arrows-arrowhud-cs.md)，包含准确源码位置、触发场景、最小优化建议、设计检查及证据边界。本次交付审查记录与证据索引，后续修复由用户选择优先级。

集成探针在 Core 1.4.0 下构建通过，0 警告、0 错误。首次过早准备手牌触发断言，稳定出牌阶段重置后通过。完成 9.1 秒有声正常出牌录像并关闭本次测试游戏。测试范围为目标覆盖的一次定向复现。

工作树为 `../worktrees/STS2_ShoujoKagekiJunna/arrow-ui-review-20261110`，任务分支为 `codex/arrow-ui-review-20261110`，目标为启动时捕获的 main。原检出起始状态干净。

全部受 Git 管理及将加入 Git 的文本文档均检查凭据与本机绝对路径。录像、截图和状态位于工作树 `out/tmp/arrow-ui-review/`，已列入合入前的交付保留计划。

审查记录合为一个提交，同步 main 后等待合入及清理确认。清理项为本任务工作树、任务分支、工作树内隔离游戏和临时文件。先把所需视频、截图、状态及日志保存在主仓库 `artifacts/task-deliverables/arrow-ui-review-20261110/`。

## 首轮完整修复结果

用户确认的全部项目已实现并完成定向验证。

### 实现

- `ArrowDeployment` 提供独立显示目标。群星闪耀与攻击命令共用扩展规则，借助已有能力扫描的 attack-only 模式区分攻击命令和直接扣血。原始目标、结算排序及单体附加效果保持原有行为。
- 预览按物理箭矢缓存，标准画布每页最多六张，卡牌缩放 0.8，缓存最多十二张。支持滚轮、原生分页按钮、PageUp/PageDown 和 Esc。移出后保留缓存，箭矢移除时回收对应卡面。
- 稳定布局增加纵向安全边界，超量使用 `+数量` 入口查看隐藏箭矢。保留阵营、群体与单体分区、顺序和删除后的稳定槽位。
- 导线按密度降噪，突出当前预览页和相关目标。倒计时字号从 14 调整为 20，增加深色底牌，连发倍率为次级。
- 单支及不超过三支的批次保留原有呈现。较大批次首支正常起手，后续压缩起手与飞行，并同步命中时刻。牌效继续依次执行。

### 定向验证

- Beta、Stable 角色程序集和已有集成测试模组构建均为 0 警告、0 错误。
- `tests/ArrowLayout` 原有最少位移、分区、密集布局检查及新增纵向容量检查通过。
- `junna_arrow_ui check` 的三十项断言全部通过，覆盖动态增加与移除群星闪耀、其他敌人悬停、真实伤害、外部直接伤害卡转箭、六张分页、十二张缓存、节点复用、鼠标／键盘交互、导线层级、友方一百支容量、三个特效档位和回收。
- `junna_arrow_pivot check` 十二项通过，包括显式等待部署交接、旋转中心、数字直立、目标死亡改向与释放交接。探针曾在部署尚未完成时手动推进 HUD，现已用交接状态和五秒超时替代该时序假设。
- 原性能探针的顺序、目标点、实例矩阵、关闭特效显示、档位恢复及五十支箭矢回收检查全部通过。
- 离屏测试先验证指针命中分页按钮，再向原生控件派发按下／释放回调。最小化窗口在合成滚轮后保留 handled 状态，直接视口注入会跳过 GUI 分发。临时事件追踪已从源码清理。

### 性能与录像

相同五十支群体箭矢、空牌效基准中，顺序完成耗时从 18331.2985 毫秒降至 2817.204 毫秒，下降约 84.6%。此测量用于比较呈现等待，实际伤害及附加效果的执行时间随各牌效果计算。

五十支群体箭矢、四目标、Full 特效下采样三百六十帧。平均帧间隔 16.6664 毫秒，P95 为 17.1235 毫秒，最大 18.6845 毫秒。HUD 更新平均 0.2452 毫秒，导线绘制平均 0.0811 毫秒，导线绘制托管分配为 0 B/帧。

- 正常出牌有声录像为 13.93 秒，包含等待中的箭矢随能力改变目标范围，四名敌人最终各承受 74 点伤害。
- 密集场景有声录像为 29.33 秒，包含六张可读分页、按钮及键盘翻页、超量入口与五十支真实满弦射击。指定敌人承受 1250 点伤害，结束时部署数、可见箭矢数和预览缓存数均为零。
- 两段录像均已完成封装，包含 H.264 视频与 AAC 游戏内部混音。随机端口、最小化与静默启动，焦点采样均为 0/120，最终运行日志无错误或异常。测试游戏已关闭。
- 交付证据位于工作树 `out/tmp/arrow-ui-refactor/`，包含 `presentation-results.json`、`pivot-results.json`、`batch-comparison.json`、`final-performance.json`、`recording-results.json`、两段 MP4、截图及帧拼接图。原始审查证据保留在 `out/tmp/arrow-ui-review/`。

### 提交与集成

审查和本轮重构已合为任务提交 `f61ebcf506d00d4c3a7d5e11cbe72041f4f417d9`。同步本地 main 的 `4f1f807b474eb9c75dc5db66dcd4d518f726fbeb`，保留并行任务的知识提示修复，未发生冲突。

同步后重新构建 Beta、Stable 及集成探针，均为 0 警告、0 错误。布局定向回归、三十项展示断言和十二项旋转断言再次全部通过，最终运行日志无错误或异常，测试游戏已关闭。新增证据为 `synchronized-presentation-results.json`、`synchronized-pivot-results.json` 与 `synchronized-assembly-hashes.json`。录像对应同步前已验证的相同箭矢实现。

任务分支为 `codex/arrow-ui-review-20261110`，原检出仍为 main，原有未提交状态为空且保持不变。已完成本地同步，等待用户确认合入及清理。

清理前将两轮录像、截图、结果、日志、启动检查及已构建程序集保留到主仓库 `artifacts/task-deliverables/arrow-ui-review-20261110/`。清理范围为本任务工作树、任务分支、工作树内隔离游戏和临时文件。

## 悬停排列修正

用户指出六张卡牌和底板的排列不符合游戏 UI，继续在原工作树修正。参考游戏 `NPlayerHand`、`HandPosHelper` 与原生卡牌悬停，使用透明的单排遮叠卡组，当前牌完整抬起，邻牌缩小并轻微倾斜。滚轮及方向键逐张浏览，序号随主牌显示。保留物理卡牌缓存和既有箭矢机制，重点检查战斗主体、意图和手牌避让。当前合入仍待确认。

### 修正实现与验证

- 移除六卡网格、底板和分页工具条。复用 `HandPosHelper` 的原生手牌位置和角度，最多五张遮叠展开。当前牌缩放 0.9 并保持直立，邻牌缩放 0.702，焦点用 0.12 秒缓动抬起，序号贴在主牌下方。
- 悬停邻牌直接聚焦。滚轮、左右方向键和 PageUp/PageDown 逐张切换，Esc 关闭。保留目标范围、物理牌实例、箭矢顺序及超量入口，缓存最多十张。
- 根据所有焦点姿态的共同边界固定展开区域，为存活角色及敌人意图、顶部状态和底部手牌留出空间。透明根控件承接卡牌间隙的输入，预览根及焦点卡的兄弟节点顺序与视觉层级一致，修复后部署箭矢抢占悬停的缺陷。
- `junna_arrow_ui check` 三十三项全部通过，新增邻牌抬起后焦点与外框稳定、滚轮停留于展开区、五十张全部可达和十张缓存上限检查。原目标覆盖、导线、特效档位、超量与回收检查继续通过。探针经引擎输入队列派发，并有界等待焦点动画完成。
- Beta 与 Stable 角色程序集及集成探针构建通过，均为零警告、零错误。临时逐帧追踪已清理。
- 新录制正常出牌与悬停视频 13.97 秒，包含单张、三张异种箭矢的邻牌抬起、四张箭矢的滚轮和键盘浏览。密集悬停视频 5.77 秒，包含五十张中的连续浏览、超量入口及 Esc。
- 新证据位于 `out/tmp/arrow-hover-native/`，包括 `presentation-results.json`、`recording-results.json`、`normal/` 与 `dense/` 下的有声 MP4、`single.png`、`three-focus.png`、`four-focus.png`、`dense-focus.png`、`overflow.png` 和两张录像拼接图。首轮网格录像作为历史对照保留。

本轮修正已与既有箭矢修复重新整理。同步本地 main 至 `eb020c1311fa90e366a1bd1bec369bdb1dc9340d`，包含工作台 r4816 及知识连线。悬停文件出现两处同一声明区的冲突，保留原生手牌式预览与 main 新增 `CardAnchors` 只读接口，未改变知识连线的查询语义。

同步后从 `build.ps1` 正常入口重新构建 Beta、Stable、加载器及资源包，均通过。集成探针零警告、零错误，三十三项箭矢展示断言再次全部通过。额外正常打出星坠，验证箭矢预览与一条知识关联同时显示、连线几何通过、移出后清零。最终启动焦点采样为零／一百二十，游戏日志无错误或异常，测试游戏已关闭。

最终证据补充 `final-build.log`、`final-presentation-results.json`、`knowledge-coexistence.json`、`knowledge-coexistence.png`、程序集及资源哈希和最终运行日志。新悬停录像对应同步前的相同预览实现，首轮网格录像保留为历史对照。

原检出的四项工作台未提交改动保留，合入与清理继续等待用户确认。清理前将三轮录像、截图、检查结果、日志及构建包保留到主仓库 `artifacts/task-deliverables/arrow-ui-review-20261110/`。清理范围为本任务工作树、任务分支、工作树内隔离游戏和临时文件。
