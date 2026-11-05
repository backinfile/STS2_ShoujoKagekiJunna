# 工作台编辑

工作台内容统一使用项目 MCP `junna_workbench` 读取和修改，逐条携带读取时的 `expectedVersion` 提交。具体流程见 [docs/design/AGENTS.md](docs/design/AGENTS.md)。

当前会话尚未加载新工具时，使用 `docs/design/workbench/mcp-client.cjs` 连接同一个 MCP 服务。源码修改仍使用普通文件工具；本地编辑数据由 MCP 服务持久化。

# 图片素材处理

所有 AI 生成图片在交付前统一使用本地工具进行去摩尔纹与生成伪影处理。优先复用项目已安装的 Artifact Cleaner，按原尺寸对比处理前后，检查眼镜、发丝、弓弦和服装边缘；保留生成原图，并记录本地处理工具、参数与成品路径。
