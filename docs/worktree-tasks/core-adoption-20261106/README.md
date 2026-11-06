# Core 接入任务

## Original user input

> Junna任务从 <junna-source-checkout> 的main创建slug core-adoption-20261106，按../worktrees/STS2_ShoujoKagekiJunna/定位。
> 角色原目录已有其他未提交工作，创建前自行记录实际状态并保护。只能在各工作树修改。主工作台内容通过MCP读取，当前任务不改工作台卡牌设计。
> Junna接入共享闪耀声明、保存/复制/牌堆/表现与耗尽回调。箭矢调度、倒计时和专属表现保留在Junna，统一执行入口接入Core实际结算协议。原本箭矢和“作为箭矢打出”的其他闪耀牌均在每次实际结算扣次数，原有重放循环在批次安全收尾后耗尽，避免普通出牌钩子重复计次。按当前实际模型/生成器/已有工作台已确认数据接入，不把仍在设计阶段的候选内容任意加入正式牌池。

## Brief task plan

通过工作台 MCP 核对已确认闪耀规则与实际模型。为箭矢实际执行和重放接入 Core 执行器及批次收尾，接入声明、保存与耗尽回调。接通双版本及随包构建，验证箭矢计次。

### Captured original state

- 原始检出 <junna-source-checkout>。
- 原分支 `main`，HEAD `338c8d2e7d69d9eb752c2adf6e98ac44d999b52c`。
- 任务分支 `task/core-adoption-20261106`，工作树 `../worktrees/STS2_ShoujoKagekiJunna/core-adoption-20261106`。
- 原有未提交状态 11 个文件，文件摘要如下。原目录保持原样。

- `docs/design/workbench/knowledge-display.test.cjs`，SHA256 `F3BA73491AF71FD83D45B1E43A3DA5418EEB18C560B9F6E2DEC07EED54A13BAC`。
- `docs/design/workbench/mcp-sqlite.cjs`，SHA256 `AA625EC0EFDEC7618DBAC33564870F19F67A60C3A995D72877367EBBE6771608`。
- `docs/design/AGENTS.md`，SHA256 `FA67E0F37ECB7F0D1E689C9A1074EF1D5C25B22909BB48FC7D94C23DABD7EA13`。
- `docs/design/workbench/app.js`，SHA256 `737C71869CF8EA46711EEF27A34C51B963A329089B64119947E8593CACAC448E`。
- `docs/design/workbench/mcp-store.cjs`，SHA256 `D8ADD3C25D44015FA4C7A4C75D47172768099697EC00DF1B004633D2E29C7B25`。
- `docs/design/workbench/mcp-store.test.cjs`，SHA256 `2FCF47AC616BD2005280710E8A6D32B580BD88B9EE0B986B1EECE7DEB66951D9`。
- `docs/design/workbench/mcp-tools.cjs`，SHA256 `AC9A00AEDFAA37AC47A982FF1335BF6F2B15F58B11EBE6088275F9E28A1701DD`。
- `docs/design/workbench/mcp-web-client.js`，SHA256 `58C27431D8D7DDC573782627A3B7C8E0FC0293AFFE249B6726E130890AAF10AE`。
- `docs/design/workbench/mcp-web-client.test.cjs`，SHA256 `1E1435762E11CDB4EFF07819BD1019EC5EC5766F3D78CEC0BA2C88936DC4C107`。
- `docs/design/workbench/workbench-domain.js`，SHA256 `1A3023F98E2EE3BCB7E155E5D2747361650BFCD892238468481D08D5B1B4F903`。
- `docs/design/workbench/workbench-domain.test.cjs`，SHA256 `A52A879C1AA2DD1151C9936BB0D4C6C4EE75054998F849F21B3FF2B2FE80E0EA`。

## Result and changes

箭矢实际 payload 使用 Core typed ResolveAsync，提供选择上下文和逐次重放序号，批次覆盖部署清理与结果牌区收尾。注册延迟结算判定，部署和等待保持闪耀预算，实际结算分别扣次。

生成器支持明确次数的 ShineCard 声明，保留当前工作台与运行牌池。正常入口最终双版本 DLL/PCK 与加载器构建零警告、零错误，官方 SDK 生成要求及锁文件，两角色携带的 Core 逐文件哈希一致。

测试版真实双角色集成 20 项断言通过，覆盖原生出牌、三次箭矢重放、回调、复制、空壳与重获。最终组合包正常出牌与箭矢部署录像完成，最终上传历史携带 Junna/本体来源、Core/API 版本及游戏目标。完整矩阵与性能专项需用户另行同意。

原目录工作台工作已由其他任务提交为 2fdb716，main 干净，本任务将同步该提交后申请合并。原分支合并与工作树清理分别等待统一确认。

完整暂存索引隐私检查中发现 verify.cjs 与开发启动脚本的个人目录，已改为运行时用户目录解析，Node 与 PowerShell 语法检查通过。
