# 箭矢动画提速与后续结算放行

## Original user input

### 消息 1：工作区规则

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
  <filesystem><workspace_roots><root><repository-root></root><root><karen-repository></root><root><research-repository></root><root><nana-project></root><root><mcp-repository></root><root><core-repository></root><root><visualization-output></root></workspace_roots><permission_profile type="managed"><file_system type="restricted"><entry access="read"><special>:root</special></entry><entry access="write"><path><repository-root></path></entry><entry access="write"><path><karen-repository></path></entry><entry access="write"><path><research-repository></path></entry><entry access="write"><path><nana-project></path></entry><entry access="write"><path><mcp-repository></path></entry><entry access="write"><path><core-repository></path></entry><entry access="write"><path><visualization-output></path></entry><entry access="write"><special>:slash_tmp</special></entry><entry access="write"><special>:tmpdir</special></entry><entry access="read"><path><repository-root>/.git</path></entry><entry access="read"><path><karen-repository>/.git</path></entry><entry access="read"><path><research-repository>/.git</path></entry><entry access="read"><path><nana-project>/.git</path></entry><entry access="read"><path><mcp-repository>/.git</path></entry><entry access="read"><path><core-repository>/.git</path></entry><entry access="read"><path><visualization-output>/.git</path></entry><entry access="read"><path><repository-root>/.agents</path></entry><entry access="read"><path><karen-repository>/.agents</path></entry><entry access="read"><path><research-repository>/.agents</path></entry><entry access="read"><path><nana-project>/.agents</path></entry><entry access="read"><path><mcp-repository>/.agents</path></entry><entry access="read"><path><core-repository>/.agents</path></entry><entry access="read"><path><visualization-output>/.agents</path></entry><entry access="read"><path><repository-root>/.codex</path></entry><entry access="read"><path><karen-repository>/.codex</path></entry><entry access="read"><path><research-repository>/.codex</path></entry><entry access="read"><path><nana-project>/.codex</path></entry><entry access="read"><path><mcp-repository>/.codex</path></entry><entry access="read"><path><core-repository>/.codex</path></entry><entry access="read"><path><visualization-output>/.codex</path></entry><entry access="read"><path><repository-root>/.aws</path></entry><entry access="read"><path><karen-repository>/.aws</path></entry><entry access="read"><path><research-repository>/.aws</path></entry><entry access="read"><path><nana-project>/.aws</path></entry><entry access="read"><path><mcp-repository>/.aws</path></entry><entry access="read"><path><core-repository>/.aws</path></entry><entry access="read"><path><visualization-output>/.aws</path></entry></file_system></permission_profile></filesystem>
</environment_context>

### 消息 2：任务请求

[$worktree-task]($worktree-task)  我玩了星屑这张牌，发现箭矢的打出动画太慢了，你看看怎么整体提升下速度。两个方向： 1 提升整体速度 2 在动画正在进行的过程中，放行下一张牌的结算（保证动画不出现问题即可）

### 消息 3：技能全文

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

### 消息 4：通用生成流程确认

卡牌创建出来的这一步，是游戏通用的吗？

### 消息 5：补测要求

你也测测流星，或者其他箭矢

### 消息 6：专用生成优化

你可以为流星和星屑专门优化下这个创建卡牌的动画流程吗，感觉现在有些慢了

### 消息 7：用户试玩

开一个游戏配置好状态，我来试试

### 消息 8：批注处理

请处理批注 20261111-153118-121-6a528d539a0f42f38bd8f146d2aba3c8/cbe89d20e6244943b5d3f2561b64ecdb

批注原文：

hover状态的展示的卡牌呢？

### 消息 9：星屑聚拢

你看看能不能将多张星屑聚合到一起来显示（即星屑的箭头不要分隔那么远）

### 消息 10：恢复原预览布局

？原本的预览布局不是卡牌并排在一起吗？你怎么自作主张改了？

### 消息 11：继续靠近并恢复原 hover

