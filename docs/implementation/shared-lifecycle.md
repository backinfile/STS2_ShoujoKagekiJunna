# 公共生命周期与生成元数据

Junna 要求 Core 1.2.0，使用 `card-snapshots@1` 与 `card-selection@1`。

持续能力的 `EffectData` 在安装时捕获原模型快照、数值、升级、X 值和消耗信息；各份 Grant 独立维护本回合计数。快照经 Core 创建，最后一次闪耀安装能力时仍保留原模型，来源耗尽后效果继续使用捕获的数据。临时实际复制使用 Core 战斗复制入口，保留费用、箭矢转换和投入，同时独立消耗闪耀。

`KnowledgeRelations` 统一动态知识链接、实体成员查询、序号与格式版本。获得静态伙伴组后统一关联实际获得的所有成员；整局恢复、玩家同步、开战和获得操作负责旧研究组链接归一化并推进序号。`Members` 和 HoverTips 只查询，不修复保存数据。既有 `JunnaKnowledgeLinks`、`JunnaKnowledgeGroupSequence` 与研究组保存键保持不变。

`data/card-rules.json` 定义语义名与原保存字段的绑定。`generate-card-catalog.py` 将 aliases、tips、upgradedTips 写入 `data/card-pool.json`；运行时读取类型化元数据。更新现有目录元数据使用 `--metadata-only`，随后执行 `test_card_metadata.py`。原始 N0 等保存字段、本地化、卡牌正文与数值保持不变。必需字段缺失会明确报错，需要回退值的规则须显式传入回退值。

最多三个选项的发现与选择共用 Core 原生选牌入口，仍会经过知识牌原生选牌挂钩。
