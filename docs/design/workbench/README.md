# 纯那工作台

启动本地服务：

```powershell
powershell -File docs/design/workbench/start-mcp.ps1
```

打开 `http://127.0.0.1:17842/`。Codex 的项目 MCP 配置会通过 STDIO 自动启动并连接同一个后台服务。多个客户端共享条目、历史和版本。

网页读取、保存、导入、撤销、恢复和上传配图都调用 `/mcp`。每条内容的版本由递增序号及内容摘要组成；同一条目的旧版本提交返回 `VERSION_CONFLICT`，整个批次保持原状。不同条目可独立提交。网页草稿按客户端存入 IndexedDB，冲突时可查看服务器内容、编辑合并结果或导出草稿。重试沿用原提交编号，服务重启后仍能识别已完成的请求。

旧 `/file` 接口返回 `410 MCP_REQUIRED`。`start-local-save.ps1` 保留为兼容入口，转到 MCP 启动脚本。

知识牌编辑栏提供“删除一组”和“备用一组”，从组内任意一张牌可操作同组全部牌面。删除移入垃圾桶，备用可整组移回卡池，保留知识关系和发放数量，已删除成员保持原状；每次整组操作记录为一个事务，可一次撤销。原“删除”“移入备用”只操作当前单张牌。

常用工具：`workbench_status`、`workbench_query`、`workbench_get_entity`、`workbench_commit`、`workbench_history`、`workbench_restore`、`workbench_import_asset`、`workbench_export`。当前会话加载新工具前，可使用 `mcp-client.cjs`，它也通过 MCP 协议连接服务。

```js
const {callTool}=require('./mcp-client.cjs');
const card=await callTool('workbench_get_entity',{collection:'cards',id:'JH19'});
await callTool('workbench_commit',{
  requestId:require('node:crypto').randomUUID(),clientId:'card-review',label:'修改稍息备注',
  changes:[{collection:'cards',uid:card.uid,expectedVersion:card.version,updates:{notes:'...'}}]
});
```

源文件修改后运行 `npm run build`。必要的版本控制验证运行 `npm run test:mcp`；测试使用临时数据目录。

本地配置 `local-save.config.json` 包含连接密钥。后台日志为 `mcp.stdout.log` 和 `mcp.stderr.log`。服务仅监听本机地址。

“流派统计”以箭矢、知识、能量/高费为流派，伤害加成单列为小体系。数量建议分别显示当前数、建议数，差额直接显示“多X张”“少X张”或“一致”，当前比例和建议比例分别展示。同一张兼具牌分别计入触发牌和收益牌；每个体系内按牌面去重，跨体系分别计数。“仅奖励入口”同步调整基数，职责筛选保留完整体系的配比参照。建议权重保存在 `route-statistics-defaults.json` 的 `recommendations` 中，依据与快照见[触发收益推荐](../data/纯那流派触发收益推荐_20261103.json)。

箭矢流派仅打出箭矢牌计为触发。加速、额外触发、复制、转型与调度按工具或收益作用归类；监听打出箭矢、箭矢完成等事件取得回报的牌归为收益，箭矢牌自身兼有收益时归为兼具。

“流派统计”的“效果归类”逐项列出各体系的触发、收益、兼具与工具效果，并读取当前范围内卡牌的完整基础描述作为示例。归类内容保存在各流派条目的 `effectRoles`，通过 MCP 维护；流派筛选同步筛选归类表。

每个具体效果单独显示牌面数，点击数量查看匹配卡牌。数量遵循统计范围、仅奖励入口及流派位置筛选；同一牌面在每项内去重，不同效果分别计入。匹配读取基础描述、费用、卡牌类型与当前知识关系，不重复计算升级描述；按当前牌名识别引用。`effect-statistics.js` 保存匹配规则，流派 `effectRoles.effects` 中的 `{id,text}` 关联规则与显示文本。

打包当前内容时，可先通过 `workbench_export` 把快照导出到临时文件，再运行 `node build.cjs --snapshot <快照路径>`；默认构建继续读取初始 seed。

总览的“完整卡池参考”依据 `STS2_Card_Reserch/docs/职业卡池设计要求_按五职业规律.md` 第1、2、6节与 `体系驱动卡池设计流程.md` 阶段4，展示费用、稀有度、类型、体系规模及突破牌参考范围。参考按完整卡池的入池牌与基础牌统计，知识附带牌面、备用和垃圾桶另计；分组筛选不改变整池参考基数。类型比较时箭矢按已有内部子类型计入攻击、技能或能力，卡牌显示仍为箭矢。`pool-reference.js` 保存来源范围与计算规则，范围保持文档原值，不随草案张数放大。