继续挨的更近一些（可以重叠一半） 另外你来看看原本的hover卡牌展示效果 恢复下

### 消息 12：满行换行与同状态星屑预览合并

聚合在一起后，怎么没有占满一行就开新的一行了？另外hover时展示的卡牌，允许把状态一致的星屑牌合并为一张牌显示，卡牌下方明确标注×n表示卡牌数量

### 消息 13：混合箭矢效果确认

如果有其他箭矢是什么效果呢

### 消息 14：授权合并与清理

合并，清理

## Brief task plan

- 起始检出：仓库根目录，main，ce63ec98f4fa1cf99e48a09c0356ef944637ca96。
- 主目录角色选择与工作台未提交改动原样保留，从已提交 HEAD 创建任务工作树。
- 工作树：../worktrees/STS2_ShoujoKagekiJunna/arrow-play-speed-20261111；分支：codex/arrow-play-speed-20261111。
- 复现星屑与连续箭矢的等待时间，提高整体速度并安全放行后续卡牌结算。
- 必要构建与定向实机验证，随机端口、满解锁、最小化静默运行，正常出牌录制。
- 补测流星、连射、横扫、满弦射击、自身及群体箭矢，对比原始动画与当前动画，并录制正常出牌。
- 为效果生成的流星和星屑优化原生中央展示等待，保留原生生成与出牌事件、资源、目标及顺序；通过专用计时与钩子探针验证，并重新录制。
- 复现批注中的 64 支星屑箭矢悬停牌面缺失，修复显示并检查敌人、玩家、直接悬停与清理。
- 按用户纠正恢复悬停牌面的原始排布，仅保留边界异常修复；同目标连续星屑使用相邻箭头槽位，实机复核聚拢、悬停与发射。
- 继续将纯星屑中心间距收紧至箭头宽高的一半；恢复既有 2a8bf4f 的原生横向扇形 hover、焦点抬起及滚轮浏览，并补齐缓存生命周期验证。
- 修正星屑仍按旧列距计算容量的问题，按半重叠实际间距占满一行再换行；扇形预览仅对状态一致的星屑分组，并在每张代表牌下方标注 ×n。
- 整理单个任务提交，同步 main，提交可审查结果并请求合并及清理确认。

## Result and changes

实现、必要构建及定向验证完成；用户已明确授权合并到 main，并清理本任务分支、工作树和临时测试环境。最新 main 同步、构建和布局复核均已完成；集成与清理的执行结果保存在 artifacts/task-deliverables/arrow-play-speed-20261111/integration/receipt.json。

- 生产改动包括共享运动速度、部署放行、流星和星屑的专用出生展示、恢复原生扇形预览及星屑相邻排位；涉及十一个生产文件，详见各节。
- 共享速度从 1.25 调到 2，即在当前速度上提高 60%。默认完整部署从 1.64 秒降到 1.025 秒，发射至命中从 0.352 秒降到 0.22 秒；排位与转向同步加快。
- 自定义动画的放行时点从拉弓开始前移到初始移位完成，显式等待从 0.808 秒降到 0.07 秒。普通出牌保留原生最低展示时间，效果生成的流星和星屑使用下述专用流程。
- 部署动画将自己的卡牌节点从原生出牌容器移入动画节点，独立播放和回收，后续牌堆变动不会复用或回收这张正在飞行的卡面。提前完成仍通过已有 FinishDeployment 交接到发射动画。
- 新增 tests/Integration/ArrowSpeedProbe.cs，提供 junna_arrow_speed check 定向计时与回收检查，以及录制前的可见手牌准备。
- 根因是连续生成箭矢逐张等待拉弓时点，显式等待在星屑链中累加。单独提速能缩短动画，进一步前移放行时点后，后续结算能与飞行、装箭及拉弓重叠。

### 复现与计时

