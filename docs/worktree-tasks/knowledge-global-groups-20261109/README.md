# 知识关联组全局 ID 关系

## Original user input

第一条用户输入（工作区约定及环境）完整记录于 [user-instructions.md](user-instructions.md)，仅将外部文件系统绝对路径替换为用途占位符。第二条用户输入如下：

> worktreetask 我来确认下知识关联牌的实现细节。要求不要改卡牌或关键字描述，只改实现：1 关联组为全局效果，一张牌加入关联组后，其中所有牌都共享一个关联组，因此所有牌的关联组tips都会更新 2 关联组中的卡牌按卡牌名字（卡牌ID）引用，复制牌也会遵循同样的关联组，本局游戏中之后出现的牌也有对应的关联组。进入关联组之后，一个关联组只有一张卡牌入口（其他牌不会作为卡牌奖励掉落），兼收并蓄永远会作为其关联组的入口。


第三条用户输入（合并及清理确认）：

```xml
<send_user_message_question_reply>
[{"answer":"整合既有修改，合并到 main 并清理","question":"任务已提交为 5a3f669，目标为 main；55 项实机检查、30 项逻辑检查及双版本编译通过。主目录 KnowledgeCombat.cs 有需保留并整合的既有修改。如何处理？选择清理时，会先保留录像、断言报告和发行包，再删除任务工作树、任务分支及本次临时状态快照。","questionItemId":"[\"request_user_input_async\",\"call_d9c145c658564faab8e79321c10024f1\",0]"}]
</send_user_message_question_reply>
```
## Brief task plan

- 从 `main` 的 `65538012c43ab020965b7fef2c9eceb91d5c38a8` 创建 `codex/knowledge-global-groups-20261109`，工作树位于 `../worktrees/STS2_ShoujoKagekiJunna/knowledge-global-groups-20261109`。
- 保留原检出已有的 52 个未提交或未跟踪文件，不纳入本任务。
- 将动态关联改为本局玩家持久化的卡牌 ID 关系，合并整个关联组，统一提示、抽牌、获取和奖励入口。
- 添加必要的定向回归验证，检查卡牌及关键字文本未改变，汇总一个任务提交。
- 同步本地主分支后，按 `$worktree-task` 规则请用户确认合并与清理。

## Result and changes

实现与集成验证已完成，合并及清理已获用户授权。

- `src/Models/KnowledgeRelation.cs` 保存以完整模型 ID 为键的关联成员与张数，传递合并重叠组，强制以兼收并蓄为其所在组的入口。保留既定的同名成员数量，重复实体不会扩大关系定义。
- `src/Combat/Knowledge/DynamicKnowledge.cs` 将动态关系持久化到本局玩家数据；已有牌、复制牌、未来新牌、外角色牌和预览均按 ID 读取同一份关系。完整迁移旧实例关联，成员移除后关系仍保留，存档读取与原生玩家快照同步均恢复关系。
- `src/Combat/Knowledge/KnowledgeCombat.cs` 在获取时发放完整合并组，保留所选牌本体与其修改；复制或显式单牌生成继续遵守原有单牌流程。关联抽牌按完整模型 ID 匹配。
- 奖励生成先过滤非入口；已经生成或手工指定的奖励在打开时重新映射入口并去重，兼收并蓄始终保留。纯那以外卡池里的已关联牌同样遵守入口规则。
- `src/UI/Knowledge/KnowledgeCardPresentation.cs` 与 `KnowledgeGroupPreview.cs` 使用模型 ID 区分外角色牌，关系变化后重建预览缓存，所有成员提示展示完整关联组。
- 卡牌、关键字、本地化文本、牌库和运行规则数据均无改动。

### 定向验证

