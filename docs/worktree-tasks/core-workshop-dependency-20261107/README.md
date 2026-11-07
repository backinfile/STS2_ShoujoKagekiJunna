# Core 创意工坊依赖

## Original user input

> $worktree-task core项目之后会发布到创意工坊，当前项目的引用应该修改为创意工坊引用，不要直接包含了

## Brief task plan

- 原始分支：`main`；起始提交：`a988a0d7adaf0de680e69b15420520b7b08df778`。
- 工作树：`../worktrees/STS2_ShoujoKagekiJunna/core-workshop-dependency-20261107`；任务分支：`codex/core-workshop-dependency-20261107`。
- 保留主目录已有未提交改动，核对游戏清单、工坊配置和 Core SDK 打包契约。
- 将 Junna 改为独立 Core 工坊依赖，移除发布包内的 Core 分发，并同步必要安装说明和开发脚本。
- 执行本次改动必要的定向验证，整理为一个提交，同步原始分支后请求合并确认。

## Result and changes

- 已完成实现，等待用户确认合并到 `main`；任务分支与工作树保留。
- `tools/core-package.ps1` 与 `build.ps1`：编译仍校验官方 Core SDK；按 V1 契约直接生成 `core.requirements` 与 `core.package.lock`，兼容仍会内嵌 Core 的旧 SDK，清理旧构建留下的 Core 目录。Junna 的发布内容只包含自身目录。
- `tools/prepare-workshop.ps1`：必填独立 Core 数字工坊 ID，生成 `workshop.json`，必需物品为 BaseLib `3737335127` 和 Core；`content/` 只复制 Junna。支持预览图和现有 Junna 工坊 ID。
- `tools/dev/start-test-game.ps1`：开发环境通过 `-CorePackage` 或 `SHOUJO_CORE_PACKAGE` 单独安装 Core。
- 已同步 `AGENTS.md`、`README.md`、`tools/README.md` 和 `docs/implementation/architecture.md` 的构建与安装说明；游戏清单中的 Core 最低版本保持 `1.0.1`，编译引用继续关闭复制本地。
- `tests/PackagingChecks.ps1`：使用现有 Core 1.0.1 发行包执行 18 项离线定向检查，全部通过，覆盖元数据、旧 Core 清理、独立工坊依赖、仅 Junna 上传内容、预览及错误路径。测试使用离线角色文件夹夹具，报告为 `out/tmp/core-workshop-checks-8fab6f03732846d4bcc32c0f9c11c4f6/results.json`。
- 修改涉及的 5 个 PowerShell 入口语法检查通过，`git diff --check` 通过；文档敏感信息与本机绝对路径检查通过。
- 主目录原有 13 项未提交或未跟踪状态及逐文件 SHA-256 与任务开始时一致；未提交的 README、构建及游玩入口修改继续保留，预检发现启动脚本的新增参数与原有 `Playable` 参数冲突；合并预览已在 `out/tmp/integration-preview/` 中保留两者，并让原有 `play.ps1` 转发 Core 来源、在跳过构建时同样解析默认 Core 目录。两份预览语法检查通过；待确认合并时应用，主目录当前保持原样。
- Core 尚未提供正式工坊 ID，发布准备时传入该 ID 即可生成实际依赖。工作区布局及工坊依赖字段依据 [官方 ModUploader 工作区说明](https://github.com/megacrit/sts2-mod-uploader/blob/main/template/README.md)。
