# 流星、选项图鉴与对应牌回手修复

## Original user input

### 1. 工作区指导

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

### 2. 本地安装要求

安装到本地steam版上

### 3. 原因排查

你查查我现在为啥流星是1回合的

### 4. 修复要求

修复这个问题，并看看有没有类似问题同样处理下；另外打出多张即兴时，只会堆叠前x次这个数值

### 5. 整备选项实现询问

整备这张牌的选择卡牌是怎么做的

### 6. 选项隐藏与图鉴问题

你想我的全部用于选项的牌都隐藏下。另外你看看我现在游戏中为啥衍生池还有几张未解锁的牌，明明unlock all了

### 7. 全部对应牌回手要求

立论和悖论两张牌打出后，都是会获取所有的相对的牌的（不要修改卡牌描述）

### 8. 合并与清理确认

<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_08a64fc012014af9a494174ecca5e69b\",0]","question":"按 `$worktree-task` 的明确要求，请确认是否将本次扩展后的提交 `306d339` 合入 `main`。包含流星生成、即兴叠加、选项牌隐藏、图鉴解锁判定，以及立论／驳论取回全部对应牌，牌面描述保持原文。双版本构建通过，152项定向检查通过，原 main 工作区干净。选择清理时，会先把安装包、报告、截图和录像保存到主目录 `artifacts/task-deliverables/meteor-generation-stacking-20261109/`，再删除本次工作树、任务分支及其中临时文件。","answer":"合并并清理本次工作树和分支"}]
</send_user_message_question_reply>

### 9. 工作树技能要求

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

- 原始分支 `main`，原始提交 `c450f4b86fb1f83311c853c291083226ab9e6d38`，工作区干净。
- 工作树 `../worktrees/STS2_ShoujoKagekiJunna/meteor-generation-stacking-20261109`，任务分支 `codex/meteor-generation-stacking-20261109`。
- 用独立最小化静默游戏构建定向失败回归，覆盖基础／升级生成者、卡牌悬停预览和多张即兴的触发上限。
- 生成牌沿牌面指定版本生成，排查当前所有生成入口。即兴只累计触发次数，每个有效攻击伤害事件生成一张流星。
- 同步工作台相关规则、运行元数据及能力描述，完成构建、定向检查与原生操作录像，按 `$worktree-task` 整理提交和集成。
- 用户追加选项隐藏与图鉴排查。同步已有choiceOnly元数据，修正图鉴将奖励资格当成解锁资格的判定，并验证原生图鉴可见性与选择交互。
- 按当前卡名立论／驳论处理用户的对应牌要求，取回全部既有对应副本，保持牌面描述。同步工作台规则并做跨牌堆与升级版本的定向回归。

## Result and changes