- 修改前使用当前配套 Core 1.4.0 完成编译基线；最终 Beta 和 Stable 编译及资源打包均通过，0 警告、0 错误。
- `tests/KnowledgeGroups` 的 30 项检查通过，覆盖原有数量规则、传递合并、重复模型数量、入口优先级与序列化。
- `tests/Integration/KnowledgeRelationsProbe.cs` 在 Beta v0.111.0 独立满解锁实例的 55 项断言全部通过，覆盖原生奖励选择、原有完整七张组的合并、预览、复制、新牌、后续整组发放、12 次原生奖励采样、奖励选项保留、旧奖励去重、外角色牌、关联抽牌、存档往返、全部实体移除、旧保存迁移、不同玩家隔离和原生快照同步恢复。
- `git diff --check` 通过；确认 `data/card-pool.json`、`data/card-rules.json` 与所有本地化文件无差异。
- 审阅 52 份原有跟踪文档及两份任务文档，未发现私密凭据、私人邮箱或本机绝对路径；新增输入记录使用用途占位符替换外部文件系统路径。
- 实机证据位于 `out/tmp/knowledge-global-groups-20261109/`。`integration-results.json` 保存断言结果，`knowledge-global-demo.mp4` 为约 17 秒、1280×720、30 FPS、有声录像，展示正常获取后的共享提示与唯一奖励入口；抽帧检查和完整解码通过。录制期间仅使用正常奖励选择与悬停操作。
- 仅执行本次关联实现必要的定向检查；Stable 完成编译，完整单人、联机与跨版本流程回归需要另行确认测试范围。

### 集成状态

- 起始目标固定为 `main` 的 `65538012c43ab020965b7fef2c9eceb91d5c38a8`，任务分支为 `codex/knowledge-global-groups-20261109`。
- 原检出的 52 个未提交或未跟踪文件已逐文件 SHA-256 核对，内容与 Git 状态均保持不变；其中 `src/Combat/Knowledge/KnowledgeCombat.cs` 与本任务修改文件重叠。保留原检出，合并前需要整合该文件中的既有修改。
- 任务分支、工作树、发行包及证据保留，等待用户确认后再合并或清理。

### 获准后的整合验证

- 原有 52 项改动完整备份于 `out/tmp/knowledge-global-groups-20261109/pre-integration/`。
- 既有未提交的 `PlayPartners` 保留，并在组合版本中改用完整模型 ID 匹配全部同名实体。新增两项定向断言验证三张同名牌（含复制牌）全部自动打出，来源牌保留在手牌。
- 与原有 r4444 改动组合后，双版本编译、资源打包和 30 项逻辑检查通过，Beta 实机 57 项断言全部通过。
- 原有修改、组合发行包、录像和断言报告已保留至原检出的 `out/tmp/knowledge-global-groups-20261109/`，清单为 `preservation-manifest.json` 和 `combined-input-manifest.json`。
- 首次合并前检查发现 `main` 已从起始提交推进到 `c1555de`（箭矢布局整合），因此在写入主目录代码前停止，转为先同步该提交。

### 最新主分支同步

- 同步目标最终为 `main` 的 `6c15e4c`，包括同期已合并的箭矢布局、不可打出材料转化和牌面数值预览。同步未发生内容冲突。
- 同期整合使原检出 7 个文件发生了必要的合并变化；本任务以其最新内容重新备份全部 52 项现有改动，保存于 `out/tmp/knowledge-global-groups-20261109/pre-integration-after-sync/`。
- 最终组合版本重新通过 Beta、Stable 编译、资源打包、31 项知识组逻辑检查和 Beta 的 57 项实机断言。新录像完成封装、抽帧与完整解码检查。
- 最终发行包位于 `out/tmp/knowledge-global-groups-20261109/packages/integrated/ShoujoKagekiJunna/`，报告与录像位于同目录根下的 `evidence/integrated/`；保留的 `combined-input-manifest.json` 记录组合版本输入哈希，`preservation-manifest.json` 记录交付文件哈希。
- 组合验证使用临时覆盖层，完成后已移除；原有改动将继续保留为主目录的未提交状态，`PlayPartners` 的 ID 匹配兼容调整归入该原有未提交改动。
