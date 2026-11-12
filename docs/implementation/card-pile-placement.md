# 卡牌入堆位置约定

## 运行约定

生成或复制一张牌时，目的牌堆、插入位置、牌的实际版本和发放数量分别确定。卡牌效果由显式实现执行，运行时不会从牌面文字推导这些参数。

- 新生成或复制到抽牌堆的牌，在牌面没有明确置顶、置底要求时，使用 `CardPilePosition.Random`。知识关联牌发放到抽牌堆也使用这一规则。
- 随机插入使用原生 `CardPileCmd.AddGeneratedCardToCombat` 及 `RunState.Rng.Shuffle`。原生随机位置包含抽牌堆两端，单次落在底部属于合法结果。
- 定向抽取已有实际牌时，先用 `CardPilePosition.Top` 移动所选牌，再调用正常抽牌管线，保留抽牌钩子和牌主。
- 永久牌组发放使用 `PileType.Deck`。永久升级和战斗内升级分别沿各自的去处执行，知识组在永久牌组中按既有顺序相邻放置。
- 加入手牌、弃牌堆、消耗牌堆、出牌区或箭矢区时，按对应效果的实际去向处理。

原生 `AddGeneratedCardToCombat`、`AddGeneratedCardsToCombat` 和 `Add` 的位置参数默认为 `Bottom`。修改调用前核对当前游戏 API，不能用参数省略表达随机。

`CardActions.Generate` 的 `position` 参数为必填。所有调用必须选择具体位置，遗漏会在编译时失败。直接使用原生生成 API 的调用也须在实现评审中核对位置。

## 已核对入口

2026-11-10 核对 Junna 的全部 `PileType.Draw` 写入、生成、复制及最终去向，额外检查 Core 的 `ShuffleMoveOrderPatch`。

| 入口 | 对象和去处 | 位置规则 | 验证入口 | 本次结果 |
| --- | --- | --- | --- | --- |
| `CatalogPower.BeforeSideTurnEnd`，A 埋头学习 | 基础心得，抽牌堆 | 显式 Random，基础 1 张、升级 2 张，各份能力独立叠加 | `junna_study_placement check` | 4 项通过 |
| `KnowledgeCombat.GrantUpgrade`，JK82 学无止境 | 战斗内升级生成基础新知，抽牌堆 | 显式 Random，每次实际新增升级层数发放一张 | `junna_study_placement related` | 首次及再次升级通过 |
| `KnowledgeCombat.GrantPartners` | 知识附带牌，沿入口目的牌堆 | 目的地为 Draw 时 Random，数量和单次附带发放沿知识规则 | `junna_study_placement related` | 2 张关联牌发放通过 |
| `CatalogPower.AfterCardPlayed`，退役 JK15 反复研习 | 本场复制，抽牌堆 | 显式 Random，复制保留当前版本，单张生成 | `junna_study_placement related` | 随机位置与复制版本通过 |
| `CardActions.DrawMatching` | 已存在的指定实际牌，抽牌堆到手牌 | 明确 Top 后正常 Draw，其他牌序保留 | `junna_study_placement related` | 指定实际牌和原牌序通过 |
| `CardActions.PlayDrawPileArrows` | 已有抽牌堆箭矢，进入正常出牌与布置流程 | 读取当时快照，沿实际出牌目标及去向规则处理 | 源码对照 | 已核对调用与去向 |
| 永久升级、知识取得、单张复制和故事奖励 | 永久牌组 | Deck 发放及既有组内相邻顺序 | 本次永久升级控制项及既有知识回归 | 永久升级控制项通过，其余源码对照 |
| `CardActions.Fly`、星屑生成和箭矢复制 | 出牌区或独立箭矢区 | 沿各自出牌、布置和布局顺序处理 | 源码对照 | 已核对调用与去向 |
| Core `ShuffleMoveOrderPatch.AddWithVisuals` | 原生洗牌批次 | 原样传递调用方的 position | 源码对照 | 已核对调用与去向 |

退役牌模型仍可被旧存档使用，因此也纳入调用检查。

## 回归要求

使用 `tests/WorkbenchFitSync/StudyPlacementProbe.cs` 的实际能力、Core 升级和知识发放入口进行定向检查。

1. 修复前先保留失败结果，记录目的牌堆、原生 API 收到的位置参数、生成牌的实际索引和数量。
2. 验证基础与升级、叠加或重复升级、空抽牌堆，以及基础生成版本。知识临时生成应只发放本次要求的牌。
3. 把原有牌投影回原牌序，确认随机插入后它们的相对顺序保留。
4. 同时检查永久升级仍进入 Deck、手牌生成仍进入 Hand、定向抽取仍抽到选中的实际牌。
5. 随机结果的通过条件以请求了原生 Random 并正确落入牌堆为准。直接捕获原生 API 的 position，并核对实际落点。
6. 录制前准备场景并核对可见卡牌与模型一致。正常出牌、结束回合后检查实际抽牌结果和前后截图。
7. 构建和安装记录关联回归报告，逐文件校验最终安装包。发现其他受影响入口时，在本表登记对应修复和回归结果。

## 本次问题记录

埋头学习及另外三处入口曾省略位置参数，实际使用原生 Bottom。历史任务文档已经列出这些入口，当时没有形成对应的修复和位置回归。[既有排查记录](../worktree-tasks/calling-arrow-texture-20261109/README.md)

本次将位置选择落实到调用点，并将通用生成入口的位置设为必填。维护记录采用“入口、预期位置、修复状态、验证结果”的对应关系。修复提交完成后，在任务记录中保存失败基线、通过结果、安装哈希与录像位置。

相关任务记录位于 [study-random-placement-20261110](../worktree-tasks/study-random-placement-20261110/README.md)。

本次 11 项定向回归通过。新增 7 项中，修复前的 4 个受影响场景记录为 Bottom，3 个控制场景通过；修复后 7 项全部通过。实机使用原生熔炉祝福升级学无止境，抽牌堆由 8 增至 9，随后正常出牌及结束回合，下一回合同时抽到新知和心得。详细结果与产物校验由任务记录列出。

## 2026-11-12 发牌动画专项

生成历史、随机入堆与可见动画分别核对。新模型加入隐藏牌堆后，使用原生 `CardCmd.PreviewCardPileAdd` 展示实际成功结果，再飞入牌堆；手牌和箭矢沿各自原有展示流程。

本次补齐埋头学习、学无止境的战斗/永久升级、反复研习复制、知识附带牌和永久单张复制的展示。随机位置、数量、基础/升级版本和永久组内排序保留。埋头学习四个真实回合及同类入口十一项定向回归通过，包含预览、飞牌和飞牌完成断言。详见 [发牌动画任务记录](../worktree-tasks/studious-end-turn-animation-20261112/README.md)。