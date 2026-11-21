# 游戏本体代码风格审查

## 一次性修改计划

Junna 的全部改动在一个工作树实施，整体验收后合为一个提交。下列顺序用于处理依赖。本体决定模型职责、命令和结算时序。Karen 提供具体实现参考，包括角色在 Core V1 上的接入、自定义牌堆和分版本构建。 [Karen 与 Core 对照](#karen-与-core-对照)

### 实施顺序

1. **恢复三版本构建。** 按 Karen 的分版本组织，让 Stable v0.107.1、Main v0.111.1、Beta v0.113.1 分别使用经校验的引用程序集。把选牌及伤害签名差异集中到 `GameApi`。构建、加载器、打包检查和工坊准备共用变体清单，缺任何必需 DLL 或 Core 能力时立即失败。普通游玩与 MCP 测试分开加载依赖，复用安装前校验 Junna 和 Core。
2. **先迁移旧档和退役九张牌，再删通用强化。** 在旧模型 ID 被解析前妥善处理牌组与知识关系，保证旧事件可继续。删除 `JK359`–`JK367` 的发放、预览、运行卡池、牌效及专属资源，并在生成器持续排除这些 ID。随后删除通用伤害加成及额外命中的状态、全局补丁、复制、角标、提示、本地化和专属测试。保留牌效自身的多段攻击和条件命中。 [九张牌的处理细节](#九张事件知识牌清理计划) · [伤害实现约定](../../implementation/card-damage-rules.md)
3. **每张牌一个具体类，每个能力管理自身行为。** 每张保留牌的具体类拥有 `OnPlay`、适用的 `OnUpgrade` 和专属状态。选牌、复制和暂存继续复用本体命令与现有 Core 接口，迁完移除按 ID 集中分发牌效的代码。工作台继续生成注册、数值和本地化，保留现有模型 ID、数值和有效保存字段。每个 Power 自己管理触发与状态，走 `PowerCmd` 施加、叠层、移除，保证原生通知读到完整状态。保留不同来源和升级数值的结算，显示用 `DisplayAmount`。删除旧 `JH30` 的三处能量命令补丁，保留实际的能量结转。
4. **对齐遗物、药水、事件及奖励。** 「胶带」使用本体「象牙麻将牌」的耗能和重放回调口径，绿色宝石、燃烧宝石保留原生牌组事件及 Core 闪耀耗尽接口。药水由各自的 `OnUse` 结算，知识药水选牌返回后复查战斗并按组生成，显影液与箭雨药水共用飞矢出牌入口，补齐英语名称、描述和选牌提示。「铭刻」仿照「禁忌魔典」，由所属能力在 `AfterCombatEnd` 每层登记一份 `KnowledgeCopyReward`，领取时才从当前永久牌组选牌。商店按纯那场景资源和视觉类型接入，保留角色先古对话，检查各支持版本的知识获取白名单。 [铭刻流程](#铭刻战后奖励计划)
5. **对齐抽牌、自动出牌和飞矢结算。** 定向抽牌补齐禁抽通知，生成或复制到抽牌堆显式指定位置，未要求置顶或置底时用原生 `Random`。自动出牌用本体默认的同步选敌和 X 值，保留牌效明确指定的目标与支付，删除「更近一步」旧瞄准补丁。转飞矢时保存经原生去向钩子确定的最终牌堆和位置，到期按该去向处理，并保留飞矢自己的消耗、回手和循环规则。实际牌效走本体的效果边界，部署只计一次出牌。 [入堆位置约定](../../implementation/card-pile-placement.md) · [飞矢结算对照](#飞矢结算与联机补充对照)
6. **按战斗和玩家管理飞矢，收口预估与界面。** 按 Karen 的约定牌堆方式，用 Core `ICardMovementPipeline` 跟踪普通离堆并声明 `movement-pipeline@1`。直接移出、单人阵亡、团灭和退出由各自生命周期清理，异步动画返回时确认仍属原战斗。Core 路由确需扩展时保留现有调用契约，并锁定匹配的 Core 包。预计伤害按刷新时的状态读取当前伤害、段数、重放及已生效修正，格挡和未来牌效、能力触发均不参与。纳入可直接读取的本体攻击牌伤害。HUD 由部署状态驱动，统一卡面刷新与节点回收，能量附栏按状态更新，知识升级预览随原界面退出而取消。 [伤害实现约定](../../implementation/card-damage-rules.md)

### 统一验收

| 范围 | 定向检查 |
| --- | --- |
| 构建与资源 | 三个变体分别用正确引用构建，缺少 Main 或所需 Core 能力时打包检查失败，普通启动和 MCP 测试入口各能使用。重新生成规则及卡池资源，用复核后的冻结预期检查版本与条目。若扩展了共享 Core，定向确认 Karen 原有的对应入口仍正常。 |
| 数据与旧档 | 含九张牌、旧事件或旧知识关联的存档能恢复。清理后的卡池、图鉴和奖励没有九张退役牌，其他牌的顺序、升级和状态保留。 |
| 模型和原生命令 | 对照受影响牌效的基础、升级及重放。覆盖能力首次施加、叠层和移除，胶带重放和宝石牌组触发次数，药水英文提示与战斗提前结束，禁抽通知、入堆位置、随机目标和 X 值。 |
| 奖励及联机 | 「铭刻」两层出现两份独立奖励，能跳过、读档后领取，战斗回放正常结束。验证双纯那及纯那与原版角色同店。主客机和回放逐步对照飞矢目标、倒计时、复制和重放。 |
| 飞矢与界面 | 验证转飞矢后的回手、置顶及 `ReboundPower`，用 `Hemokinesis` 核对力量触发时机，并覆盖 `CurlUpPower`、团灭、单人阵亡及退出。改变当前格挡时预估保持一致，改变当前有效伤害修正时预估随之刷新。检查卡面节点复用、能量附栏和升级动画退场。 |

开发中每步只运行必要的定向检查。整套实现完成后汇总验收结果，完整测试套件在说明范围并取得同意后运行。

### 实施记录

- 运行卡池 r5929 留有 96 张有效卡牌和 31 组知识关系。九张事件知识牌已移出运行数据、模型和图片，旧档先过滤实际牌组再走本体反序列化，知识关系在解析前去掉对应 ID。旧事件沿用升级与离开选项。
- 具体卡牌实现出牌和升级，能力施加使用指定模型并在本体通知前准备快照。通用伤害加成和额外命中的补丁、保存状态、界面及关联资源已退役。牌面规定的多段、按当前手牌条件命中及旧版牌自身的研究命中分别由对应牌效计算。
- 飞矢保存本体最终去向，区分部署和实际牌效。状态绑定战斗，使用 Core 移牌流程及本体的直接移出、阵亡、战斗结束和重置接入。界面集中处理卡面刷新与回收。知识获取目标缺失会写明诊断。
- Core 源码仓库更名为 `STS2_ShoujoKagekiCore`，随后发行包 2.0.0 将运行时 Mod ID 和程序集改为 `ShoujoKagekiCore`。Junna 的构建、加载、工作台入口、启动脚本、测试项目与工坊打包一并更新。清单要求 Core 2.0.0，包内保留 SDK V1 接口。

### 定向验收记录

- Stable v0.107.1、Main v0.111.1、Beta v0.113.1 分别用最终 Core 2.0.0 包构建通过。故意将 Beta 程序集传作 Main 时，构建在进入编译前拒绝。工坊包定向检查通过 20 项，包括缺 Main DLL 和混入 Core 两条失败路径。
- v0.113.1 单人实机中，「想守护的天空」产生 9 格挡。「强劲的箭」部署时目标仍为 42 生命，倒计时完成后变为 17。两次「铭刻」独立生成两份特殊奖励，点击其中一份打开选牌，确认后只消费该奖励。
- v0.113.1 使用 Core 2.0.0 续读 Core 1.9.8 写入的存档，两份「铭刻」奖励均恢复。领取一份并确认选牌后，另一份仍可领取。也验证了启动脚本能将旧 Core 移出实例的 `mods/`，继续沿用同一份存档。
- 用运行中存档的副本构造含九张退役牌的旧格式牌组。Core 2.0.0 下通过游戏正式反序列化入口读档，九张均移除，剩余牌的顺序、升级和 ID 一致。旧事件模型仍可用于读档。
- 35 项研究规则检查通过，集成探针已用最终 Core 2.0.0 SDK 编译。完整测试套件、联机和回放待执行。
- 实机录像在工作树的 `out/tmp/native-style-review/`。初次单人战斗录在 `junna-native-smoke.mp4`，Core 2.0.0 的续档与领取奖励录在 `junna-core2-smoke.mp4`。
- 新版 Junna 和 Core 2.0.0 已安装到本机 Steam 游戏，逐文件校验与发行包一致。旧模组保存在游戏目录的 `local-mod-backups/`。另开独立对话用同一构建试玩纯那进阶 0。

## 原始用户输入

> review下所有代码，对比游戏本体代码风格，你看看有哪些问题 $worktree-task

> 你看看mod中的卡牌代码和游戏本地的卡牌代码实现，是不是完全不一样？

> 你再来整体分析，列一个问题列表和简要修复计划

> **你写个文档记录下，飞矢的预计伤害全部按当前状态来，不计算格挡，不计算未来的卡牌效果或者能力效果，因为结算效果太复杂了你算不清楚。 伤害加成和额外命中的全部相关逻辑可以删除了，游戏内没有相关效果了。 为啥你的卡牌不是一张卡牌一个类？**

> mod内删除那9张知识牌，你先别修改，只写计划

> **铭刻的效果应该和另外一张战斗结束后删除牌组一张牌的先古牌使用类似的结算流程，都是创建一个特殊的战斗奖励**

> 你应该用本体的方式来做，你继续review其他代码

> 继续对照，然后整理一个简要修改计划给我看

> 是一次性全部修改的计划

> power,遗物等代码呢？

> 继续对比

> 继续

> 整理下一次全部修改的计划，我看看

> 也读读karen 项目的项目接口，代码风格等做个参考

> 重新给我一个 一次性全部修改的计划

> $worktree-task  按计划全部修改吧

> core的目标变了，你看看 <Core 仓库目录>

> core还正在修改，你可以继续干你的，等会更新下

> 安装到本地，我来试试，你也创建一个新对话打一局junna进阶0试试

## 简要计划

- 以 `main` 的 `b3cec76` 为基线，盘点运行时代码、测试及配套脚本。
- 以本机游戏程序集反编译所得的实现为参考，核对卡牌、战斗钩子、持久化及界面生命周期。
- 按影响排序记录发现，附上代码位置和可行的修正方向。

## 基线审查结果

以下代码位置对应实施前基线 `b3cec76`。

审查基线为 `main` 的 `b3cec76`，任务工作树为 `../worktrees/STS2_ShoujoKagekiJunna/native-style-review`。盘点了 101 个运行时 C# 文件、加载器、110 个测试 C# 文件，以及 Godot 脚本、构建、打包和数据生成入口。重点沿正常出牌、飞矢结算、选牌、战斗结束和发布路径追踪实际调用。

本体参考为本机 v0.113.1 程序集（`d09d48e5`）的反编译结果。兼容检查另用 Steam depot 的 v0.111.1 程序集（`cc1f9462`）和 v0.107.1 引用包。风格对照以模型职责、命令调用和生命周期为依据。

### 需要修复的问题

| 优先级 | 发现 | 影响与修正方向 |
| --- | --- | --- |
| P1 | Main 选择目标时引用了 Beta 的 `RunManager.CardSelectorStack`。 [CardActions.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardActions.cs#L177) | 在真实 v0.111.1 上重建报两处 CS1061。该版本的选牌器由 `CardSelectCmd` 提供。建议将版本差异收进 `GameApi`，业务层只询问是否有选牌器。 |
| P1 | 默认构建把同一个 Steam 游戏目录当作 Main 和 Beta 的引用来源。 [build.ps1](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/build.ps1#L8) | 使用 v0.113.1 引用时，把编译符号改为 Main 仍能编译，但 `GameApi.Damage` 的 IL 调用带第八个参数 `ModifyDamageHookType`。v0.111.1 只有七参数入口。发布前应校验每个变体实际引用的程序集，避免成功打出加载器会在正式版选中、却含有 Beta API 引用的 DLL。 |
| P2 | 工坊准备和打包检查均漏检 `ShoujoKagekiJunna.Main.dll`。 [prepare-workshop.ps1](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tools/prepare-workshop.ps1#L19) · [PackagingChecks.ps1](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tests/PackagingChecks.ps1#L22) · [Bootstrap.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/loader/Bootstrap.cs#L17) | 缺少 Main 的目录仍可通过这两处文件检查，v0.111.1 加载器会按 Main 路径尝试加载。构建、发布校验和加载器应使用一致的变体清单。 |
| P2 | 逐牌审计的冻结预期已落后于当前运行卡池。 [AuditExpectations.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tests/Integration/AuditExpectations.cs#L13) · [card-pool.json](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/data/card-pool.json#L2) · [card-pool-expectations.json](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tests/Integration/card-pool-expectations.json#L2) | 审计预期为 r5294／112 张，运行数据为 r5929／105 张。两个使用该预期的入口会在执行逐牌用例前抛错。保留独立预期和失败检查，在同步卡池时一起提交经过复核的预期。 |
| P2 | 「铭刻」在胜利回调内等待一个战斗回放拿不到的选牌结果。 [KnowledgeProgress.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/KnowledgeProgress.cs#L86) | 对照本体先古牌「禁忌魔典」，由能力在 `AfterCombatEnd` 为所属玩家登记特殊奖励，领取时才选牌。复用「学有所成」已有的可序列化 `KnowledgeCopyReward`。 [KnowledgeCopyReward.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/KnowledgeCopyReward.cs#L23) |
| P3 | 通用手牌转箭可使用本体攻击牌，预计伤害目前只识别部分模型。 [ArrowDamageForecast.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowDamageForecast.cs#L58) | 扩充可以从当前状态直接读取的伤害数据。格挡和未来触发按已确认的预估口径排除。 [卡牌伤害实现约定](../../implementation/card-damage-rules.md) |

### 与本体实现的主要差异

- **牌的行为分散。** 本体 `StrikeIronclad`、`Nightmare`、`GeneticAlgorithm` 在各自模型中集中声明变量、出牌、升级和状态。纯那的模型注册和数值生成适合继续由卡池驱动，复杂牌目前还需要同时检查 `CardEffects.Resolve`、`CatalogCard` 中按 ID 的例外、`CardValueNames` 和战斗钩子。「温柔的星光」的格挡与每回合首次能量就是两个入口。可优先把有独立状态和多个阶段的牌效放回具体模型，常规效果继续共用命令。 [CardEffects.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardEffects.cs#L50) · [CatalogCard.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CatalogCard.cs#L16) · [JunnaCombatHooks.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/JunnaCombatHooks.cs#L53)
- **战斗状态的生命周期依赖全局清理。** 本体 `EchoFormPower` 用所属角色和 `CombatManager.History` 查询回合内记录，`CurlUpPower` 把临时状态留在自身能力实例。纯那有玩家或卡实例弱表，也有跨战斗共享的飞矢列表和胜利奖励字典，后者依赖 `BeforeCombatStart`／`AfterCombatEnd` 清理。将状态挂到战斗或持有者可自然限制旧异步结算和当前战斗的交叉影响。 [ArrowCombat.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowCombat.cs#L14) · [KnowledgeProgress.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/KnowledgeProgress.cs#L21)
- **通用额外命中依赖改写攻击命令。** 当前补丁扫描异步 `MoveNext` 并改写命中回调。通用强化机制已决定退役，清理该机制时同时移除专属的全局伤害、段数和第三方命中桥。 [DamagePatches.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Damage/DamagePatches.cs#L124) · [卡牌伤害实现约定](../../implementation/card-damage-rules.md)
- **界面和结算相互调用。** `ArrowCombat` 查找、创建并等待 HUD，HUD 又直接轮询部署和伤害计算。五个补丁各自挂在 `NCard.UpdateVisuals`，归还对象池时目前只有知识叠牌显式处理释放。建议集中卡面刷新与清理的接入点，由待结算飞矢状态驱动 HUD，优先针对同一节点在飞矢、古代牌和普通牌之间复用的情况。 [ArrowCombat.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowCombat.cs#L63) · [ArrowHud.DamageForecast.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/Arrows/ArrowHud.DamageForecast.cs#L14) · [KnowledgeCardPresentation.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/Knowledge/KnowledgeCardPresentation.cs#L279)

卡牌生成、抽取和移动大量复用了 `CardPileCmd`，随机选牌及随机入堆使用本体同步随机数。独立卡牌模型和既有 Core 复制事件已提供拆分复杂牌效的接入点。

### 单张卡牌的实现对照

手写起始牌与本体的实现接近。纯那的打击和防御在具体类里声明 `CanonicalVars`、实现 `OnPlay`，并在 `OnUpgrade` 修改变量，与本体 `StrikeIronclad`、`DefendIronclad` 的职责一致。打击通过 `DamageCmd.Attack(...).FromCard(...).Targeting(...).Execute(...)` 结算，防御通过 `CreatureCmd.GainBlock` 结算。 [Cards.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/Cards.cs#L105)

r5929 的 `cards` 列表有 105 个条目，每张都有自己的注册类和模型 ID。其中 102 个类由工作台导出生成，集中写在 `CardCatalog.Generated.cs`，通过 `CatalogCard` 共享牌效入口。这样同步卡池时可以统一更新费用、升级、规则和本地化。以「朋友！」为例，生成类注册模型，规则表声明 `BlockAndDraw`，卡池提供基础及升级数值，`CatalogCard.OnPlay` 把结算交给 `CardEffects.ResolveCommon`。本体同类的 `ShrugItOff` 将格挡、抽牌顺序及升级写在具体类里。两者使用相同的 `CreatureCmd.GainBlock`、`CardPileCmd.Draw`。 [生成器](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tools/assets/generate-card-catalog.py#L311) · [CardCatalog.Generated.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CardCatalog.Generated.cs#L225) · [card-rules.json](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/data/card-rules.json#L1866) · [CatalogCard.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CatalogCard.cs#L83) · [CardEffects.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardEffects.cs#L377)

后续按本体方式组织单牌行为。具体卡牌类拥有 `OnPlay`、升级行为和专属状态，能力类负责自己的战斗回调。工作台生成模型注册、数值与本地化，共用操作沿用本体命令及已有公用机制。迁移沿用模型 ID 与保存字段，以单张牌或一组关联牌为验收单位。

「满弦射击」的伤害效果仍是原生 `DamageCmd.Attack`，Core 出牌路由把牌效交给飞矢牌堆，倒计时结束时调用原牌效。本体 `TheBomb` 也保留原生命令，但把倒计时管理放在 `TheBombPower` 上。飞矢需要保存实体卡、目标、重放和牌堆去向，共用延迟结算有明确作用。适合向本体靠拢的部分是复杂牌自身的行为归属，例如让「温柔的星光」的首次出牌状态与出牌逻辑从全局回调和分发器收回具体模型。 [JunnaFullDraw.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/JunnaFullDraw.cs#L13) · [HandArrow.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/HandArrow.cs#L162) · [JunnaCombatHooks.cs](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/JunnaCombatHooks.cs#L51)

### Karen 与 Core 对照

对照本机 Karen 的 `43135db` 与 Core 的 `bab02df`。Karen 通过 BaseLib 和本体模型注册实际内容，共用操作走 Core V1。

| 对照项 | Karen 的实现与纯那可采用的做法 |
| --- | --- |
| 单牌与能力 | Karen 的 `KarenDefend`、`KarenNewDay` 把 `CanonicalVars`、`OnPlay`、`OnUpgrade` 放在具体卡牌，`KarenNewDayPower` 自己检查所属玩家和原去向，用 `PowerCmd.Decrement` 扣层。适合作为纯那拆分 `CatalogCard`、`CatalogPower` 的组织样例。纯那继续生成数值与注册，模型类承载行为。参考 Karen 的 `src/Core/Models/Cards/promisePile/KarenNewDay.cs` 和 `src/Core/Models/Powers/KarenNewDayPower.cs`。 |
| 遗物、药水和奖励 | `KarenWaterBottleRelic` 将每场使用状态留在遗物实例，`KarenBaseballCatRelic` 直接实现 Core 的 `IShineDepletionListener`，通过 `DisplayAmount` 显示计数，并独立复制可变集合。纯那宝石已实现同一接口，可以沿用。`KarenTeaPotion` 在自身 `OnUse` 调原生命令，`KarenSunlight` 用 `CombatRoom.AddExtraReward` 为当前玩家登记额外奖励。「铭刻」继续以本体「禁忌魔典」的 `AfterCombatEnd` 为发奖时机。参考 Karen 的 `src/Core/Models/Relics/`、`src/Core/Models/Potions/` 和 `src/Core/Models/Cards/shine/KarenSunlight.cs`。 [纯那宝石](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/JunnaGems.cs#L12) |
| Core 公共接口 | Karen 的 `CardSelectCmdEx` 转用 `CardOptions`，实际候选牌走 `CardPicking`，批量出牌共用 `CardExecution.PlayStaged`，下一次出牌的额外重放走 `CardReplays`。纯那已经使用其中的选牌、暂存和复制服务，迁移牌效时直接沿用这些入口。飞矢布置后保存的重放次数仍归部署快照。参考 Karen 的 `src/Core/Commands/`、`src/Core/ExtraReplaySystem/ExtraReplayExtension.cs` 和 Core 的 `src/Api.V1/`。 [纯那选牌与出牌](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardActions.cs#L40) · [飞矢部署](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowDeployment.cs#L33) |
| 自定义牌堆与状态 | Karen 的 `PromisePileManager` 通过 `ICardMovementPipeline` 在原生移牌钩子之后处理入堆和离堆。状态按 `PlayerCombatState`、牌堆和所属 Power 保存，异步刷新后会检查玩家是否仍在原战斗。纯那可采用同一接口处理正常飞矢离堆，并像 Karen 一样在包里声明 `movement-pipeline@1`。已核对 v0.111.1 和 v0.113.1，`CardPileCmd.RemoveFromCombat` 不经过该移牌钩子，直接移出和战斗退出仍需各自的清理入口。参考 Karen 的 `src/Core/PromisePileSystem/PromisePileManager.cs`、`tools/core-package.ps1` 和 Core 的 `src/Modules/Shine/CardOperations.cs`。 [纯那 Core 能力声明](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tools/core-package.ps1#L25) |
| 商店节点 | Karen 的 `NMerchantCharacterPatch` 检查视觉子节点类型，可借鉴按实际视觉节点识别的思路。它将检查施加到所有商店角色，并读取托管类型名。纯那将判断限制于自己的静态场景，类型核对参考本体 Spine 绑定使用的 Godot `GetClass()`，并覆盖两名纯那同店。参考 Karen 的 `src/Core/Patches/NMerchantCharacterPatch.cs`。 [纯那商店补丁](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/JunnaMerchantPresentation.cs#L7) |
| 构建与 API 边界 | Karen 的桌面构建分别取 Main 和 Beta 引用，核对游戏版本及 Core 目标，从同一个变体表生成和检查 DLL。其 `AttackCommandCompat` 把 `FromCard` 的签名差异收在一处。纯那沿用这个构建组织，额外核对实际引用程序集，Main 与 Beta 仍按本地核对到的接口差异分别适配。参考 Karen 的 `tools/build_dual_branch.ps1`、`loader/Bootstrap.cs` 和 `src/Core/Compat/AttackCommandCompat.cs`。 [纯那游戏兼容层](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Compatibility/GameApi.cs#L1) |

### 九张事件知识牌清理计划

目标是从 Mod 移除「舞台少女的武器」整组事件知识牌。本轮记录计划，实施时一起处理运行时数据、获取入口、旧档和后续生成。

| ID | 卡牌 |
| --- | --- |
| `JK359` | 翡翠弓 |
| `JK360` | 爱的审判 |
| `JK361` | 骄傲的火花 |
| `JK362` | 轮舞 |
| `JK363` | 决定者 |
| `JK364` | 水仙花 |
| `JK365` | 花蕾的光辉 |
| `JK366` | 思春期的可能性 |
| `JK367` | 孤高的奥黛特 |

1. **处理事件入口。** 「舞台少女的笔记」已停止在新局生成，旧事件仍能通过 `JK359` 发放其余八张。删除整组发放选项与预览，核对旧档恢复到该事件时可以继续。 [事件发放入口](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/JunnaStageGirlNotes.cs#L60)
2. **处理旧档中的牌。** 确认原生反序列化解析模型 ID 的时机。用固定的九个旧 ID 清理存档中的牌及指向它们的知识关联。若读取阶段必须注册旧 ID，则只保留加载所需的兼容声明，迁移后九张牌不进入可使用牌组。保留其他牌的顺序、升级和状态。
3. **移除运行时数据与实现。** 删除九张卡与九份关联表、专属牌效、提示和图片注册。清理飞矢预测对 `JK361`、`JK363`、`JK366` 的特判。当前生成器会把缺失卡牌转入 `retiredCards` 并继续生成类，需处理这九个 ID，使后续工作台同步不会重新生成可用模型。 [专属牌效](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardEffects.cs#L58) · [飞矢预估入口](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowDamageForecast.cs#L58) · [退役模型生成](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tools/assets/generate-card-catalog.py#L207)
4. **完成定向验收。** 检查新包、图鉴和奖励入口没有这九张可用牌。验证含旧卡牌的存档及停留在旧事件的存档可以恢复，其他知识牌的发放与抽取正常。将旧事件整组牌效探针改为移除及旧档用例，并同步独立逐牌预期。通过后，再处理旧通用伤害加成及额外命中的剩余引用。 [旧事件探针](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tests/Integration/StageGirlNotesProbe.cs#L198)

### 铭刻战后奖励计划

本体 v0.113.1 的「禁忌魔典」施加 `ForbiddenGrimoirePower`。能力在 `AfterCombatEnd(CombatRoom room)` 按层数调用 `room.AddExtraReward`，每层创建一个独立的 `CardRemovalReward`。玩家在奖励界面领取时，才由 `CardRemovalReward.OnSelect` 打开删牌选择。

「铭刻」沿用这一流程。让 `JunnaPowerJK127.AfterCombatEnd` 为所属玩家登记可单独领取的 `KnowledgeCopyReward(player, 1)`，每次施加对应一份。移除胜利回调中供「铭刻」使用的直接选牌和计数。领奖时从当前实际牌组选至多一张符合条件的知识牌，沿用已有复制与奖励序列化代码。验收覆盖两层出现两份奖励、跳过、读档后领取，以及战斗回放能正常结束。 [铭刻能力](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CatalogPower.Rules.cs#L153) · [复制奖励](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/KnowledgeCopyReward.cs#L35)

### 其他模块审查

| 优先级 | 发现 | 修复计划 |
| --- | --- | --- |
| P2 | 三种药水的本地化只有 `zhs/potions.json`。本体查找当前语言再回退英语，BaseLib 3.4.8 检测到 Mod 有英语目录时也会跳过自动选择其它语言。现有英文环境缺少药水名称、描述和显影液的选牌提示。 | 补齐英语药水表，加入打包时的同类键核对。用英文环境核对药水栏提示及显影液选牌界面。 [现有药水资源](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/ShoujoKagekiJunna/localization/zhs/potions.json#L1) · [显影液选牌](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/Potions.cs#L98) |
| P2 | 独立启动器依赖项目内未跟踪的 MCP DLL，路径写死，普通可玩模式也会复制它。新的审查工作树中该文件缺失，启动会在复制步骤报错。`-UseInstalledMod` 复用隔离目录时也未检查现有 Junna 和 Core 文件。 | 启动前统一验证游戏、核心包和模组产物。测试模式允许指定 MCP 产物路径，普通游玩只加载游戏所需依赖。用干净工作树检查两种入口。 [启动脚本](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/tools/dev/start-test-game.ps1#L76) · [普通游玩入口](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/play.ps1#L25) |
| P2 | 知识药水把一组生成牌逐张交给 `AddGeneratedCardToCombat`。本体单张入口取批量结果的第一个元素，战斗已结束时批量入口返回空列表。选牌恢复或前一张生成触发改变战斗状态时，继续加牌有越界风险。 | 取得选择后复查战斗状态，按整组使用原生批量入堆并处理返回结果。复查满手回落牌堆、取消结算和等待选牌时战斗被提前结束。 [知识药水](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/Potions.cs#L41) |
| P2 | 能力施加与生效数据分两次更新。`CatalogPower.Install` 先走 `PowerCmd.Apply`，原生通知发出后才加入对应的 `Grant`，然后用 `SetAmount` 重写层数。监听者在施加与叠层时可能读到上一份快照，`AfterPowerAmountChanged` 的数值与最终显示也可能不同。 | 能力类在本体施加生命周期内建立自己的数据。实际层数由 `PowerCmd` 管理，剩余次数等 UI 数值用本体同款 `DisplayAmount` 表达。以「呼喊与回应」为例，验证首次施加、叠加及触发时的通知和图标。 [CatalogPower 安装](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CatalogPower.cs#L69) · [层数同步](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CatalogPower.Description.cs#L35) |
| P3 | 「更近一步」已经在牌效内用选项牌选择加速类别，旧补丁继续挂在全局 `CardModel.IsValidTarget` 和 `NTargetManager`。旧目标校验要求 `AnyEnemy`，当前模型固定为 `None`。 | 以当前选项流程为正式入口，移除旧瞄准桥，核对手打和自动打出仍能选择或按现行规则处理。 [当前目标类型](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CatalogCard.cs#L19) · [旧瞄准补丁](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/CloserStepTargeting.cs#L20) · [结算内选择](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardChoices.cs#L21) |
| P3 | 「胶带」与参考本体「象牙麻将牌」的重放时机有差异。两者都读 `CardPlay.Resources.EnergyValue`，本体每次收到 `AfterCardPlayed` 都检查耗能，纯那只在系列首段触发。 | 按本体定稿时用同一个出牌回调口径。用一张够费的重放牌和一张临时降为低费的牌核对实际触发次数。 [胶带](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/JunnaSpikeTape.cs#L26) |

已核对直接使用本体生命周期的实现。两种宝石用原生入堆和移除钩子处理牌组变动，本体变形只对换入的牌发一次 `AfterCardChangedPiles`，所以当前按移出及换入合计两次。显影液与箭雨药水已各自在具体模型的 `OnUse` 内描述自身规则，共用飞矢操作。 [宝石](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/JunnaGems.cs#L24) · [药水模型](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/Potions.cs#L91)

### 共用结算补充对照

- **定向抽牌漏发阻止通知。** 本体 `CardPileCmd.Draw` 在 `ShouldDraw` 返回 false 时调用 `AfterPreventingDraw`，例如原生 `Fiddle` 在此亮起。纯那的两条定向抽牌路径提前退出，只实现了阻止抽牌，跳过通知。置顶前保留这条通知，并核对禁抽后抽牌堆顺序。 [定向抽牌](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardActions.cs#L51) · [分类抽牌](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardTutors.cs#L60)
- **批量免费打出的默认规则分散。** 「星星指引我前行」与箭雨药水使用第一个合法目标，并为 X 费传入 0。原生 `CardCmd.AutoPlay` 在目标缺省时使用同步随机选敌，默认按当前能量捕获 X，且有统一的阻止出牌与去向处理。对齐默认流程时保留牌效指定的目标、一次性快照和特定支付规则。「烁星」实际额外支付属于牌效自身规则，使用本体支付入口。 [批量打出](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardActions.cs#L141) · [额外支付](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/PaidReplay.cs#L23)
- **旧知识关系需在读取处迁移。** `JunnaKnowledgeRelations` 会保存合入的关系表，旧记录中同一主牌的关系优先。移除运行时卡池的条目后，旧档仍可能带九张牌的模型 ID。关系解析直接调用 `ModelDb.GetById`，因此要在解析前清理删除项，并同步清理九张实际卡牌。 [关系读取和解析](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/DynamicKnowledge.cs#L38) · [关系保存](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/DynamicKnowledge.cs#L95)
- **「铭刻」复用本体联机领奖流程。** `RewardsSetSynchronizer` 把领取索引同步给其他玩家，各端执行同一奖励，牌组选取同步具体选择。保留现有 `KnowledgeCopyReward.OnSelect` 的选牌同步和加牌流程，并用主客机各领一份验证。 [复制奖励选牌](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/KnowledgeCopyReward.cs#L35)

### 联机与生命周期补充对照

| 优先级 | 发现 | 修复计划 |
| --- | --- | --- |
| P2 | 本体允许两名玩家选同一角色，进商店时将各自的角色节点挂到同一容器。MegaDot 会自动改名第二个同名节点。纯那补丁只按节点名识别静态立绘，第二个节点改名后走原版 `_Ready`，其 Spine 绑定会拒绝静态 `Sprite2D`。BaseLib 3.4.8 的商店补丁只覆盖播放动画入口。 | 按稳定的场景资源或组件类型识别纯那商店场景，核对两个纯那同店及纯那和本体角色同店。 [商店补丁](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/JunnaMerchantPresentation.cs#L7) · [商店场景](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/ShoujoKagekiJunna/scenes/junna_merchant.tscn#L4) |
| P3 | `GainEnergy`、`LoseEnergy`、`SetEnergy` 的全局补丁只服务已标为旧版规则的 `JH30`。当前运行卡池和模型注册均无该条目，置能补丁仍通过递归保护重新调用原命令。 | 删除三个补丁及专属的 `EnergyChanged`、`AfterEnergySpent` 分支。回合能量重置和本体保留能量的接入继续由 `EnergyReserve` 处理。 [能量命令补丁](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/EnergyReserve.cs#L49) · [能力分支](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Models/CatalogPower.Damage.cs#L15) · [旧版规则](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/data/card-rules.json#L1613) |
| P3 | 本体 `NEnergyCounter` 进入节点树时订阅战斗状态和能量变化，退出时解订阅，战斗状态跟踪器也覆盖能力施加、增减层和移除。纯那的附栏挂在每个能量计数器上，每帧刷新能力数值和布局，隐藏时仍执行刷新检查。 | 复用原生状态通知更新数值，用布局变化重新定位。核对初始显示、能力叠减与移除、回合能量更新和退场清理。 [附栏刷新](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/EnergyStatusBar.cs#L53) · [附栏接入](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/EnergyStatusBar.cs#L140) |
| P3 | 本体升级动画在退出节点树时取消自己的播放任务。纯那的普通升级路径把动画节点退出直接当成完成，并用 `CancellationToken.None` 延迟显示知识牌。提前关闭原界面时，延迟任务仍会尝试在当前 `NRun` 的界面上显示预览。 | 将正常完成与场景退出区分开，延迟预览和原界面共用取消条件。核对正常升级展示顺序和退出时的清理。 [知识升级动画](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/Knowledge/KnowledgeUpgradeAnimation.cs#L26) · [入堆预览](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Cards/CardActions.cs#L212) |

知识获取白名单在正式版 v0.111.1 和测试版 v0.113.1 都找到了全部 49 个目标方法。注册遇到缺失方法会静默跳过，因此把目标解析检查放入支持版本的验证，随版本更新及时定位失效入口。 [知识获取注册](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Knowledge/KnowledgeAcquisitionSources.cs#L85)

### 飞矢结算与联机补充对照

| 优先级 | 发现 | 修复计划 |
| --- | --- | --- |
| P2 | **手牌转飞矢会丢失原牌的出牌去向。** 本体 `ParticleWall` 打完回手，`ShiningStrike` 打完置于抽牌堆顶。本体把所属玩家、牌堆和堆中位置一起交给结算。Core 在进入本体去向钩子前把这一步改成飞矢堆，到期后纯那再按消耗、回手或默认弃牌处理，原去向已丢失。要求原去向为弃牌堆的本体钩子（如 `ReboundPower`）也看不到原去向。 | 把暂存飞矢堆和最终去向分别记录，按本体时机和原生入堆命令执行，处理消耗、回手和循环的优先级。用原牌回手、置顶及 `ReboundPower` 核对去向与触发次数。 [路由注册](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/JunnaMod.cs#L23) · [飞矢离堆](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowCombat.cs#L185) |
| P2 | **原牌伤害可能因能力触发顺序而改变。** 本体将卡牌效果包在 `BeforeCardPlayed`、`AfterCardPlayed` 与战斗效果作用域内。飞矢在布置时走完原生出牌，实际命中时直接调用保存的效果，只给 `CurlUpPower` 补了一个结束回调。例如玩家回合中转成飞矢的 `Hemokinesis` 先扣自己的生命，再攻击。普通出牌时 `RupturePower` 在攻击后才把这次自伤转为力量，飞矢到期时会在攻击前直接拿到力量。 | 按版本复用本体的效果开始和结束作用域，梳理随牌效开始、结束的监听者。部署只计一次出牌，延迟结算不重发通用出牌通知。用 `Hemokinesis` 和 `RupturePower` 核对力量相对于攻击的时序，保留 `CurlUpPower` 多段结算覆盖。 [实际牌效](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowDeployment.cs#L37) · [现有结束回调](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Compatibility/ArrowNativeHooks.cs#L9) |
| P2 | **飞矢状态的清理只覆盖开战和胜利。** 本体团灭走 `ProcessPendingLoss`，退出走 `Reset`，都不触发当前用于清理的 `AfterCombatEnd`。联机只倒下一人时，本体清掉该玩家的五个原生牌堆，飞矢堆和部署记录还留着。飞矢效果会跳过已死亡的所属玩家，HUD 仍会遍历其部署。 | 让部署归属具体战斗和玩家，整场退出时清理，在本体移除阵亡玩家战斗卡牌的时机回收飞矢。视觉等待返回后复查原场战斗及部署，避免旧任务继续执行卡牌移动。用单人阵亡、团灭、未结算时退出及重开战斗验收。 [战斗清理](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/JunnaCombatHooks.cs#L110) · [释放等待](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowCombat.cs#L159) · [HUD 部署读取](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/UI/Arrows/ArrowHud.Snapshot.cs#L20) |
| P3 | **本体联机校验看不到纯那的飞矢状态。** 两个版本的 `NetFullCombatState` 都逐项写入五个原生战斗牌堆。测试版本体「愚人」的箭槽另有自己的状态字段。纯那的自定义飞矢堆及目标、倒计时、重放状态未进入当前校验，即使这些数据暂时不一致，原生校验也可能等到伤害或卡牌移动后才发现。 | 沿用既有的同步动作和选择。用稳定的牌实例、玩家和目标标识为主客机及回放增加逐步对照，覆盖同回合加速、目标死亡换向和复制重放。 [飞矢堆](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowPile.cs#L11) · [部署状态](https://github.com/backinfile/STS2_ShoujoKagekiJunna/blob/b3cec765b483e020c037ea123a8eb222a922128f/src/Combat/Arrows/ArrowDeployment.cs#L10) |

### 验证

- Stable v0.107.1 引用包和 Beta v0.113.1 的 Release 构建通过，Beta 集成测试程序集编译通过。Main 使用 v0.111.1 构建时出现两处 `CS1061`，均指向 `CardSelectorStack`。使用 Beta 引用编译 Main 的检查构建通过，并反编译确认调用绑定到八参数伤害入口。曾使用的旧测试目录虽有 v0.111.1 版本文件，DLL 哈希却与 Beta 一致，正式版结论以 depot 及哈希一致的副本为准。
- `generate-card-rules.py --check` 通过，覆盖 218 项卡牌规则、33 项能力规则和保存字段。逐牌测试预期与运行卡池的版本差异直接由两份 JSON 及入口断言确认。
- 回放及飞矢预估覆盖来自调用链比对。本轮另外读取了安装包内嵌卡池 r5929 和工作台 r5976，确认「孤高的奥黛特」仍带有旧强化效果，并核对了本体「禁忌魔典」的战后发奖顺序。变更仅包含文档。
- 其他模块审查核对了本机本体和 BaseLib 3.4.8 的调用链、仓库本地化资源及干净工作树的启动依赖。药水在战斗结束时的异常路径和能力通知时序为静态分析结果，留待相应修复的定向实机验证。
- MegaDot 4.5.1 的最小无界面实验复现了同一父节点下第二个 `JunnaMerchant` 被自动改名。商店初始化异常由该结果与本体 Spine 校验调用链共同确认，双玩家会话留待修复验收。知识白名单的 49 个目标按两个游戏程序集的声明方法逐项核对。
- 飞矢去向、能力时序、异常收尾和联机校验对照了正式版 v0.111.1、测试版 v0.113.1 及 Core 路由源码。上述飞矢用例为静态调用链结论，实机复现纳入修复验收。
