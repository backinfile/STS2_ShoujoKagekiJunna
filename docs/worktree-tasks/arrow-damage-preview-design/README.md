# 飞矢伤害预览设计

基线为 `main` 的 `f4ab9d7`。工作树位于 `../worktrees/STS2_ShoujoKagekiJunna/arrow-damage-preview-design`，分支为 `codex/arrow-damage-preview-design`。原检出的 `docs/worktree-tasks/knowledge-refit-20261117/` 为未跟踪目录。

## 原始用户输入

<details>
<summary>工作区约定与项目说明</summary>

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

</INSTRUCTIONS>

```text
<environment_context>
  <cwd><Junna-repository></cwd>
  <shell>powershell</shell>
  <current_date>2026-11-18</current_date>
  <timezone>Asia/Shanghai</timezone>
  <filesystem><workspace_roots><root><Junna-repository></root><root><Karen-repository></root><root><card-research-repository></root><root><Nana-project></root><root><STS2-MCP-repository></root><root><Core-repository></root><root><session-visualizations-directory></root></workspace_roots><permission_profile type="managed"><file_system type="restricted"><entry access="read"><special>:root</special></entry><entry access="write"><path><Junna-repository></path></entry><entry access="write"><path><Karen-repository></path></entry><entry access="write"><path><card-research-repository></path></entry><entry access="write"><path><Nana-project></path></entry><entry access="write"><path><STS2-MCP-repository></path></entry><entry access="write"><path><Core-repository></path></entry><entry access="write"><path><session-visualizations-directory></path></entry><entry access="write"><special>:slash_tmp</special></entry><entry access="write"><special>:tmpdir</special></entry><entry access="read"><path><Junna-repository>/.git</path></entry><entry access="read"><path><Karen-repository>/.git</path></entry><entry access="read"><path><card-research-repository>/.git</path></entry><entry access="read"><path><Nana-project>/.git</path></entry><entry access="read"><path><STS2-MCP-repository>/.git</path></entry><entry access="read"><path><Core-repository>/.git</path></entry><entry access="read"><path><session-visualizations-directory>/.git</path></entry><entry access="read"><path><Junna-repository>/.agents</path></entry><entry access="read"><path><Karen-repository>/.agents</path></entry><entry access="read"><path><card-research-repository>/.agents</path></entry><entry access="read"><path><Nana-project>/.agents</path></entry><entry access="read"><path><STS2-MCP-repository>/.agents</path></entry><entry access="read"><path><Core-repository>/.agents</path></entry><entry access="read"><path><session-visualizations-directory>/.agents</path></entry><entry access="read"><path><Junna-repository>/.codex</path></entry><entry access="read"><path><Karen-repository>/.codex</path></entry><entry access="read"><path><card-research-repository>/.codex</path></entry><entry access="read"><path><Nana-project>/.codex</path></entry><entry access="read"><path><STS2-MCP-repository>/.codex</path></entry><entry access="read"><path><Core-repository>/.codex</path></entry><entry access="read"><path><session-visualizations-directory>/.codex</path></entry><entry access="read"><path><Junna-repository>/.aws</path></entry><entry access="read"><path><Karen-repository>/.aws</path></entry><entry access="read"><path><card-research-repository>/.aws</path></entry><entry access="read"><path><Nana-project>/.aws</path></entry><entry access="read"><path><STS2-MCP-repository>/.aws</path></entry><entry access="read"><path><Core-repository>/.aws</path></entry><entry access="read"><path><session-visualizations-directory>/.aws</path></entry></file_system></permission_profile></filesystem>
</environment_context>
```

</details>

```text
[$worktree-task]($worktree-task)  你来做一下箭矢的预览伤害，先搜集相关游戏ui设计资料（非web ui），设计下
```

<details>
<summary>随请求提供的 worktree-task 技能说明</summary>

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

</details>

> 多设计几种

> 我是指非hover状态下的展示，或者打出加速卡牌时的展示

> 你看看能直接反应到敌人的血条上吗（按回合数设计）

> 你搜搜游戏ui设计相关资料（而非web ui） 按游戏ui风格进行设计

> 参考下**格挡、中毒或厄运**  的显示风格

> 淡金段在哪？我怎么看不明白

> 你这配色对吗，搜搜相关资料

> 与剧毒厄运同时在场的不太对吧

> 你看看箭矢的伤害表示用上半血条表示如何，只看清下回合的即可

> 伤害数字位置和显示方式需要设计下

> 感觉不对？评估下显示在箭头旁呢

> 为啥要有一个约等于符号

> 可以，我再看看

> 你再来综合分析下，最好的方案是什么

> 可以，你来实现吧

> 实现后录制实战视频我看看效果

> 你这个箭头图标是不是不太对