使用真实引擎、Full 特效、默认游戏速度、固定首场小啃兽战斗及同一套星屑生成场景。最小反馈命令为 py -3 out/tmp/arrow-play-speed/check.py，通过 MCP 调用真实的 junna_arrow_speed check。修复前命令以 AssertionError 退出，星屑出牌、完整部署和两个子箭矢生成耗时断言失败；伤害和数量断言通过。

| 指标 | 修复前 | 仅提高共享速度 | 最终修改 |
| --- | --- | --- | --- |
| 星屑自动出牌至返回 | 1.374 秒 | 1.081 秒 | 0.871 秒 |
| 默认完整部署动画 | 1.640 秒 | 1.025 秒 | 1.025 秒 |
| 星屑命中并打出两个子箭矢 | 3.567 秒 | 2.833 秒 | 2.452 秒 |

最终场景出牌耗时减少约 37%，两个子箭矢生成链减少约 31%。自动出牌计时包含原生移牌和展示等待；手动出牌演示单独录制。

### 验证

- Beta、Stable 角色程序集及最终集成探针构建通过，0 警告、0 错误。测试资源 PCK 使用当前工作树重新构建，与代码版本一致。
- 新探针 15 项断言通过：早放行、完整时长、飞行卡面独立持有、两段子箭矢部署重叠、伤害恰好一次、生成数量、消耗、部署中提前完成与节点回收。
- 现有 junna_arrow_cadence check 的 8 项行为断言通过，另有 timing 和 complete 两条记录。覆盖后续部署重叠、提前完成及部署节点独立回收。
- 隔离测试使用随机选择的端口、满解锁存档、1280 × 720、Compatibility 渲染、最小化静默启动。启动前台采样为 0/120，silent_test 和 minimized_startup_guard 均启用。
- 正常出牌视频长 20.9 秒，1280 × 720、30 FPS、H.264 与引擎内部 AAC 音轨。录像期间只使用合法出牌和只读状态查询，覆盖星屑、满弦射击、连射和三次弦声齐鸣。总伤害 101，最终目标生命 899/1000，与牌效一致。视频抽帧人工检查、完整解码通过，最终日志无错误或异常。
- 测试运行的角色 DLL 与最终构建 SHA-256 相同。录像封装后已关闭本任务测试进程。
- 首轮必要检查共 23 项行为断言；后续按用户要求补测 82 项，详见下节。
- 已审查全部 83 份既有受版本控制的文本资料及任务记录，凭据模式和本地绝对路径扫描通过。

### 流星与其他箭矢补测（专用生成优化前）

新增 tests/Integration/ArrowVariantsProbe.cs，使用真实的 CardActions.Fly、CardActions.PaidPlay 与 ArrowCombat.Accelerate 路径，对原始版本和提速版本逐项对照。八组场景均为相同双小啃兽战斗、默认速度和 Full 特效；每组重置血量与战斗状态。以下耗时包括原生生成、移牌和自动出牌等待。

| 场景 | 原始版本 | 提速版本 | 耗时减少 |
| --- | --- | --- | --- |
| 单张流星生成与部署 | 1.436 秒 | 0.923 秒 | 35.7% |
| 普通、升级流星连续生成 | 2.853 秒 | 1.850 秒 | 35.1% |
| 连射，含生成流星 | 2.436 秒 | 1.953 秒 | 19.8% |
| 双目标横扫，含生成两张流星 | 3.852 秒 | 2.869 秒 | 25.5% |
| 满弦射击自动部署 | 1.433 秒 | 0.934 秒 | 34.8% |
| 微光自动部署 | 1.434 秒 | 0.934 秒 | 34.8% |
| 牵制自动部署 | 1.434 秒 | 0.934 秒 | 34.9% |
| 话语就是我的力量自动部署 | 1.434 秒 | 0.934 秒 | 34.9% |

