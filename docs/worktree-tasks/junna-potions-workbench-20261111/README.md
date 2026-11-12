# 职业药水工作台设计

## 原始用户输入

### 消息 1：工作区约定

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

# 卡牌入堆位置

- 生成或复制到抽牌堆时必须显式指定位置。未明确置顶或置底的生成、复制及知识附带发牌使用 `CardPilePosition.Random`，复用原生生成事件与同步随机数。定向抽取先置顶再走正常抽牌，永久牌组发放按牌组规则处理。修改时按[卡牌入堆位置约定](docs/implementation/card-pile-placement.md)核对位置、数量、版本、原牌序及实际入口，保留同类回归结果。

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

<environment_context>
  <cwd><repository-root></cwd>
  <shell>powershell</shell>
  <current_date>2026-11-11</current_date>
  <timezone>Asia/Shanghai</timezone>
  <filesystem><workspace_roots><root><repository-root></root><root><karen-repository></root><root><card-research-repository></root><root><nana-godot-project></root><root><sts2-mcp-repository></root><root><core-repository></root><root><session-visualizations-directory></root></workspace_roots><permission_profile type="managed"><file_system type="restricted"><entry access="read"><special>:root</special></entry><entry access="write"><path><repository-root></path></entry><entry access="write"><path><karen-repository></path></entry><entry access="write"><path><card-research-repository></path></entry><entry access="write"><path><nana-godot-project></path></entry><entry access="write"><path><sts2-mcp-repository></path></entry><entry access="write"><path><core-repository></path></entry><entry access="write"><path><session-visualizations-directory></path></entry><entry access="write"><special>:slash_tmp</special></entry><entry access="write"><special>:tmpdir</special></entry><entry access="read"><path><repository-root>/.git</path></entry><entry access="read"><path><karen-repository>/.git</path></entry><entry access="read"><path><card-research-repository>/.git</path></entry><entry access="read"><path><nana-godot-project>/.git</path></entry><entry access="read"><path><sts2-mcp-repository>/.git</path></entry><entry access="read"><path><core-repository>/.git</path></entry><entry access="read"><path><session-visualizations-directory>/.git</path></entry><entry access="read"><path><repository-root>/.agents</path></entry><entry access="read"><path><karen-repository>/.agents</path></entry><entry access="read"><path><card-research-repository>/.agents</path></entry><entry access="read"><path><nana-godot-project>/.agents</path></entry><entry access="read"><path><sts2-mcp-repository>/.agents</path></entry><entry access="read"><path><core-repository>/.agents</path></entry><entry access="read"><path><session-visualizations-directory>/.agents</path></entry><entry access="read"><path><repository-root>/.codex</path></entry><entry access="read"><path><karen-repository>/.codex</path></entry><entry access="read"><path><card-research-repository>/.codex</path></entry><entry access="read"><path><nana-godot-project>/.codex</path></entry><entry access="read"><path><sts2-mcp-repository>/.codex</path></entry><entry access="read"><path><core-repository>/.codex</path></entry><entry access="read"><path><session-visualizations-directory>/.codex</path></entry><entry access="read"><path><repository-root>/.aws</path></entry><entry access="read"><path><karen-repository>/.aws</path></entry><entry access="read"><path><card-research-repository>/.aws</path></entry><entry access="read"><path><nana-godot-project>/.aws</path></entry><entry access="read"><path><sts2-mcp-repository>/.aws</path></entry><entry access="read"><path><core-repository>/.aws</path></entry><entry access="read"><path><session-visualizations-directory>/.aws</path></entry></file_system></permission_profile></filesystem>
</environment_context>

### 消息 2：任务

$worktree-task  新增药水到工作台 普通职业药水 从3组随机知识牌中选择1组加入你的手牌。这些牌在本回合可以免费打出。 罕见药水 所有手牌获得9点伤害加成。 稀有药水 打出你的所有箭矢。

### 消息 3：技能内容

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

### 消息 4：补充要求

你需要整理下游戏本体中药水的命名特点，和卡图的特点

### 消息 5：继续命名与绘图

根据这些特点你来为我的药水命名，绘制药水图
### 消息 6：对照本体重绘

重新比对本体药水icon，重绘下
### 消息 7：检查工作台入口

工作台上怎么看不到

### 消息 8：并排查看对比

和本体的药水放一起我看看对比下

### 消息 9：重绘两瓶药水

运动饮料和瓶装剑雨重绘

### 消息 10：药水页签与卡池分类

工作台中需要创建一个单独的药水页签，你将备用页签和垃圾桶页签整合进入卡池页签，左侧新增一个筛选分类，并且默认用卡池的页签分类

### 消息 11：先确定SVG瓶形

