# 卡牌数量变化小体系探索

## 原始状态

- 原检出目录为本仓库主目录。
- 集成目标分支为 `main`。
- 起点提交为 `9a39e0d44fd4e61b416e37d803d74f9d10823cc6`。
- 原检出目录初始干净，无进行中的 Git 操作。
- 本次分支为 `codex/card-count-subsystems-20261110`。
- 工作树位于 `../worktrees/STS2_ShoujoKagekiJunna/card-count-subsystems-20261110`。

## 简要计划

1. 通过 junna_workbench MCP 读取最新机制、风格约定、知识组和相关卡牌。
2. 阅读体系设计、体系分析、职业卡池要求及单卡分析文档的相关章节。
3. 围绕整局牌组数量变化提出结构不同的小体系，给出代表牌效、取舍与数量轨迹。
4. 逐项核对既有方案、知识单向发放、闪耀、永久牌身份及数值边界。
5. 完成最小文档验证并提交，按 $worktree-task 在合并及清理前请求确认。

## 结果与变更

已基于工作台revision 4685完成五个机制方向及一个取得方式变体，见同目录的 [proposals.md](proposals.md)。

- 候选覆盖共享补给、周转负担、永久拆分、永久合并与精简转成长。
- 对照在用牌及相关退役候选，明确小体系、知识组结构和取得变体的定位。
- 已核对知识单次发放、闪耀及升级恢复、永久来源、临时副本、重放、战后预约和实际牌身份。
- 发现实现文档中的旧遗物描述与MCP当前内容不同，设计依据采用MCP当前内容。
- 最小验证通过，包含五个机制及一个变体的章节完整性、四份通用设计参考、六条数量轨迹算术、65份已跟踪及待添加文档的脱敏扫描。数值保留为纸面初值。
- Git跟踪文档的凭据及本机绝对路径扫描已完成，提交前再次扫描新增文档。
- 两份任务文档将合并为一个任务提交，并同步main。合并及删除本次工作树和分支等待 $worktree-task 要求的确认。

---
# 用户原始输入

## 1. 工作区与项目指引

工作区为本仓库，使用 PowerShell，日期为 2026-11-10，时区为 Asia/Shanghai。用户提供的本机与环境绝对路径以占位符保存。

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

- 主台部署在 `vipcloud`，由 `junna-workbench.service` 运行。程序入口为 `<服务器程序目录>`，主台数据为 `<服务器主数据库>`，内部连接配置为 `<服务器私有连接配置>`。图片保存在 `<服务器图片目录>/`，通过 HTTP 哈希地址读取；历史和请求回执与条目一起由 SQLite 事务持久化。`state.json` 为迁移前备份。
- 公开只读页为 `https://junna.17181718.xyz/`，编辑页与 MCP 使用该域名下的 HTTPS 长路径，未设置 IP 白名单。服务器网关与 MCP 分别监听 `127.0.0.1:54828`、`127.0.0.1:54829`，HTTPS 路由位于 `<服务器HTTPS配置>`。
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

## 2. 设计请求

$worktree-task  你看看 “绝望的轮回” 小体系就设计的不错，契合了junna角色卡牌中卡牌数量变化的整体思路，你能尝试设计更多的类似小体系吗。读取相关体系设计文档，给出多个小体系机制

## 3. 用户提供的技能全文

技能名称为 worktree-task，来源为 $worktree-task。
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



## 主目录并行改动检查

起点检查为干净。工作期间出现以下并行未提交改动，当前HEAD仍为起点提交。记录路径与SHA-256，合并前按当时最新状态校验并保护。