- 原始版本及提速版本各 48 项断言全部通过，覆盖流星普通/升级倒计时、生成伤害、独立目标、连续动画重叠、连射和横扫的本体及流星伤害、自身格挡/能量、群体状态层数、群体攻击、部署中提前完成及节点回收。
- 另外通过现有 pivot 10 项、fly_targets 9 项、attack_pose 15 项断言，覆盖目标死亡改向、目标继承、旋转中心、姿态衔接、连续与重复结算及异常退出。
- 本次新增验证共 82 项行为断言，未发现需要追加生产修复的问题。
- 11.7 秒正常出牌视频覆盖横扫生成两张流星、满弦射击、牵制、微光、群体攻击、连射生成流星、弦声齐鸣和敌人死亡后的剩余箭矢改向。结束时剩余敌人生命 752/1000，玩家格挡 16、能量 22，部署数、部署动画与发射动画节点均为 0。录像为 1280 × 720、30 FPS、有引擎音轨；抽帧和完整解码通过，最终日志无错误或异常。
- 补测视频、原始/提速对照、回归结果、两个构建及 SHA-256 清单保存在 artifacts/task-deliverables/arrow-play-speed-20261111/variants/，清理工作树后保留。

### 同步 main 后的复核

- 同步本地 main 的 2505462，包含并行完成的知识系统调整；箭矢提速的两个生产文件未发生冲突或额外变化。
- 同步后的 Beta、Stable 及集成探针重新构建通过，0 警告、0 错误；同样八组箭矢的 48 项断言再次通过。上表为同步后的复核数据。
- 同步后的报告与已验证构建保存在 variants/variants-after-synchronized.json、variants/variants-comparison-synchronized.json 及 variants/synchronized-build/。
- 原始归档和两段录像保留，任务测试进程已在收尾后关闭。

### 流星与星屑专用生成动画优化（最终实现）

- 新增 src/UI/Arrows/GeneratedArrowPresentation.cs。两个效果生成入口继续调用原生 AddGeneratedCardToCombat，再为该张新卡建立角色附近的 45% 尺寸卡面，直接交给既有箭矢飞行、装箭及拉弓动画持有。
- 仅在该新卡的生成和自动打出作用域内，将原生 CardCmd.AutoPlay 的 skipCardPileVisuals 设为 true，跳过中央移牌、停留及最低展示等待。生成历史、生成/出牌钩子、资源处理、原生随机目标策略和串行结算顺序继续复用原入口。
- CardActions.Fly 与星屑子牌创建各改为一处专用生成调用。作用域结束会清除快速标记，并回收未被箭矢动画接管的节点；同一张流星之后重新打出也恢复普通流程。
- 直接跳过原生展示的中间方案虽然达到了耗时目标，但视觉断言捕获了卡面未建立的问题；最终方案显式建立短暂出生卡面并由现有部署动画接管，保留完整可见过程。

| 专项场景 | 本轮优化前 | 最终实现 | 耗时减少 |
| --- | --- | --- | --- |
| 流星生成并放行后续结算 | 0.863 秒 | 0.102 秒 | 88.1% |
| 星屑命中并生成两个子箭矢 | 2.449 秒 | 0.769 秒 | 68.6% |

- 连续两张流星的生成放行耗时为 0.185 秒；连射含流星为 1.120 秒，双目标横扫含两张流星为 1.201 秒。普通满弦射击、自身与群体箭矢仍约 0.93 秒。
- 新增 tests/Integration/GeneratedArrowProbe.cs。17 项专项断言通过，覆盖真实生成耗时、四类原生钩子每个子牌恰好一次、目标与倒计时、免费出牌、伤害与消耗、同实例复用后的作用域清理、手动打出的星屑、两段子动画重叠、提前完成、Reduced/Off 档位及节点回收。
- 另有八种箭矢 48 项、pivot 10 项、fly_targets 9 项、attack_pose 15 项、cadence 8 项通过；本轮共 107 项定向断言。Beta/Stable 与集成探针均构建通过，0 警告、0 错误。
- 修正既有 attack_pose 探针的受击动画结束边界判断：Hit 时长为 0.3 秒，原断言在等待 300 ms 后仍要求处于 Hit。现在同时接受自然完成信号，提前切换动画仍会失败；生产姿态逻辑未改动。
- 最终正常出牌有声录像长 9.97 秒，覆盖手动星屑、满弦射击、连射和三次弦声齐鸣，生成子卡从角色附近直接进入箭矢轨迹。目标生命由 1000 降为 899，另一个目标保持 1000；最后保留 8 支新星屑，部署动画和发射动画节点均为 0。录像期间只做合法出牌与只读状态查询，抽帧、完整解码及最终无错误日志检查通过。
- 专项视频、基线、107 项定向结果、出生阶段诊断、最终构建和 SHA-256 清单保存在 artifacts/task-deliverables/arrow-play-speed-20261111/generation/。已在封装后关闭本任务测试进程。