图片还是不太对，你先用svg设置瓶子的大致形状

### 消息 12：修改稀有药水名称

也修改下瓶装箭矢这个名字

### 消息 13：明确批准线上发布

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_47e291120a02414c9d34076454943102\",0]","question":"是否批准将已在本地验证的「独立药水页签、卡池内的备用/垃圾桶分类、默认卡池」发布到线上工作台，并重启共享的 junna-workbench.service？服务会短暂中断，上一版本会保留以便回退。自动审批因缺少明确的生产部署批准而拦截了这一步。","answer":"批准发布并重启工作台服务"}]
</send_user_message_question_reply>

### 消息 14：箭头形瓶身

箭矢药剂你看看能不能做一个箭头形状的瓶子

### 消息 15：参照悬挂箭矢

你参考游戏内悬挂的箭头的形状

### 消息 16：按确认瓶形制作药水图

可以，你重新根据形状来制作药水图吧

### 消息 17：全面分析本体药水后设计

查看所有官方药水，看看官方是怎么一个设计药水图标的思路。具体从形状、颜色、装饰元素入手。得出官方的图标设计思路。然后你再来设计。

### 消息 18：倾斜角度

来个倾斜角度

### 消息 19：再次改名

再换个名字

### 消息 20：学习本体风格并柔化造型

太尖锐了，你来学习药水的风格用imagegen生成

### 消息 21：从零生成

完整从0生成，不要修改

### 消息 22：纠正圆角方向

不太对，你看看狡诈药水也有尖尖，我的意思不是润滑

### 消息 23：要求实体药水感

是不要像ui，要求更像是实体药水

### 消息 24：瓶口透视

瓶口怎么斜了

### 消息 25：工作台故障

工作台怎么挂了

### 消息 26：故障位于手机MCP

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_cd23adb824294ab6a472183562ec97e0\",0]","question":"目前服务和页面访问正常。你刚才遇到的是哪种情况？如果是某个操作后卡住，也可以直接写下操作。","answer":"手机mcp无连接"}]
</send_user_message_question_reply>

### 消息 27：先正立绘制再倾斜

你这个药水还是不对，你先画正立着的，再斜过来

### 消息 28：运动饮料从零重做并全面复核本体图标

运动饮料完全重做。查看所有官方药水，看看官方是怎么一个设计药水图标的思路。具体从形状、颜色、装饰元素入手。得出官方的图标设计思路。然后你再来设计。

## 简要计划

1. 保留 main 的原有未提交修改，从起始提交创建隔离工作树。
2. 读取工作台当前风格规则、知识关系和药水相关条目；核对本体药水中文名称、职业池和实际图标。
3. 通过 MCP 在契合池「职业药水」主题新增三瓶药水设计，保留用户原效果；将本体命名和图标规律写入风格约定。
4. 定向回读三条设计、规则和公开快照，记录请求与版本证据；只验证本次内容修改。
5. 按补充要求为三瓶药水命名，使用内置 imagegen 绘制独立透明药水图标；原尺寸运行本地 Artifact Cleaner，保留原图、提示词及处理参数，检查轮廓、瓶内物和小尺寸可读性。通过 MCP 同步药水名称与配图。按最新要求重新逐张对照本体实际药水图标，保留首版并重绘三张，调整色块、玻璃轮廓、箭矢造型及笔触，再完成同样的清理和同步。
6. 单独重绘运动饮料与瓶装箭雨；新增独立药水页签，将备用和垃圾桶并入卡池左侧分类，默认卡池，保留旧链接兼容。完成定向验证并部署工作台。
7. 逐张分析全部本体药水的形状、颜色和装饰；以已确认的悬挂箭矢瓶形重绘齐射药剂，清理、核对并通过MCP发布。
8. 按补充要求将菱形药水顺时针倾斜约25度并改名箭雨药水，清理图片、同步名称引用并发布。
9. 按最新澄清从零生成实体药水图：尖角可以保留，重点是玻璃壁厚、瓶口、侧面及液面。
10. 按瓶口反馈从零重生成，统一瓶口、瓶颈和木塞轴线以及瓶沿与木塞顶面的透视。
11. 按最新要求先生成正立图，核对水平瓶口和垂直轴线，再整体旋转25度。
12. 重新检查全部63张本体药水，从形状、颜色和装饰归纳设计思路，再从零重做运动饮料。
13. 更新任务提交，同步本地 main，申请合并及清理确认。

## 起始状态

- 原分支：main。
- 原提交：ce63ec98f4fa1cf99e48a09c0356ef944637ca96。
- 任务分支：codex/junna-potions-workbench-20261111。
- 工作树：../worktrees/STS2_ShoujoKagekiJunna/junna-potions-workbench-20261111。
- 原工作区有未提交的角色选择画面和工作台界面修改，其路径与 SHA-256 记录于 original-state.json；这些修改留在原工作区。