> 有算易伤虚弱力量这种吗？

> 如果是箭矢带来的这种易伤呢？

> 你只计算所有明面上的伤害即可，未打出的箭矢，下回合的状态变化不要提前计算

> 是不是不盖住血条更好？

> 有点远了，是不是应该紧贴着，你看看第二回合，或者更多回合怎么显示好？

> 你这个ui不太对，你看看怎么和当前的箭头统一一下，都用 箭头包裹内部数字（表示回合数），需要紧贴着血条，你这个还是有距离。另外多回合分段呢？

> 数字没有对齐，而且你这个不像是箭头，更像宝石

> 总长度不要超过血条

> ？是箭矢的箭头

> 你来取现在飞矢的形状

> 形状还是不对，你看看现在飞矢的形状

> 1 图标有点大了 2 回合数放到图标的箭头内部吧，不要碰到边缘，只显示1位数 3 伤害数值位置应该和图标中心水平对齐

> 怎么在敌人的回合没有了

> 合并推送，清理分支

## 计划

1. 查官方游戏画面与设计说明，提取延时伤害、目标、命中次数的呈现方式。
2. 查纯那飞矢的 HUD、倒计时和实际结算规则，确定预览的交互、显示口径和更新条件。
3. 在既有实机截图上制作静态设计示意，核对摆放、示例数字和源码入口。

4. 基于同一实机画面给出四个摆放和交互方案，核对各自的数字口径及界面占位。
5. 按新增方向检查原生血条和加速牌的实际行为，改为待机和出牌状态的按回合血条预览。
6. 查游戏开发者的界面复盘和官方战斗说明，以原生飞矢图形和血条层重画待机、出牌状态。
7. 核对安装版格挡、中毒、厄运的贴图、分层和数值，制作飞矢同场的显示对照。
8. 核对原生受伤拖尾的颜色、游戏无障碍指南与去色后的可读性，重选飞矢配色。
9. 接入原生血条和飞矢结算，录制普通与剧毒、厄运同场的出牌和命中。

## 结果

交付 [设计稿](design.md) 和 [战斗内静态示意](battle-mockup.png)。确定在现有飞矢悬停焦点下显示逐目标的格挡前伤害，记录了多段、重放、星屑分组和改目标的取值规则。本轮范围为资料和设计，功能尚未实现。

已检查原始输入记录和设计稿内的仓库相对链接，核对星屑的基础伤害为 8。对比了 1280×720 底图与示意图，像素改动仅覆盖目标名下方的伤害标记。Git 文本文档审查未发现需要处理的凭据或本机路径，另行命中的旧工作台脚本属于源码。未运行游戏或构建。

同步本地 `main` 至 `99ec733`。当前「沸腾的热情」在伤害结算中提供乘区加成，飞矢继续按其实际目标范围预览。已复核新版星屑牌数据、实现入口和设计链接。

按追加请求，保留目标下方方案，并新增箭标、焦点卡下缘和按回合汇总三种方案。交付 [四种布局对照](battle-layouts.png)，并在设计稿补齐显示时机、群体与叠箭表现。对照图使用同尺度的实机截图裁切，按回合汇总为示例数据。已逐格检查倒计时、卡框、生命条与伤害标记的相对位置。

按 11 月 19 日的补充，交付 [敌方血条按回合设计](health-bar-design.md) 和 [三种血条与开演对照](enemy-health-preview.png)。核对了原生血条容器及中毒、厄运、格挡的更新逻辑，推荐血条内分段，拥挤时贴边显示。对照来自既有开演录像的待机和打出帧，血量和伤害用静态示例演示，未修改游戏功能。

按追加的游戏界面要求，查阅《杀戮尖塔》的开发者意图复盘、《陷阵之志》的 GDC 战斗界面幻灯片、《XCOM 2》官方手册和《火焰纹章 Engage》战斗说明，并写入对应落点。旧版底框收敛为原生血条内的银蓝分段、微型箭标和数字，立即段改用暖金。交付 [实机静态对照](native-hud-preview.png)，含待机、实际打出开演的底图与放大血条。已查看两态布局和像素比例。

追加的原生状态参考复用了安装版血条贴图、原生状态图标和 Kreon 数字。交付 [原生状态与飞矢四态对照](native-status-comparison.png)。待机用淡金分段，同场改贴血条下沿。开演示例的 32 点预计伤害先由当前格挡吸收 10，显示 0 回合预计掉 22 点生命。已核对图中的图标位置、阈值方向和示例数值。

同步本地 main 至 62076ed，包含飞矢队列间距与生成入场动画更新。已核对同步后的设计链接、原始输入、卡牌加速说明及示意图。

