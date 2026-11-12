# 分支整合决策

整合起点为 main dc66cd5；主目录未提交选人画面与工作台整组操作先独立保全为 416569b。所有旧分支完整提交保留于合并历史，原始 bundle 与工作树证据归档于 artifacts/task-deliverables/main-consolidation-20261112/。

| 分支 | 处理 |
| --- | --- |
| card-count-subsystems-20261110 | 合入设计提案与记录。 |
| junna-a0-playtest-20261107 | 合入报告、截图及结算数据。 |
| character-select-cool-variants-20261105 | 合入全部四套候选、原图及清理记录。当前选人场景不替换。 |
| junna-potions-workbench-20261111 | 合入药水页签、导航、测试及素材；与现有整组备用修复自动合并并定向验证。 |
| arrow-ui-review-20261110 | 归档完整任务记录与历史；原生扇形、逐帧预热和池化归还已由 arrow-play-speed-20261111 转入 main。保留最新同状态星屑分组和行宽排布，不恢复旧 DisplayTargets 和旧布局。 |
| junna-a0-playtest-20261109 | 合入报告和证据。保留 main 的 IsRewardCard（同时过滤 ChoiceOnly）、KnowledgeDamageVar 战斗外预览及 JunnaArchitectDialogue 原生对话别名；旧的同目的 Harmony 前缀不重复安装。 |
| copy-lifecycle-refactor-20261108 | 归档完整记录及提交。保留 main 的 Core.CardCopies.Snapshot、公共同步选择和新的全局模型 ID 知识关系；不恢复旧物理牌 Links 关系或旧 card-pool/card-rules。 |
| full-card-resolution-localization-20261105-before-consolidation | 历史已压缩为 main 的 d47bc1a 等提交；合并旧历史，保留当前完成度探针、运行卡池和本地化。 |

药水运行实现工作树仍有活动写入，待该任务完成后复核；不能把起点与 main 相同误认为药水已实现。选人画面任务仍在写入主目录，最终切换前再次核对完整差异和摘要。