- 修复与定向验证完成，用户已确认将实现提交 `306d339` 合入 `main` 并清理本次工作树和分支。使用已安装r4272卡池为基线，相关工作台规则通过MCP提交为r4275，回执 `f4b073cb-fb2e-4d55-9565-704b8b697ea8`。
- 即兴、连射与箭雨的基础／升级版均生成基础流星，初始等待2回合。修正横扫、连射与箭雨的流星预览。显式生成升级流星及流星自身升级已纳入回归。
- 即兴按所有已安装来源的当前牌面次数合计本回合触发上限，每个有效攻击伤害事件生成1张流星。普通＋普通为前2次，普通＋升级为前3次，升级＋升级为前4次。能力层数与悬停描述同步显示累计上限。
- 核对全部当前生成入口。持续阅读+的牌面指定笔锋+，生成升级笔锋。复制牌与知识关联发放的版本由对应规则确定。
- 新增真实入口回归 `tests/WorkbenchFitSync/MeteorProbe.cs`，同步旧流星预览断言。修复前82项断言中48项通过、34项失败，修复后82项全部通过。覆盖四种生成来源及预览、普通和升级即兴的四种组合、单事件生成数量、回合上限、下一回合刷新、安装前的攻击记录、流星完成后的嵌套触发、明确升级生成与伤害记录。
- 失败基线 `artifacts/task-deliverables/meteor-generation-stacking-20261109/meteor-fix/before.json`，最终结果 `artifacts/task-deliverables/meteor-generation-stacking-20261109/meteor-fix/results.json`。用于复现与验证的游戏内命令为 `junna_meteor_fix check`。
- Beta、Stable、加载器均为0警告、0错误，Godot导入与PCK打包通过。167项运行规则、24项能力规则及字段身份校验通过。只执行本次修复所需定向检查。
- 已录制12.8秒原生悬停与出牌演示，即兴和即兴+合计上限3，前三次攻击各生成1张2回合流星，第四次不再生成。H.264＋AAC，15fps，191次实际采集、1帧补帧、0丢帧。录像记录位于 `artifacts/task-deliverables/meteor-generation-stacking-20261109/meteor-fix/recording.json`，帧拼图与原生截图位于同目录和 `video/`。
- 独立实例采用随机端口、满解锁、最小化与静默配置，启动焦点采样0次前台占用。本次测试实例已退出。
- 修复包 `artifacts/task-deliverables/meteor-generation-stacking-20261109/Junna-fixes.zip` 完整性检查通过，SHA-256为 `b4a37f973aaa0504744315f4ea63fc91124fa541ebe094b8cc60824d071db206`。
- 统一隐藏10张专用选项牌。原有8张辅助模型已设置隐藏，补齐JK78D抽牌堆／JK78E弃牌堆的choiceOnly导出与运行读取。工作台已有这两张牌的正确标记。整备原生选项选择正常，选择箭矢后实际抽取2张箭矢，已保存19.27秒录像。
- 当前Steam档案中纯那衍生牌已有发现记录。原生图鉴先按奖励池生成已解锁集合，再检查已发现集合，纯那奖励池对基础牌、衍生牌与知识关联牌的过滤因此误显示锁定。新增局部图鉴补丁，将当前可展示纯那牌纳入图鉴解锁集合。奖励池资格与已发现判断继续沿原生流程。
- 原生图鉴定向回归修复前21/30通过，修复后30/30通过，覆盖10张选项牌隐藏、流星／更近一步／基础牌／知识关联牌的可见状态、未发现流星状态及奖励资格。结果位于artifacts/task-deliverables/meteor-generation-stacking-20261109/option-library/results.json，截图native-meteor-unlocked.png显示衍生池中的流星正常展示。
- 立论／驳论打出后取回全部既有对应副本，手牌内实例保持原位，抽牌堆、弃牌堆和消耗堆中实例进入手牌，执行中的牌沿原有排除规则。保留实例的升级状态与原生回手去向。基础与升级牌面描述保持原文。工作台仅更新内部结算规则与备注，MCP回执36166c5f-6976-42b7-a152-7769d829659a，修订4298。
- 新增对应牌回手回归tests/WorkbenchFitSync/RecallProbe.cs，40/40通过，覆盖双方普通／升级版、多副本跨牌堆、已有手牌、执行中对象、无对象场景、支付与结算去向。结果artifacts/task-deliverables/meteor-generation-stacking-20261109/recall-all/results.json。实机录像6.27秒，打出立论取回3张驳论，随后打出驳论取回4张立论，94次采集、0补帧、0丢帧。测试辅助模组构建0警告、0错误。
- 最终Beta、Stable和加载器构建0警告、0错误，Godot导入与PCK打包通过。构建记录artifacts/task-deliverables/meteor-generation-stacking-20261109/build.log，最终ZIP包含上述全部修复。
- 全部210份跟踪及新增文档经过敏感信息与本地绝对路径审查，未发现需要脱敏的内容。
- 合入前核对原主目录仍为main及原始提交，工作区干净。Steam游戏进程仍在运行，本地更新将在退出后进行。
- 已将27项修复包、回归结果、构建日志、工作台回执、截图和录像保留到主目录忽略的 `artifacts/task-deliverables/meteor-generation-stacking-20261109/`，复制文件逐项哈希一致。ZIP解包后7个安装文件与工作树发行目录一致。保留清单为同目录preservation-manifest.json，集成与清理回执为integration-receipt.json。本次工作树和任务分支在成功集成核验后按确认清理。