### 交付及原目录状态

- 视频、抽帧、计时、连发结果、启动焦点采样、录像操作记录和已验证构建已按 SHA-256 归档到原目录的 artifacts/task-deliverables/arrow-play-speed-20261111/；其中视频为 gameplay.mp4，任务工作树清理后仍保留。
- 原目录起始未提交项：角色选择图片、角色选择场景、工作台 README/app/domain/domain tests，以及角色选择脚本和 UID。任务期间角色选择场景与脚本继续发生并行更新，并新建角色选择着色器；本任务未写入这些文件。合并前将以最新逐文件哈希再次校验并保留。
- 用户继续调整后，任务改动已在保留最新 main 内容的基础上重新整理为单个任务提交。集成目标固定为起始捕获的 main。合并与工作树、任务分支及临时测试环境清理，等待用户明确确认。

### 用户试玩交接

按用户要求，以已验证构建启动可交互、有声的独立游戏窗口，随机端口、满解锁；使用本任务保留的 artifacts/arrow-speed/ 游戏及隔离存档。准备双小啃兽战斗，各 1000 生命，玩家 30 能量。八张可见手牌为星屑、满弦射击、连射、三张弦声齐鸣、横扫和应援。用户随后结束试玩并关闭窗口；准备截图和状态位于 out/tmp/arrow-play-speed/user-playtest-ready.png 与 user-playtest-state.json。


### 批注初版：密集箭矢的悬停牌面（排布调整已在后续恢复）

- 批注 20261111-153118-121-6a528d539a0f42f38bd8f146d2aba3c8/cbe89d20e6244943b5d3f2561b64ecdb 已通过游戏 MCP 标记为 resolved，记录版本 27，保留批注原文。
- 复现 64 支星屑指向同一敌人时，原布局缩放产生 0.000122 像素的宽度误差，Clamp 的最小 X 为 24、最大 X 为 23.999878，抛异常后牌面未完成定位与缩放，全部重叠在画面左上角。单张预览正常。
- src/UI/Arrows/ArrowHud.Hover.cs 修正 Clamp 上界，并在密集布局中选取能容纳更大牌面的行数。64 张从 22 列、3 行、0.245 倍缩放改为 13 列、5 行、0.443 倍；保留每张实体卡及其独立倒计时。
- 新增 tests/Integration/ArrowHoverDensityProbe.cs，通过真实视口鼠标输入验证敌方 1、8、64、128 支箭矢和玩家自身 2 支箭矢。五组共 35 项检查通过：数量与可见性、全卡框处于视口内、实体独立、倒计时更新、直接悬停单支预览、移开清理及移除后的数量刷新。
- Beta、Stable 角色程序集和集成探针构建通过，0 警告、0 错误。128 支场景的首次探针将鼠标放在被箭矢遮挡的敌人中心，命中单支箭矢；改用命中区域内未被遮挡的实际鼠标位置后通过，生产输入逻辑保持原样。
- 5.87 秒录像展示 64 张预览、单支预览与移开后的切换。1280 × 720、30 FPS、引擎内部音轨，抽帧人工检查与完整解码通过；本轮独立实例日志无错误或异常。
- 前后对照、五组回归、录像、批注处理回执和已验证完整构建保存在 artifacts/task-deliverables/arrow-play-speed-20261111/hover-density/，35 个文件的 SHA-256 清单已核验。运行时 Beta DLL 与交付构建一致。
- 提交前审查全部 264 份受版本控制的文本资料，凭据、个人邮箱和本地绝对路径检查通过；独立测试进程已关闭，原目录并行改动保留。


