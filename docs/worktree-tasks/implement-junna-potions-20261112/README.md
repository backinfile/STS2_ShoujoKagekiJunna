# 三瓶纯那职业药水的运行实现

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


### 消息 29：确认运动饮料

可以

### 消息 30：本次工作区约定

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
  <current_date>2026-11-12</current_date>
  <timezone>Asia/Shanghai</timezone>
  <filesystem><workspace_roots><root><repository-root></root><root><karen-repository></root><root><card-research-repository></root><root><nana-godot-project></root><root><sts2-mcp-repository></root><root><core-repository></root><root><session-visualizations-directory></root></workspace_roots><permission_profile type="managed"><file_system type="restricted"><entry access="read"><special>:root</special></entry><entry access="write"><path><repository-root></path></entry><entry access="write"><path><karen-repository></path></entry><entry access="write"><path><card-research-repository></path></entry><entry access="write"><path><nana-godot-project></path></entry><entry access="write"><path><sts2-mcp-repository></path></entry><entry access="write"><path><core-repository></path></entry><entry access="write"><path><session-visualizations-directory></path></entry><entry access="write"><special>:slash_tmp</special></entry><entry access="write"><special>:tmpdir</special></entry><entry access="read"><path><repository-root>/.git</path></entry><entry access="read"><path><karen-repository>/.git</path></entry><entry access="read"><path><card-research-repository>/.git</path></entry><entry access="read"><path><nana-godot-project>/.git</path></entry><entry access="read"><path><sts2-mcp-repository>/.git</path></entry><entry access="read"><path><core-repository>/.git</path></entry><entry access="read"><path><session-visualizations-directory>/.git</path></entry><entry access="read"><path><repository-root>/.agents</path></entry><entry access="read"><path><karen-repository>/.agents</path></entry><entry access="read"><path><card-research-repository>/.agents</path></entry><entry access="read"><path><nana-godot-project>/.agents</path></entry><entry access="read"><path><sts2-mcp-repository>/.agents</path></entry><entry access="read"><path><core-repository>/.agents</path></entry><entry access="read"><path><session-visualizations-directory>/.agents</path></entry><entry access="read"><path><repository-root>/.codex</path></entry><entry access="read"><path><karen-repository>/.codex</path></entry><entry access="read"><path><card-research-repository>/.codex</path></entry><entry access="read"><path><nana-godot-project>/.codex</path></entry><entry access="read"><path><sts2-mcp-repository>/.codex</path></entry><entry access="read"><path><core-repository>/.codex</path></entry><entry access="read"><path><session-visualizations-directory>/.codex</path></entry><entry access="read"><path><repository-root>/.aws</path></entry><entry access="read"><path><karen-repository>/.aws</path></entry><entry access="read"><path><card-research-repository>/.aws</path></entry><entry access="read"><path><nana-godot-project>/.aws</path></entry><entry access="read"><path><sts2-mcp-repository>/.aws</path></entry><entry access="read"><path><core-repository>/.aws</path></entry><entry access="read"><path><session-visualizations-directory>/.aws</path></entry></file_system></permission_profile></filesystem>
</environment_context>

### 消息 31：实现药水

[$worktree-task]($worktree-task)  在mod中实现上这些药水吧

### 消息 32：显式调用的技能完整内容

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

### 消息 33：知识药水的入手与确认流程

**知识药水**  是加入手牌，而且不需要二次确认

### 消息 34：知识药水录像反馈

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_b198e06cb64a44f6a3d6485e7fc93294\",0]","question":"是否继续将修正后的药水提交3dfb1b9同步到最新main（当前2448cfe），重新打包后合并？选择清理时，会先保留演示、构建包和验证材料，再删除本任务工作树、任务分支及临时测试文件。","answer":"知识药水录制有问题"}]
</send_user_message_question_reply>

### 消息 35：录像清晰度

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_07e97bf5a5754c3cad5acf18ea514a6a\",0]","question":"知识药水录屏具体是哪一处有问题？","answer":"画面模糊"}]
</send_user_message_question_reply>

### 消息 36：复用已有堆叠UI

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_4707ed72fd1742e58bedf5da97eaf595\",0]","question":"知识药水的单次点选和1080p录像均已修正。是否继续将提交12d5a18同步到最新main、更新构建包并合并？清理前会保留高清录像、截图、构建包和验证材料。","answer":"ui不对啊，你这堆叠不是正常已经有的堆叠ui，是你额外写的？"}]
</send_user_message_question_reply>

