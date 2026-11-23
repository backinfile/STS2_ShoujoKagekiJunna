# 立下誓言的格挡标记修复

用户反馈「立下誓言」被附上灵巧 2 后，基础版显示获得 4 点格挡，并确认“合并，清理”。

## 原因与修改

`CatalogCard.GainsBlock` 原先只检查定义是否含有 `Block` 数值，因此把能力牌的持续收益误标为卡牌自身获得格挡。原生 `Nimble.CanEnchant` 按 `GainsBlock` 筛选，并明确排除先施加能力、之后再获得格挡的牌。

- 排除能力牌的 `GainsBlock` 标记，阻止灵巧选中「立下誓言」。
- 扩充 `PowerBlockPreviewProbe`，覆盖基础／升级版的附魔资格、数值预览和实际收益，以及烁星、星光结界的灵巧加成、脆弱与敏捷叠加对照。
- 已有存档中误附上的灵巧仍需清除。

## 验证

- Beta、Stable 与集成探针编译通过，0 警告、0 错误。
- 运行 `python tests/Integration/run-power-block-preview.py --port <随机端口>`：修改前 86 项中 6 项失败，基础／升级牌面被灵巧错误增加至 4／5；修改后 86／86 通过。[修改前结果](evidence/oath-nimble-tag-20261117/before.json)、[修改后结果](evidence/oath-nimble-tag-20261117/after.json)。
- 隔离实例采用随机端口、满解锁、最小化及静默音频；启动采样未获得前台焦点。
- 实机验证中，脆弱下基础版和升级版能力分别显示 2 和 3。叠加后打出飞矢实际获得 5 点格挡，灵巧 2 的普通防御再获得 5 点格挡，总计 10。录制期间只使用悬停与正常出牌。
- 录制时长 11.2 秒，含 AAC 游戏音轨。[录像元数据](evidence/oath-nimble-tag-20261117/recording.json)

清理范围为本轮 `artifacts/oath-nimble-20261117/`、`out/tmp/oath-nimble-20261117/` 及格挡专项的临时结果目录；本页所链接的结果与录像保留。