### 星屑箭头靠拢与旧网格恢复阶段（后续已恢复原生扇形）

- 按用户纠正撤销自适应增加预览行数的改动。src/UI/Arrows/ArrowHud.Hover.cs 相对起始 main 仅保留 Clamp 上界保护；牌面行列、缩放、间距及排序均使用原规则。64 张再次使用原先的三行排布，预览边界异常修复继续生效。
- src/UI/Arrows/ArrowHud.Layout.cs 为同目标、连续部署的星屑建立相邻槽位约束。纯星屑区域的中心列距由 80 收紧至 56，行距由 96 收紧至 72；混合区域保持原间距并让连续星屑相邻。箭矢实体、牌效、倒计时、原始顺序与鼠标预览均保留。
- src/UI/Arrows/ArrowLayout.cs 在相邻约束内继续选择已有箭矢最少移动、总位移最短的有序分配；其他箭矢继续使用原算法。两支星屑的实机中心间距从均匀散开的 400 降至 56。
- tests/ArrowLayout/Program.cs 新增四种相邻场景：两支新星屑、接续旧星屑、填补移除间隙与两段分离的星屑。修改前真实旧分配算法在首项相邻断言失败；修改后四项通过，原有八项穷举最优位移、六种阵型、分区与十种密集场景也通过。
- 2、8、64 张实机悬停的 21 项检查通过，覆盖数量和可见性、视口边界、实体独立、实时倒计时、单支悬停、移开清理与删除后刷新。正常出牌录制中生成的八支星屑全部相邻，目标最终生命 899/1000，部署与发射动画节点均为 0。
- Beta、Stable 与集成探针构建通过，0 警告、0 错误。最终录像展示正常出牌、星屑聚拢、原布局多牌预览及单支预览；引擎内部有声音轨，抽帧与完整解码通过，实机日志无错误或异常。
- 最终前后截图、实机坐标、21 项结果、录像、批注最新处理回执及已验证完整构建保存在 artifacts/task-deliverables/arrow-play-speed-20261111/stardust-cluster/，SHA-256 清单已核验。
- 最终聚拢录像长 13.50 秒；密集 64 支箭头的三行中心 Y 为 188、260、332，中心列距为 56。批注处理说明更新至版本 29，保留原文。提交前审查 264 份文本资料通过，本任务测试实例已关闭；原目录当前 18 项并行改动保持原样。


### 原生扇形恢复与半重叠星屑阶段