### 消息 37：批准继续同步、合并及清理

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_003165447374429fa1e18609fbd07fe0\",0]","question":"现已复用已有知识组堆叠，点击后直接整组入手。是否继续将提交d718409同步到最新main、更新构建包并合并？清理前会保存新版1080p录像、截图、构建包和验证材料。","answer":"继续同步、合并并清理（推荐）"}]
</send_user_message_question_reply>

## 简要计划

1. 保留main的并行修改，新建隔离工作树。
2. 通过MCP读取三条药水最新设计与图片，核对本体药水接口及职业药水池规则。
3. 实现知识药水、运动饮料、箭雨药水，接入职业池、资源与本地化。
4. 运行必要的编译和定向游戏检查，以随机端口、最小化、静音、满解锁启动测试并录制合法操作视频。
5. 审查文档与用户输入，整理单次提交，同步main后申请合并及清理确认。

## 起始状态

- 原分支：main。
- 原提交：dc66cd5f323724a62a48b422dbc647212edd83b1。
- 任务分支：codex/implement-junna-potions-20261112。
- 工作树：../worktrees/STS2_ShoujoKagekiJunna/implement-junna-potions-20261112。
- 原工作区29个未提交文件的摘要见original-state.json，均保留在原工作区。

## 当前结果

三瓶运行实现、复用既有知识组堆叠和高清录像均已完成；用户已批准继续同步、合并及清理。最新main同步与构建完成，集成和清理结果在交付时报告。

- 新增纯那职业药水池，替换角色此前复用的静默药水池。知识药水为普通、运动饮料为罕见、箭雨药水为稀有；三瓶均限战斗使用。
- 知识药水通过已有三选一界面展示3组不同知识牌，复用已有知识组堆叠，点击组入口后整组加入手牌。按本局静态及动态关系保留成员、数量；所选整组本回合免费，满手牌按原生规则进入弃牌堆。未选卡牌清理出战斗。
- 运动饮料复用伤害加成机制，对手牌中可造成伤害的牌增加9点；其他牌堆和已布置箭矢不受影响，加成按现有规则在本场战斗保留。
- 箭雨药水对使用时各牌堆和已布置的实际箭矢建立一次快照，自动选择有效目标并免费各打出一次。保留原有倒计时和布置行为，新生成箭矢不递归加入本次快照。
- 三瓶采用工作台 revision 5000 的定稿图；知识、运动饮料、箭雨对应 v2、v10、v9。原图哈希、资源路径及处理记录见 assets.json，设计读取版本见 workbench-designs.json。
- 主要代码：src/Models/Potions.cs、src/Models/Junna.cs、src/Combat/Cards/CardActions.cs。本地化：ShoujoKagekiJunna/localization/zhs/potions.json；图标与轮廓：ShoujoKagekiJunna/images/potions/。
- 改动前 Beta 编译通过。同步前 Beta、Stable、加载器编译及 Godot 导入、打包通过，无编译警告或错误。使用 Core SDK 1.6.0，Stable 引用 API 0.107.1。
- 5个定向游戏用例共74项当前断言通过，覆盖职业池和稀有度、整组数量、免费及到期、满手牌、动态关系去重、9点加成范围和保留、各牌堆及已布置箭矢各打出一次、药水消耗及能量。详情见 verification.json。未运行全面测试。
- 测试使用独立档案、随机端口、最小化及静音模式，并执行 unlock all；录制中通过正常药水、选牌、出牌和结束回合操作继续游戏。三瓶演示为 out/tmp/implement-junna-potions-20261112/junna-potions-demo.mp4。
- 测试准备阶段出现过牌面显示不同步，已让手牌添加、移除使用原生显示流程；费用测试排除了会自行给予其他牌永久免费的卡牌，避免将其效果误判为药水超时。最终上述用例全部通过。
- 原目录起始29个未提交文件未由本任务修改。执行中观察到角色选择画面的3个文件继续被并行任务修改；合并前以原目录最新内容再次核对，保留这些修改。
- 提交前同步前已审查294份已跟踪及新增文档，未发现本地绝对路径、凭据模式或当前工作台私有连接配置的精确匹配。
- 任务提交、main同步及合并结果在交付时报告；本次工作树和分支在收到合并、清理确认前保留。

## 同步 main 的结果

- 在任务提交7774d1f之后，main已推进至c9cdee1，包含并行角色选择画面、药水工作台归档与箭矢悬停调整；已合入任务分支，无冲突。
- 同步带入的运行源码仅涉及src/UI/Arrows/ArrowHud.Hover.cs，药水逻辑不变。同步后Beta、Stable、加载器编译和Godot导入、打包均通过；main的箭矢悬停代码在第234行有一条既有CS8602警告，两个API构建各报告一次。
- 原目录当前仍有3个并行修改文件，清单及同步时哈希见synchronization-state.json。任务提交不修改这3个路径，合并前后再次核对。

