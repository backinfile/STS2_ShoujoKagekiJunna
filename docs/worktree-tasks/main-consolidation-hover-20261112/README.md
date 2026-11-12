# main 分支整理与敌人悬停预览修复

## 原始输入

完整输入及工作区约定见 [user-input.md](user-input.md)。用户已明确授权合并与清理，并要求保留主干最新内容；随后补充敌人头顶预览及 tips 遮挡问题。

## 简要计划

1. 捕获起始 main dc66cd5 及现有未提交文件，保存逐文件摘要、二进制补丁和全部分支 bundle。
2. 在 ../worktrees/STS2_ShoujoKagekiJunna/main-consolidation-hover-20261112 整合；先单独保存主目录现有修改，再合入文档、素材及药水工作台。旧代码逐项比对，以最新主干实现为准。
3. 先复现敌人悬停预览定位及 tips 相交，再做最小修复和定向回归、实机录像。
4. 所有旧工作树的 out/tmp、artifacts 与未提交任务文档先归档，逐文件核验；原生共享资源链接不递归复制。
5. 验证结果后快进 main，确认主目录改动均已保全，并清理已整合分支和工作树。

## 当前结果

已建立隔离工作树，原目录保持不变。分支及主目录快照保存在 artifacts/task-deliverables/main-consolidation-20261112/。旧工作树归档与整合进行中。