- 重新核对保留分支 codex/arrow-ui-review-20261110 的 2a8bf4f 及其 four-focus.png 实机截图，确认用户所指原效果是原生手牌式横向扇形。此前按起始 main 的旧网格对照，恢复了错误的参考版本；本轮已恢复正确效果。
- src/UI/Arrows/ArrowHud.Hover.cs 复用该版本：最多五张遮叠牌面，当前牌 0.9 倍并抬起直立，邻牌 0.702 倍并轻微倾斜，0.12 秒焦点过渡；支持邻牌悬停聚焦、滚轮、方向键与 PageUp/PageDown 逐张浏览，以及 Esc 收起。序号位于主牌下方，沿用原有战斗主体、意图和手牌避让与边界保护。
- 恢复时保留当前 CardAnchors 接口，将原 DisplayTargets 对接到当前 Targets；当前布局没有超量按钮，省去该按钮的分支和构造方法。src/UI/Arrows/ArrowHud.cs 接回逐空闲帧预热；ArrowHud.Snapshot.cs 在部署移除时归还预览卡；ArrowHud.Deployment.cs 在退出时清理缓存，沿用原生 NCard 对象池。缓存上限十张。
- src/UI/Arrows/ArrowHud.Layout.cs 将纯星屑的中心间距改为箭头宽高的一半，即横向 21、纵向 36.4，保持箭头尺寸并形成紧凑阵列。64 支在当前实机画布中为 16 列、4 行。混合箭矢中的连续同目标星屑，在原占用行内压紧至横向 21，其他箭矢保留原槽位；保留实体、目标、倒计时和原始顺序。
- 更新 tests/Integration/ArrowHoverDensityProbe.cs，对敌方 1、3、64 支箭矢和自身 2 支箭矢共 56 项定向条件全部通过：可见性与全牌框边界、实体独立、倒计时、扇形焦点、滚轮全量可达、十张缓存上限、方向键、邻牌抬起及外框稳定、Esc、每支箭头的实际命中、单支预览、移开收起与移除后回收。64 支半重叠箭头均可通过真实视口输入命中，64 张均可逐张浏览；混合场景另核对两支星屑的中心距为 21。
- 探针沿用原预览 250 毫秒收起缓冲，删除检查等待 350 毫秒。合成滚轮输入使用上一次真实鼠标事件的位置，避免硬件指针坐标使测试离开悬停锚点；键盘经原生输入队列派发。
- Beta、Stable 与集成探针构建均通过，0 警告、0 错误。正常出牌有声录像长 16.13 秒，覆盖三类箭矢的原扇形聚焦、星屑发射及八支子箭矢半重叠、滚轮与方向键浏览。目标最终生命 899/1000，保留八支星屑，部署和发射动画节点均为 0。
- 密集展示有声录像长 7.67 秒，展示 16×4 阵列、五张可读扇形、浏览至第 64 张、键盘返回与 Esc 收起。两段均为 1280×720、30 FPS、H.264/AAC 引擎混音；逐帧抽样和完整解码通过，最终实机日志无错误或异常。本任务独立测试实例已关闭。
- 原效果截图与源码对照、混合和密集阵列截图、56 项结果、两段录像、批注版本 30 的回执及完整已验证构建保存在 artifacts/task-deliverables/arrow-play-speed-20261111/hover-restore/；43 个文件的 SHA-256 清单已核验。


### 满行换行与同状态星屑合并（当前最终）

- 修正 ArrowHud.Layout.cs 在半重叠后仍按旧 80 列距限制容量的问题。ArrowLayout.Pack 使用实际间距顺序排布，下一支不能放入当前可用宽度时才换行；同时利用旧整数槽位末端的剩余空间。纯星屑保留 21/36.4 的半重叠间距，混合行保留普通箭矢所需的间隔；本轮实机 64 支从 16×4 改为第一行 59 支、第二行 5 支。
- 新增 ArrowHud.HoverGroups.cs，仅合并星屑的展示。按目标和拥有者、卡牌存档属性、升级、动态基础数值、伤害投资、附魔与负面附着、关键词、费用、闪耀、部署回合、剩余回合和重复数等比较；每张实体和结算保持独立。普通箭矢继续逐张显示。
- 状态一致的星屑用一张代表牌显示，在牌面下方标注 ×n；不同状态分别显示，状态恢复一致后实时重新合并，移除代表或普通成员后实时减少数量。直接悬停单支仍显示该支及 ×1。沿用原横向扇形、焦点抬起、滚轮和键盘交互；数量标签预留空间，避免和原翻阅序号重叠，并在预览卡归还对象池前移除。
- 实战复核额外捕获了两种误拆分：Core 每次打牌创建不同的执行路线快照，不能比较其对象身份；公共的 AttacksWhileWaiting 计数虽随攻击增加，但星屑效果不使用它。最终按载荷类型区分，排除这项无关历史；正常出牌生成的八支星屑已合成 ×8。X 费用快照只在实际 X 费用牌上读取。
- tests/ArrowLayout/Program.cs 的原最少位移、阵型和密集布局检查通过，新增实际间距下 2、57、58、59、64、128 支、旧网格剩余宽度及混合行换行检查通过。普通箭矢沿用原位置分配，星屑所在行优先保证先填满再换行。
- 实机 19 个合并与布局场景通过：×64、倒计时差异与恢复、升级、伤害加成、额外攻击数、费用、重复数、消耗标记、关键词、动态数值、闪耀、混合牌型、59+5 换行、移除代表、移除成员、相同投资后重并、相同普通箭矢保持分开，以及无关等待历史不拆分。数量文字位于牌下且处于视口内。
- 使用不同投资数值保持星屑状态各异，原单张、三张、64 张和自身箭矢 56 项交互条件通过；最终行宽下另复核 64 张的 14 项交互条件，包含逐支真实鼠标命中、滚轮全量浏览、方向键、邻牌抬起、Esc、缓存上限和回收。
- Beta、Stable 与集成探针构建通过，0 警告、0 错误。正常出牌有声录像 14.53 秒，验证目标最终生命 899/1000、八支星屑合成 ×8、部署与发射动画节点为 0。密集对照录像 7.02 秒，由准备完成后分别录制的 ×64 与两种倒计时 ×32/×32 片段拼接；录制内仅使用正常出牌、悬停、滚轮、键盘和只读查询。
- 所有最终录像为 1280×720、30 FPS、H.264/AAC，引擎混音、抽帧人工检查及完整解码通过；最终日志无错误或异常，独立测试进程已关闭。前后布局、数量与状态对照、定向结果、录像、批注版本 31 的处理回执及完整已验证构建保存在 artifacts/task-deliverables/arrow-play-speed-20261111/grouped-hover/，38 个文件的 SHA-256 清单已核验。


