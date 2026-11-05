# 工作台内容编辑

- 卡牌、词条、机制、知识组、流派、风格约定、契合池及配图统一通过 `junna_workbench` MCP 读取和修改。网页编辑也使用同一服务。
- 先 `workbench_get_entity` 取得内容及 `version`，再以 `expectedVersion` 提交 `workbench_commit`。每次提交使用新的 UUID `requestId`；网络重试沿用原请求及编号。
- 版本冲突时保留草稿，读取返回的当前内容，合并后使用新版本和新请求编号提交。多个相关条目放在同一批中，整批校验并写入。
- 主工作台由 `vipcloud` 的 MCP 服务持久化，本地客户端连接同一远端。内容变更通过 MCP 提交；本地 JSON 和 seed 用于导出备份、初始数据及独立预览。源码修改仍使用普通文件工具。部署源码与路径见 [workbench/deploy/README.md](workbench/deploy/README.md)。
- 若当前客户端尚未加载新 MCP 工具，可用 SDK 客户端：`node docs/design/workbench/mcp-client.cjs <工具名> <参数JSON文件>`。参数文件仅作为请求输入。
- 启动：`powershell -File docs/design/workbench/start-mcp.ps1`，或由 Codex 按项目 `.codex/config.toml` 自动启动。页面地址由 `workbench_status` 返回。
