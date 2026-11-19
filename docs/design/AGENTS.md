# 工作台内容编辑

- 工作台程序与部署已迁入 [Core 仓库](https://github.com/backinfile/STS2_ShoujoKagekiAijoCore/blob/main/tools/workbench/README.md)。前端、MCP、存储和部署均在 Core 的 `tools/workbench` 维护。
- 使用 Core 的 shoujo_workbench MCP 时，每次调用指定 modId: "junna"；原 junna_workbench 客户端省略此字段时仍进入纯那。

- 卡牌、词条、机制、知识组、流派、风格约定、契合池及配图统一通过 Core 工作台 MCP 读取和修改。网页编辑也使用同一服务。
- 先 `workbench_get_entity` 取得内容及 `version`，再以 `expectedVersion` 提交 `workbench_commit`。每次提交使用新的 UUID `requestId`；网络重试沿用原请求及编号。
- 更新已有条目优先使用 `updates` / `unset`，并用 `expectedFields` 提供所改字段的读取时原值，原先缺失的字段列入 `expectedAbsent`。同一条目的不同字段可以自动合并；同一字段发生不同修改时返回冲突。创建、删除和整体替换继续使用 `before` / `after`。
- 版本冲突时保留草稿，读取返回的当前内容，合并后使用新版本和新请求编号提交。多个相关条目放在同一批中，整批校验并写入。
- 主工作台由 `vipcloud` 的 MCP 服务持久化，本地客户端连接同一远端。内容变更通过 MCP 提交；本地 JSON 和 seed 用于导出备份、初始数据及独立预览。源码修改仍使用普通文件工具。部署源码与路径见 [workbench/deploy/README.md](workbench/deploy/README.md)。
- 若当前环境尚未加载工作台 MCP，可在 Core 仓库运行 `node tools/workbench/mcp-client.cjs <工具名> <参数JSON文件>`，在参数中指定 `modId: "junna"`。
- Codex 的工作台 MCP 指向 Core 的 `tools/workbench/mcp-server.cjs`。页面地址由 `workbench_status` 返回。
