# 云端工作台部署

`vipcloud` 运行 `junna-workbench.service`，Nginx 为 `https://junna.17181718.xyz/` 提供只读页面，独立长链接转发到服务器回环地址 `127.0.0.1:54828`。编辑入口、MCP 和公开快照均使用服务器的同一份数据。运行所需的源码在此目录及上级工作台目录；生成包将后端模块与这三个运行脚本放在同一目录。

| 路径 | 用途 |
| --- | --- |
| `/opt/junna-workbench/current` | 当前程序发布目录的符号链接 |
| `/var/lib/junna-workbench/state.json` | 唯一主台数据，包含配图、历史、条目版本和请求回执 |
| `/var/lib/junna-workbench/backend.json` | 内部 MCP 的 `port`、`token`，当前端口 54829 |
| `/var/www/junna/current` | 当前公开只读快照的符号链接 |
| `/var/www/junna/assets` | 按内容摘要命名的不可变公开资产 |
| `/etc/nginx/conf.d/junna-https.conf` | 只读站点与长链接入口配置 |

`relay.cjs` 仅连接本服务器的内部 MCP；`run-server.cjs` 从持久化路径加载主台；`publish-readonly.cjs` 在提交产生新 revision 后更新公开快照。数据和内部连接配置独立于程序发布目录。更新程序时继续使用已有数据和密钥。

本地 `docs/design/workbench/local-save.config.json` 的 `remoteURL` 是编辑长链接去掉末尾 `/`，`remoteToken` 是长链接密钥。SDK 连接地址为 `remoteURL + '/mcp'`。项目 `.codex/config.toml` 指向主目录的 MCP 客户端；其他工作树可复用主目录客户端，或仅复制这两个连接字段。私有连接信息和导出快照保存在 Git 忽略的 `artifacts/` 中。

仓库中的历史工作台 JSON 保留为迁移前的离线基线；主目录迁移前文件保留原样，当前主台以云端数据为准。任务工作树的离线内容差异，在对应增量已提交云端且保留 `workbench-changes.json`、提交回执后，可将该文件恢复至本工作树 HEAD。后续构建读取云端导出的快照，新增内容通过 MCP 提交。

构建部署包时，先从当前远端 MCP 读取完整快照并保存到本地请求输出目录，按 `connection.example.json` 准备私有连接文件，然后执行：

```powershell
node docs/design/workbench/deploy/build-package.cjs artifacts/cloud/snapshot.json artifacts/cloud/package artifacts/cloud/connection.json
```

`JUNNA_DEPLOY_SOURCE` 可指定已审阅的工作台源码目录。整个前端和后端使用这同一目录中的类型校验与展示代码。部署以“箭矢”为规范名称，并兼容曾导出的“星矢”类型及旧浏览器筛选值。运行脚本只读取服务器持久化数据；构建时的快照用于生成页面内置内容，页面连接成功后读取主台当前版本。

部署包包含私有 `relay.config.json` 和编辑页，应通过 SSH 上传到程序发布目录。在该目录运行 `npm ci --omit=dev --ignore-scripts --no-audit --no-fund`，保留已有 `state.json` 和 `backend.json`。将 `public/assets/` 合入共享资产目录，设置 `www-data` 对数据、共享资产、`/var/www/junna` 和其 `releases/` 目录的写权限；程序目录由 `root` 持有，私有配置限制为服务组可读。安装本目录的 systemd 单元并在协作切换窗口更新程序链接、重启服务。首次初始化的数据应来自主台 MCP 导出。

当前 HTTPS 配置将长链接前缀下的 `/mcp` 转发到内部网关，并为该路径允许 `connect-src 'self'`；公开主页保持 `connect-src 'none'`。长链接路径关闭访问日志并发送 `Referrer-Policy: no-referrer`。网关开放八个条目查询、读取、提交和恢复工具；文件系统路径工具保留在内部 MCP，远端客户端可用 `workbench_get_state` 读取快照。

`nginx-edit-location.conf.template` 对应当前编辑入口；将 `__EDIT_PREFIX__` 替换为私有配置中的 `prefix`，放入现有 HTTPS server 块，并在 `nginx -t` 通过后重载。公开站点的现有静态路径与 TLS 配置继续沿用。

本轮定向验证覆盖公开只读、长链接访问、SDK 初始化、旧版本冲突、空提交及请求重试。6 条后续增量已在 r2598 通过远端客户端提交，公开快照随 revision 更新。