| 状态 | 仓库相对路径 | SHA-256 |
| --- | --- | --- |
| ` M` | `ShoujoKagekiJunna/images/cards/JK122B.jpg` | `71c63022228492c7d7daa664b8030474db8731c532c057379b90796f609aa184` |
| ` M` | `ShoujoKagekiJunna/localization/eng/cards.json` | `6d7eb33771049eaeddee3a607927d027f95a4a52e1f50687e5721be1e72e9515` |
| ` M` | `ShoujoKagekiJunna/localization/eng/powers.json` | `67e244419a89febbc8d49b5b84dee17046a01f9452917f14ae502349aa67d822` |
| ` M` | `ShoujoKagekiJunna/localization/zhs/cards.json` | `6d7eb33771049eaeddee3a607927d027f95a4a52e1f50687e5721be1e72e9515` |
| ` M` | `ShoujoKagekiJunna/localization/zhs/powers.json` | `67e244419a89febbc8d49b5b84dee17046a01f9452917f14ae502349aa67d822` |
| ` M` | `data/card-pool.json` | `9fbeb4829898d165fff2e3845bea0834f85e473d6651c52566994198bcf76072` |
| ` M` | `data/card-rules.json` | `8b63aa7220863544ec1fd6578f0c62625aa17122103f9f082a6e0496ac54d857` |
| ` M` | `src/Combat/Cards/CardEffects.cs` | `a830680e03d57cf48fc8e319a284d2b60c0f927ac3075279e6b385f2630ebfd6` |
| ` M` | `src/Combat/Damage/AttackEventHistory.cs` | `a5ad3878040c66d14c46ef832467d3da4c85af24f4cc95f79a95f1d27eed4984` |
| ` M` | `src/Combat/Knowledge/KnowledgeGrowth.cs` | `850840a1b94d27e557a04d2ef1cbc7ca051fec6b10434534c244b384e33148ea` |
| ` M` | `src/Models/CardCatalog.Generated.cs` | `088af392bd0d75a4cfcebcbce7db890a90e893a0f045c3cd81efa77a60cdcb38` |
| ` M` | `src/Models/CardRules.Generated.cs` | `2d7fd592abdf85a246f8eaad8284b190ea973dd64fa346fbbe12259b78899fba` |
| ` M` | `src/Models/CatalogCard.cs` | `8131d64a3202110366f0cce7d6516269a93a4594d75ff16b6ab969c4ef389686` |
| ` M` | `src/Models/CatalogPower.Damage.cs` | `5a8a4be9e4405a5bc8185f818fb79eca37ec54672c7e7ae71d6797e18196fb3c` |
| ` M` | `src/Models/CatalogPower.Description.cs` | `4e7d02edf5d472ae727ccc3742a1363e1a372ce10da3ef5f43820f7e2d43aa12` |
| ` M` | `src/Models/CatalogPower.Turns.cs` | `81e617fe0e745ac9653c67b4ff14efddd63e7c071ad5704551b37c2354c8c398` |
| ` M` | `src/Models/KnowledgeDamageVar.cs` | `51b915866799646d067236d8acf54d400382bf301fcdda56bf97b2503693d90b` |
| ` M` | `tests/EndlessLearningChecks/generator_check.py` | `365b9d464ac18af3255faad3f7589d0a509070f5795b34cb4ecac107f60cf14f` |
| ` M` | `tools/assets/generate-card-catalog.py` | `ef3043ddc2db4528eceb4619e3237205b95d80f90e719d60fa38b7df4b2d6875` |
| `??` | `ShoujoKagekiJunna/images/cards/JK314.svg` | `8c63d2ab46ed0b4d9b1a0bb1b26cfc70f0ada989fb754bb87e9118a98d871e2d` |
| `??` | `ShoujoKagekiJunna/images/cards/JK314.svg.import` | `ef9fa48646a67ff4fa7b3e89eca6033a0749f20fd6b126f70bfd3ec430091705` |
| `??` | `src/Models/JunnaArchitectDialogue.cs.uid` | `1b88a6fc108870f47626ceec6ecb6850835cc719fafa13d1e2fc072cf87c7f92` |
| `??` | `src/UI/Arrows/ArrowPortraitTexture.cs.uid` | `c7032624c5729cd3159766d91e36f9ade641b9fea902b5533a05b1c4c6555e09` |
| `??` | `tests/CompatibilityProbe/DescriptionAnnotationsProbe.cs` | `1cc8e7fef89203ba69d2694228bf49e4db28759c2ad764a8005bc42aa1f97238` |
