# 工作台编辑

工作台内容统一使用项目 MCP `junna_workbench` 读取和修改，逐条携带读取时的 `expectedVersion` 提交。具体流程见 [docs/design/AGENTS.md](docs/design/AGENTS.md)。

当前会话尚未加载新工具时，使用 `docs/design/workbench/mcp-client.cjs` 连接同一个 MCP 服务。源码修改仍使用普通文件工具；本地编辑数据由 MCP 服务持久化。
