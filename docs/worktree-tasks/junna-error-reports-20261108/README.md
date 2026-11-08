# Junna 后台报错排查与修复

## Original user input

### 工作区约定

用户提供的完整工作区约定原文保存在 [user-instructions.md](user-instructions.md)，本机路径已替换为用途占位符。

### 用户消息 1

> 你看看后台有收到junnamod的报错了吗

### 用户消息 2

> 你来全部分析处理下 worktreetask

### 用户消息 3

```xml
<send_user_message_question_reply>
[{"answer":"协调后合并并清理（推荐）","question":"是否将 2547b3f 合并到 main？会先协调并保留已有修改；清理仅限本任务分支和工作树，验证记录与录像另行保留。完整组合中的单次原生预载错误仍保留调查记录。","questionItemId":"[\"request_user_input_async\",\"call_ad476960d62940aebaf4416ff891eed9\",0]"}]
</send_user_message_question_reply>
```

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
- 实现提交前，原目录72个已有改动文件的SHA256均与任务开始一致。用户批准协调后，保留71个文件的原字节，合并 HandArrow 的既有自动转向与本次原委托调用；原有暂存/未跟踪状态清单不变。
- 用户已明确批准协调后合并并清理；修复提交 `2547b3f` 已快进进入 main。最终文档提交同步后，按授权删除本任务分支与工作树，保留验证记录。
## 集成与保留记录

- 当前完成状态：Junna 源码修复和全部32组报错分析已完成；依赖/预载阶段的单次线程池错误仍保留调查记录。
- 合并目标为原始 main，已完成快进集成，未创建额外的合并提交。
- 已在隔离副本编译包含主目录全部现有源码的协调结果：Beta、Stable 均为0警告、0错误，10项真实 RitsuLib 边界检查通过。
- 主目录 HandArrow 的自动转向仍作为未提交差量保留；普通牌通过原委托执行前同样解析可用目标。
- 验证录像、原始报错、日志、文件校验、原 HandArrow 备份和协调结果保存在 `artifacts/task-deliverables/junna-error-reports-20261108/`，清理工作树后继续可用。
- [保留文件校验清单](../../../artifacts/task-deliverables/junna-error-reports-20261108/manifest.json)；[验证录像](../../../artifacts/task-deliverables/junna-error-reports-20261108/video/junna-compatibility.mp4)。
