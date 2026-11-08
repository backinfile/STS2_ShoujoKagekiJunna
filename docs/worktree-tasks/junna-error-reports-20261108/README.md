# Junna 后台报错排查与修复

## Original user input

### 工作区约定

用户提供的完整工作区约定原文保存在 [user-instructions.md](user-instructions.md)，本机路径已替换为用途占位符。

### 用户消息 1

> 你看看后台有收到junnamod的报错了吗

### 用户消息 2

> 你来全部分析处理下 worktreetask

## Brief task plan

1. 读取线上报错完整调用栈和环境，将直接 Junna 异常、共存环境异常及历史已修复问题逐项归档。
2. 建立命中实际故障调用路径的定向复现，对当前仍存在的缺陷做最小修复。
3. 执行受影响的编译、回归和必要实机验证，录制效果；整理每条报错处理结论。
4. 完成文档、隐私检查及单一任务提交，同步本地 main 后提交具体合并与清理选项。

## Result and changes

- 原始分支：main；原始 HEAD：e785c3815c8a0417a55bc3ae53f539a5ff5018e4。
- 隔离分支：task/junna-error-reports-20261108。
- 工作树：../worktrees/STS2_ShoujoKagekiJunna/junna-error-reports-20261108。
- 原始主目录含已有未提交改动，保持原样；任务从已提交 HEAD 开始。
- 32组、39条报告、44次触发已分为15类逐项记录，见 [error-analysis.md](error-analysis.md)。
- 修复 `HandArrow.cs` 的 RitsuLib 0.6.10 出牌边界兼容；普通牌沿传入委托执行，箭矢在牌效边界部署，保留第三方前后监听。
- 修复 `tools/core-package.ps1` 在 Windows PowerShell 5.1 中调用不存在的 `Path.GetRelativePath` 导致构建失败，改用已知包根目录截取相对路径。
- 增加 `tests/CompatibilityChecks` 的10项真实 IL 检查和 `tests/CompatibilityProbe` 的7项实机检查。
- 双实现、加载器及资源包构建通过；原版及联用出牌/命中边界、卡面、续玩和首战通过，实测箭矢伤害25、普通攻击伤害6。视频已交付。
- Core 1.0.1 单独运行可复现预载线程池错误；Core 1.0.2 独立及与 Junna 联用的对照无错误。强制绘制探针的完整联用日志仍有1次，保留为预载调查项，未宣称彻底修复。
- 按 skill 检查全部跟踪文档，发现原有模拟配置含本机来源路径，改为对主目录等价的仓库相对路径。
- 原目录72个已有改动文件的SHA256均与任务开始一致，main HEAD保持原值。原目录 `HandArrow.cs` 有未提交改动，集成前需要协调并保留。
- 分支和工作树保留，待按 $worktree-task 确认集成和清理。
## 集成准备

- 当前完成状态：Junna 源码修复和全部32组报错分析已完成；依赖/预载阶段的单次线程池错误仍保留调查记录。
- 合并目标固定为原始 main；主目录同文件的未提交修改需要先协调。