- 同步后复审409份文档，将新合入的两份历史任务记录中的部署绝对路径替换为与项目约定一致的占位符；未发现凭据或私有连接配置泄露。

- 随后再次同步main的a23e76c归档收尾提交；此次仅历史任务README变化，无运行代码或资源变化，沿用同步后构建及药水定向验证结果。

## 知识药水的一次点击入手

- 用户指出原生整组预览仍需二次确认。药水数据本已加入手牌，但原生确认动画会飞向牌组；改用仅作用于知识药水的整组选牌入口，点击整组后立即结束选择，随后走原生生成到手牌的动画。
- 保留原生随机数和多人选择同步。其他原生整组选牌来源继续使用原流程。
- 此前的合并提案暂缓，更新针对性检查与演示后再交付确认。

- 一次点击入手已通过正常、满手牌和动态知识组3个用例：点选后选择屏幕立即结束；牌组数量不变；没有飞向牌组的动画；正常容量时所选整组均进入Hand。更新后的5个药水用例共68项当前断言通过，最初验证还已实测确认过免费打出不消耗能量。
- 同步main的a34583f空值保护后，最终Beta、Stable、加载器及资源包构建全部通过，零警告、零错误；已更新知识药水演示与构建包。
- 将本次任务分支的实现、后续修正和同步记录重新整理为一个提交，main既有历史和3个并行修改文件保持原样。

## 合并前状态

- 最后已同步的main基线为01088af；main随后推进到2448cfe，新增角色选择袖口与帷幕遮挡修正，原目录现已无未提交内容。
- 收尾核对期间main连续推进3次，按照worktree-task的“stop after three races”停止自动集成。药水代码、一次点击入手、验证及演示均已完成，当前保留任务工作树，等待明确继续同步、合并及清理确认。
- 构建包基于最后同步基线01088af及本任务药水实现；继续集成2448cfe后需更新资源包。此前的并行修改由对应任务提交到main，本任务未改写这些文件。

## 知识药水高清重录

- 按消息34–35重录知识药水：游戏真实视口和原始截图均为1920×1080，录像为1920×1080、20帧、22.6秒，含游戏音频；保留选组展示和手牌悬停的停留时间。
- 已从编码后视频提取1080p画面检查文字清晰度；提供独立原片out/tmp/implement-junna-potions-20261112/knowledge-potion-1080p.mp4和原尺寸PNG：out/tmp/implement-junna-potions-20261112/knowledge-potion-1080p.png。旧720p合成演示不再作为知识药水的交付录像。
- 高清重录中知识药水的16项当前断言再次通过。此次仅更新录像与验证记录，药水运行代码不变。
- 消息34–35为录像问题反馈；继续同步、合并及清理的确认仍待处理，工作树与分支继续保留。

## 复用既有知识组堆叠

- 按消息36删除知识药水专用的KnowledgePotionChoice界面适配及其UID。改用CardSelectCmd.FromChooseACardScreen与既有KnowledgeCardStack，直接复用其他知识牌选择时的堆叠和交互。
- 只创建3个知识组入口供选择；选择后按当前关系创建整组、设为本回合免费并加入手牌。未选入口及发放中未入堆的临时牌仍清理。
- 补充既有知识组堆叠节点与原生卡包节点的观察，按新流程更新定向检查与1080p演示。

- 复用既有堆叠的最终验证：普通知识组18项、满手牌17项、动态知识组18项，均通过；加上未改动的运动饮料13项与箭雨药水8项，共74项当前断言。普通知识组选牌时观测到3个既有KnowledgeCardStack节点和0个NCardBundle节点。
- 新版1080p原片为out/tmp/implement-junna-potions-20261112/knowledge-potion-existing-stack-1080p.mp4，原尺寸选牌截图为out/tmp/implement-junna-potions-20261112/knowledge-existing-stack-1080p.png；此前使用卡包界面的录像均为已替换版本。
- 最终Beta、Stable、加载器编译及Godot导入、打包再次通过，零警告、零错误。

## 获准集成的最终版本

- 消息37明确批准继续同步、合并并清理。已同步最新main的2448cfe，带入角色选择袖口与帷幕遮挡修正；这些变更不改动药水逻辑。
- 同步后的Beta、Stable、加载器、Godot导入和PCK打包全部通过，零警告、零错误。74项当前药水断言仍适用于未变化的药水代码；高清知识药水演示复用了已有知识组堆叠。
- 本提交用于快进集成到main。按批准范围，集成后保留录像、截图、构建包、测试结果和构建日志，再删除本次工作树、任务分支及其临时测试文件。