为解决常规尺寸下的辨识问题，提高待机金色填充的明暗差，增加段间暗色切口。补充 [血条局部指向图](arrow-gold-segments-detail.png)，分别定位主条内的两段和与本体状态同场时的贴边线。示例区间和伤害口径沿用前稿。

复核原生橙色扣血拖尾及 Xbox 游戏无障碍指南 102／103。旧深金与原生拖尾色样名义明度对比约 1.09∶1。修改为暖白、深边和斜纹，回合用现有数字。交付 [配色对照](arrow-palette-comparison.png)，包含原生画面、旧色、暖白、灰度、状态同场及开演。已核对缩图和去色情况下的分段；交付为静态设计。

复核了剧毒、厄运和飞矢钩子。修正剧毒与未来飞矢曾同时从当前血量右端起画的问题。保留按先后能推算的下一批，预计先死的目标收起未来段。开演在本次出牌先结算，实际命中后本体重算剧毒位置。交付 [四态顺序对照](arrow-poison-doom-order.png)，已核对示例 92→78→66、厄运 80 的阈值、开演 92→70 及命中后剧毒 70→56。

按新方向收敛为只预告下回合的飞矢，上半血条绘制暖白段，下半保留原生状态色。每个敌人合成一段和一个伤害量。加速结算后按实际剩余回合刷新。交付 [普通目标和剧毒厄运同场的原尺寸及放大对照](arrow-next-turn-upper-half.png)，验证示例区间和下半像素保留。

补充 [数字摆位对照](arrow-damage-number-placement.png)。比较跟随上半预测段、固定在血条右侧和固定在右上角三种摆位。推荐右侧同一行，右侧拥挤时移到右上角。用小箭加单个预计掉血值表达下一回合，核对毒发将上半段左移后数字的锚点仍在原位。

对照 [实际箭标旁与目标合计的两种状态](arrow-marker-damage-placement.png)。推荐清晰单体单支同一目标时把数值放到原飞矢旁，多支、群体和密集箭列按敌人显示一份合计。核对飞矢数字未出现在两支同目标时的原箭槽，白段仍表达同一目标的下回合预计伤害。

按确认去掉预计伤害数字的约号，箭上的回合数和上半血条表达预告状态。更新 [单箭旁 12 与目标处合计 20](arrow-marker-damage-placement.png) 的静态对照。当前无法可靠求值时保留原有箭标和倒计时。

综合比较实际箭标旁、血条右端和白段上方，推荐所有下回合读数统一贴承伤敌人的白段上方。交付 [最终建议的单箭、多箭和剧毒同场静态对照](arrow-next-turn-recommended.png)。验证同一个目标始终在血条处读合计，飞矢覆盖上半且仍按剧毒等实际顺序定位。

战斗中，每个敌人的上半血条呈现一段暖白区间，小箭和合计掉血值跟随区间。支持明确定义的单段、多段、群体和重放，复用原生伤害修正和剧毒预测。格挡按原生清除规则处理，未知的新增格挡或结算修正使用原生读数。加速根据实际剩余回合刷新。

Beta 实战核对了普通场景和剧毒、厄运同场。实验体在倒计时降到 1 时仍为 100 HP，两个 25 伤害飞矢合为 50。普通场景命中后为 50 HP。剧毒 14 与厄运 20 同场时，暖白段从 86 向左延伸至 36，结算后为 36 HP。录像使用隔离的测试存档，开拍前布置测试牌和状态，录制期间仅正常出牌、结束回合。

本地同步至 main 的 57210cf。稳定版和 Beta 均编译通过，最终同步的 Beta 在 v0.113.1 通过上述两段实战。成片约 40 秒，1280×720，含游戏内部混音，存于原检出目录下的 `out/arrow-damage-preview/arrow-damage-preview.mp4`。已检查两段抽帧、宿主生命值以及成片音画解码。

后续以当前战况为准，只汇总已部署且倒计时为 1 的飞矢直接伤害。沿用原生攻击伤害计算，读取已生效的力量、虚弱、易伤和当前格挡。已排队的「牵制」只显示倒计时，易伤实际出现后才更新伤害。暖白段从当前生命末端起画，剧毒和厄运使用原生层。小箭改为朝右下的细长飞矢轮廓。

实战中，基础 25 伤害、4 力量和 2 虚弱时，倒计时同为 1 的「牵制」与「强劲的箭」只预览 21。用「一无所有」实际施加 2 易伤后，预览变为 32。剧毒 14 独立结算，飞矢命中后实验体从 100 HP 降到 54 HP。稳定版和 Beta 编译通过，实战录像保存于原检出目录下的 `out/arrow-damage-preview/arrow-damage-current-state.mp4`。