## 结果与变更

最新状态：运动饮料已在重新检查全部63张本体图标后从零重做并以v10发布；箭雨药水v9和导航保持已发布。手机MCP问题的只读检查正常，具体入口和报错待补充。Git合并待处理。

- 工作台：新增三瓶职业药水、本体命名与图标参考；r4893替换运动饮料与稀有药水的v3图标，r4894将稀有药水改名为“齐射药剂”。知识药水沿用v2；三条效果和9点数值保持用户原文。
- 最新SVG草图位于docs/design/assets/potions/2026-11-11/shape-drafts/。运动饮料采用长瓶身、短颈和旋盖；齐射药剂按游戏悬挂箭矢的上短下长菱形重画，瓶口位于上端。源SVG、渲染原图、清理记录和并排预览均保留。齐射药剂已据此生成v4成品；运动饮料保留SVG形状草图和v3工作台图片。
- 源码：新增potion-view.js；药水作为独立页签展示与编辑，沿用fitPool条目和既有MCP版本／历史。契合池排除药水条目，旧“契合池→职业药水”链接转到药水页。
- 导航：备用、垃圾桶从顶栏移除，统一进入卡池左侧“分类”。默认进入卡池；各分类保留搜索、筛选、排序、垃圾桶分页与恢复行为；旧reserve／trash链接兼容。
- 验证：10条导航状态测试通过；定向药水搜索、稀有度编辑检查通过；本地浏览器检查新增、编辑和删除临时预览药水，并清理到原有3瓶；390像素手机视图无横向溢出，筛选按钮和弹窗正常。
- 线上发布首次被自动审批拒绝，用户随后明确批准发布并重启服务。获批后复制当前运行版本，仅替换前端及静态资产，保留后端、数据库、私有配置和上一发布版本。发布包保留线上已有的整组备用修复；主目录该并行补丁未纳入本任务提交。脚本末尾换行报错已核实，服务与页面均正常，未重复切换发布。
- 公开页与编辑页已核对新药水页签和顶栏精简；公开浏览器确认卡池默认分类、备用和垃圾桶均保持在卡池页签中。最终公开页为https://junna.17181718.xyz/#tab=potions，证据见deployment-verification.json。
- 本体63种药水的中文名称、图像摘要、职业池与命名统计见native-potion-study.md与native-potions.json；所有生成原图、提示词和本地处理参数保留在药水素材目录。SVG的PNG预览同样完成本地清理，alpha与深色轮廓保持一致，箭头尖端和倒钩未裁切。
- Impeccable机械检查发现shell.html中既有data-asset图标的静态空src提示；该图标由运行时代入，浏览器实际显示正常。
- 最新分析：全部63张本体药水按256×256原尺寸逐项核对形状、配色和装饰，63条观察、3张原尺寸接触表及设计归纳见native-potion-icon-design.md与native-potion-icon-observations.json。结论是由一个主要识别点组织图标，主题可进入瓶形、内容物、瓶塞或容器本身；等级不机械决定颜色或装饰多少。
- v4成品：齐射药剂使用上短下长的菱形玻璃瓶、上端木塞、紫色药液、少量气泡和宽高光，不再放入三支实体箭。内置image_gen输出及完整提示词保留在docs/design/assets/potions/2026-11-11/v4/；本地Artifact Cleaner在原尺寸清理，透明通道和深色轮廓保持一致，32／64像素与本体同尺寸对照均已检查。处理脚本兼容alpha≥250的近不透明内部像素；首稿alpha最大为254，清理后可见RGB平均变化0.649。
- MCP r4952同时保存齐射药剂v4配图及更新后的本体药水设计规律；名称与牌效保持不变。公开快照逐字段与MCP一致，线上PNG与本地256×256成品同SHA-256；全部63张参考源图摘要复查一致，见public-art-v4-verification.json。
- v5成品：按“来个倾斜角度”用内置image_gen编辑v4，整体顺时针倾斜约25度，瓶口右上、长尖端左下，保留菱形轮廓、木塞、紫色药液和气泡。名称按“再换个名字”改为箭雨药水，相关风格引用同步更新；牌效不变。原图、提示词与本地清理记录保存在docs/design/assets/potions/2026-11-11/v5/。MCP r4982与公开快照名称、效果、图片一致，线上PNG与本地成品同摘要；原尺寸、透明轮廓和32／64像素均已定向检查，见public-art-v5-verification.json。
- 最新v7：根据用户“完整从0生成，不要修改”及“是不要像ui，要求更像是实体药水”的澄清，从零调用内置image_gen，未提供任何输入图片。保留箭头式尖角，用侧壁、厚玻璃底、圆筒瓶颈、环形瓶沿、嵌入木塞、水平液面与空腔表现实体容器。v6圆角方向未发布，未提交请求保留为workbench-art-v6-rejected-draft.json，原图与清理记录位于素材v6/。v7原图、完整提示词、本地Artifact Cleaner记录与本体同尺寸对照保留在素材v7/；原尺寸、alpha与深色轮廓、32／64像素均已检查。MCP r4983保存新配图及设计规则，名称与牌效不变；公开快照与MCP一致，线上256×256 PNG与本地成品同SHA-256，见public-art-v7-verification.json。
- v8：根据“瓶口怎么斜了”的反馈从零重生成，瓶口、瓶颈与木塞保持同轴，瓶沿与木塞顶面透视一致，保留整瓶倾斜和实体玻璃结构。未传入旧图；原图、完整提示词、清理参数及检查图位于素材v8/。MCP r4984保存图片与同轴透视规则，名称与牌效不变；公开快照逐字段与MCP一致，线上PNG与本地256×256成品同SHA-256；原尺寸透明轮廓及32／64像素均已检查，见public-art-v8-verification.json。
- v9：按“你这个药水还是不对，你先画正立着的，再斜过来”先用内置image_gen从零生成正立药瓶；Artifact Cleaner在原尺寸清理后，用System.Drawing GDI+对同一张图整体顺时针旋转25度，倾斜阶段未再生成。矩阵已核对为纯旋转，无缩放或剪切，正立源文件未变，旋转尖端未裁切；原图、清理成品、旋转矩阵、正立与倾斜对照、32／64像素检查位于素材v9/，旋转脚本为rotate-potion.ps1。MCP r4998保存成品与绘制顺序约定；名称与牌效不变；公开快照与MCP一致，线上PNG与本地256×256成品同摘要，见public-art-v9-verification.json。
- 手机MCP诊断：公开页、编辑页和快照均返回200，服务处于running且未自动重启；SDK初始化与读条目正常。线上网页实际连接模块在缺少AbortSignal.timeout的环境下通过检查；以手机请求头执行初始化和快照同步也成功。尚未复现用户手机故障，具体是网页顶部提示还是ChatGPT手机MCP连接仍待补充；未因诊断改动服务或数据，见mobile-mcp-diagnosis.json。
- 运动饮料v10：再次逐张查看本体63张图标并复核全部源文件摘要，从瓶形、颜色、装饰及实体结构归纳设计方法，结果见sports-drink-redesign.md。内置image_gen未输入旧图，从零绘制收腰运动瓶、蓝绿色旋盖和推拉壶嘴、橙金饮料；正立稿完成本地清理后整体向左倾斜18度。原图、提示词、旋转矩阵及本体并排图位于素材v10/；透明轮廓、纯旋转、画布边界和32／64像素均已核对。MCP r4999更新配图和对应设计约定，名称与9点效果不变；公开快照与MCP一致，线上PNG与本地成品同SHA-256，见public-art-v10-verification.json。旋转脚本-Key参数保留原箭雨药水默认行为。
- 本任务未写入原工作区；最终复核期间并行任务更新了其未提交文件，变化摘要已记录于synchronization-state.json。main为dc66cd5，其29个未提交文件观察基线见synchronization-state.json。app.js等包含并行未提交修改。本任务分支和工作树保留，Git合并与清理尚未执行。

本轮提交前已核对完整任务输入，并审阅全部461份已跟踪及待加入文档（含SVG），未检出本地绝对路径或常见凭据格式。

悬挂箭矢参照：ArrowHud实际加载ShoujoKagekiJunna/images/floating_arrow_c1.svg，原资源为240×416，上短下长的菱形。新瓶形以0.44等比缩放主体轮廓，上端接瓶颈和瓶塞，保留原轮廓、资源摘要、SVG与并排对照，位于docs/design/assets/potions/2026-11-11/shape-drafts/hanging-arrow-v3/。SVG、PNG透明边界和尖端画布范围均已核对。

同步结果：本轮main已推进至82f1169，已在任务工作树无冲突合入该分支的其他任务提交。同步未改变docs/design/workbench/或本次v9素材；v9成品SHA-256复核一致，定向图像验证结果保持有效。原main未合入本任务，本任务工作树继续保留。

本轮同步与整理：main推进至dc66cd5后已无冲突合入任务工作树，同步未改变docs/design/workbench/；运动饮料v10图片摘要复核一致。任务分支自身变更整理为一条提交，以本轮已同步的main为父提交；只重写本任务私有分支，原main及其未提交文件不写入。工作树继续保留，尚未合并回main。
