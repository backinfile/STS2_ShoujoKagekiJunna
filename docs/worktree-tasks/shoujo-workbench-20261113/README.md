# 少女歌剧工作台维护迁移

## 用户原始要求

完整项目与全局约定原文见 [user-instructions.md](user-instructions.md)。

参考junna工作台，你来制作一个少女歌剧工作台，可以在工作台中切换mod 你来在core中维护，不在junna中维护了，然后重新在原本的位置部署

$worktree-task



### 后续用户输入

你也来写一个游戏本体的页（作为一个mod） 先整理下卡牌，遗物，还有先古之民（及其出现的规则，遗物列表）上去。 游戏本体页不允许修改

版本选择：当前测试版 v0.113.0

每个先古之民，你需要按三个选项的选项池来展示（可能有共用的遗物，允许重复展示）

## 简要计划

将 Junna 的工作台维护入口转到 Core，保留原 MCP 的纯那兼容连接与设计约定。

起始分支 main，提交 e6e288bf9080b6951549b6489cd847cf96e64bff。任务工作树为 ../worktrees/STS2_ShoujoKagekiJunna/shoujo-workbench-20261113，分支 codex/shoujo-workbench-20261113。原检出已有 docs/worktree-tasks/full-card-resolution-localization-20261105/selection-demo.mp4 的未提交删除，保持原状。

## 实际变更与验证

- AGENTS.md、README.md、docs/design/AGENTS.md 及旧工作台说明统一指出 Core 是工作台程序与部署维护源；保留纯那内容编辑约定、原 MCP 兼容客户端和历史基线。
- Core 已完成四工作区与 v0.113.0 本体只读资料，先古之民按三个选项池展示，原址部署及21项定向回归通过。详细实现与证据见 Core 同名任务记录。
- 原检出的 selection-demo.mp4 未提交删除保持原状；当前仅修改维护文档与任务记录。已检查文档差异和隐私，用户已明确确认合并与清理，按下方确认记录执行。

### 合并与清理确认

```json
[
  {
    "questionItemId": "[\"request_user_input_async\",\"call_03c20cdd452c4477b3aa84048b84a288\",0]",
    "question": "是否将 Core `c41fb85`、Junna `7b45e30` 合并到各自的 `main`？清理范围为本次两个工作树、任务分支和本地临时验证文件；部署包、原生导出、验证记录及线上回滚备份会保留。工作台已原址上线，21项定向回归及线上验收通过。",
    "answer": "合并并清理，保留交付物"
  }
]
```

用户已授权将本任务合并到两个仓库各自的 `main`，并清理本次两个工作树、任务分支与本地临时验证文件。部署包、原生导出、验证记录和线上回滚备份保留。最终合并、保留文件哈希和清理状态记录在 Core 原检出的 `artifacts/task-deliverables/shoujo-workbench-20261113/integration.json`。