### 混合箭矢效果说明

核对现有代码并复用已通过的混合场景截图：两支同状态星屑和一支满弦射击在场上保持三支箭头，连续同目标星屑为 21 间距，与普通箭矢保持 80 间距；混合行的行距为 96。预览显示星屑 ×2 和单独的满弦射击，共两张代表牌。普通箭矢即使同名同状态也逐张显示；状态不同的星屑分组展示。仍按目标和原打出顺序排布，不跨过夹在中间的普通箭矢重排场上星屑。截图归档到 artifacts/task-deliverables/arrow-play-speed-20261111/mixed-explanation/mixed.png；此前 grouped-hover/normal.mp4 包含星屑、满弦射击与流星的实际混合出牌和悬停。本轮未修改生产实现。


### 合并与清理授权

用户已明确确认合并与清理。集成目标仍为起始捕获的 main；本轮原目录已推进至 ded7f11，新增剧情奖励实现及测试记录。合并前保存当前原目录逐文件哈希、工作区和暂存差异，保护并行改动；录像、截图、结果及验证构建已在原目录 artifacts/task-deliverables/arrow-play-speed-20261111/ 独立保留。清理范围为 ../worktrees/STS2_ShoujoKagekiJunna/arrow-play-speed-20261111、codex/arrow-play-speed-20261111 及工作树内临时游戏、构建和中间资料。


### 最新 main 同步与集成复核

main 在预检期间由 ded7f11 推进至 84d48ce，新增剧情奖励实现、卡牌效果测试记录及卡牌贴合资料，已无冲突合入任务分支。本任务箭矢生产和测试源码与同步前完全一致；生产侧新增差异仅为 main 的 KnowledgeProgress 剧情奖励流程。同步后的 Beta、Stable 和集成探针构建均为零警告、零错误，定向布局检查通过。同步后的完整构建独立保存在 artifacts/task-deliverables/arrow-play-speed-20261111/integration/validated-build/，此前 205 个归档文件逐一通过 SHA-256 核验。

用户的合并和清理确认已包含本次同步结果。最终执行回执保存在同目录 receipt.json；合入前后核对原目录当前并行文件哈希和暂存、工作区差异，并在提交树一致、构建与归档均确认后清理授权的任务工作树、分支及其临时资料。原目录归档交付物保留。