将暖白预览从原生生命填充移到血条上沿外，长度仍按当前生命和飞矢掉血量定位。新版实机显示红色生命、绿色剧毒和紫色厄运各自完整，飞矢小箭和数字随暖白线移动。Beta 构建和定向战斗通过，易伤实际挂上前后读数仍为 21 与 32，结算后为 54 HP。前后截图和新录像存于原检出目录下的 `out/arrow-damage-preview/`。

进一步把外侧暖白线向血条贴近，深边落在原生边框上沿外。Beta 画面中暖白线是第 495、496 行，原生绿段从第 500 行开始，线与绿段之间为边框的暗色。实战同时部署倒计时 1、2、3 的星象、引弦和强劲的箭，读数按回合切换 16、18、25，敌人生命为 100、80、59。新版有声录像保存在原检出目录下的 `out/arrow-damage-preview/arrow-damage-flush-health-bar.mp4`。

用同一帧实战画面比较第 2 回合单独加一组、多个回合同排、每回合各画一条线。设计建议第 2 回合只追加弱化的圆圈回合标和数字，更远的批次通过场上飞矢倒计时查看。对照图为 `out/arrow-damage-preview/arrow-later-turn-layouts.png`。本次实装调整了白线间距。

实装多回合分段。每段底边贴住原生血条上沿，批次按最近到最远向左排。复用场上银边飞矢的箭框轮廓，倒计时绘在框内，外侧显示当前战况下该批预计掉血。单个敌人的各批独立计算。总段长超出现有生命时按比例缩短，箭框旁的数字保留实算值。

稳定版和 Beta 编译通过。Beta 实战同一敌人的倒计时 1、2、3、6 显示 16、36、25、36。两支引弦合并为倒计时 2 的一段，射手结束回合后各段按新倒计时重排。剧毒 4、3 独立结算，被测敌人生命依次为 100、80、41。新版有声录像和局部截图存于原检出目录下的 `out/arrow-damage-preview/`。

将轮廓改为平尾、箭杆、箭肩和向下的尖头。框内回合数和旁边的伤害共用一条基线。整排按实际绘制字宽计算，计入描边留量。标线左端限定在原生填充左缘以内，标线总宽限定在当前生命填充宽度内。

Beta 编译和实战通过。倒计时 1、2、3、6 的各批读数为 16、36、25、36，合计 113 时标线收在 100 HP 的血条范围内。两回合推进后生命依次为 80、41。放大截图与有声实战录像保存在原检出目录下的 `out/arrow-damage-preview/`。

按场上飞矢前端的金属箭尖重画标记，采用尖端、内凹后缘和原箭尖的灰蓝刃面。回合数放在箭尖较宽处，与旁边的伤害数字对齐。Beta 构建与实机通过，四批仍显示 16、36、25、36，两回合推进后生命为 80、41。放大截图及录像保存在原检出目录的 `out/arrow-damage-preview/`。

改为直接裁切场上单体飞矢已加载的原纹理。取 `floating_arrow_c1.svg` 的下端箭尖区域，按裁片原比例绘制，取代手工绘制的轮廓。裁片适度压暗，内部回合数加深描边。Beta 实测倒计时 1、2、3、6 的分段和数字可读，首轮推进后目标从 100 HP 降至 80 HP。放大图和有声实战录像保存在原检出目录下的 `out/arrow-damage-preview/`。

放大 Beta 实机中的完整飞矢，核对了圆形倒计时槽、宽肩和长尖。血条标记直接使用场上已加载的完整外框纹理、坐标和纵横比。合计标记的原肖像区域用深色填充，回合数字写在原圆槽中，伤害值使用同一基线。实机核对了倒计时 1、2、3、6 同场、两支引弦合计 36 和首轮命中后目标剩余 80 HP。对照截图与录像保存在原检出目录下的 `out/arrow-damage-preview/`。

按原比例将血条飞矢图标宽度由 28 调到 23 画布单位，约缩小 18%。箭身内部只绘制一位回合数，保留边距。伤害数字按图标中线定位，原有多回合分段仍贴血条边缘。Beta 构建与实机核对通过，倒计时 1、2、3、6 效果同场，首轮推进后目标从 100 HP 降至 80 HP。放大截图与录像保存在原检出目录下的 `out/arrow-damage-preview/`。

移除敌方回合和结束玩家回合阶段对预览的屏蔽。战斗进行时按仍在等待的飞矢显示，开始结算的批次从预览中撤下。Beta 实机在敌方回合、目标毒发至 96 HP 时仍显示 16 和 18 两批伤害。下一玩家回合第一批命中，目标剩 80 HP，第二批继续预览。截图和有声录像存于原检出目录下的 `out/arrow-damage-preview/`。

本地任务以单一提交交付，合入起始分支 main 并推送 origin/main。设计底图随录像归档到原检出目录下的 `out/arrow-damage-preview/`。
