# r6148 逐牌测试复核补测

## 用户输入

> 分析下有无问题？

> 继续吧

```text
<send_user_message_question_reply>
[{"questionItemId":"[\"request_user_input_async\",\"call_8ef5aa18fb1641abbdcd2263c8dac3a6\",0]","question":"5 张牌的 40 个补测场景全部通过，5 段升级录像已归档，汇总误判已修正。按 `$worktree-task` 约定，是否将 `a6b3719` 合并到干净的 `main`，并清理本次工作树、分支和临时构建？","answer":"合并并清理（推荐）"}]
</send_user_message_question_reply>
```

```text
<skill>
<name>worktree-task</name>
<path>$worktree-task</path>
---
name: worktree-task
description: Use when completing a repository change in an isolated Git worktree, consolidating its task changes into one commit, and merging it back to the starting branch.
---

# Worktree Task

Complete the requested change in a temporary worktree, then integrate it safely into the branch that was current when the task began.

## Rules

- Capture the original checkout, branch, and revision once; never re-infer the integration target.
- Refuse an active Git operation. If the original checkout is dirty, warn that its uncommitted changes will not enter the worktree, preserve them untouched, and proceed from the current branch's latest commit by default.
- Place the worktree at `../worktrees/<projectName>/<task-slug>` relative to the repository root, unless repository instructions override it.
- Perform all modification and verification work inside the worktree.
- Keep a task document under `docs/worktree-tasks/<task-slug>/` inside the worktree and include it in the task commit, so it survives integration and worktree cleanup.
- Sanitize every document tracked by Git or being added to Git, including existing unchanged documents. Replace sensitive data such as private personal details and credentials with descriptive placeholders while preserving meaning.
- Use repository-relative paths in persisted documents and express worktree locations relative to the repository root. Never persist local absolute paths, including in quoted user input or Markdown links. Use descriptive placeholders for paths outside the repository and `$skill-name` for local skill references. Tool calls and safety checks may use absolute paths internally.
- Do not perform remote operations or rewrite existing original-branch history unless explicitly requested.
- For merge and cleanup confirmation, use `request_user_input_async` when available and permitted; otherwise ask in chat. Ask one short, direct question with the action choices. One explicit confirmation may approve both merging and the listed cleanup; the original task request alone is not approval.
- Preserve the task branch and worktree whenever a safety check or integration fails.

## Task Document

Use `docs/worktree-tasks/<task-slug>/README.md` by default, with the same task slug as the worktree. Create it before implementation. Keep the input record complete and the plan and results concise:

- **Original user input:** Record the full text of every task-related user message in chronological order, including introductory guidance, skill invocations, constraints, and follow-up corrections. Preserve the original wording and formatting, applying only the sanitization above. Append later messages in full.
- **Brief task plan:** Record the intended approach and main steps; update it when the approach changes.
- **Result and changes:** After implementation, summarize the actual completion status, changed files and behavior, verification results, and any remaining work or limitations. If work stops early, record partial progress accurately.

Before each commit, check the input record against the user messages, finalize the result section, and review all Git-tracked documents and documents being added for sensitive data and local absolute paths, including quoted input and link targets. If synchronization changes the implementation or verification results, update the document in the synchronization commit. Preserve the document with the task changes and include its path in the final report.

## Workflow

1. Check the repository state and refuse conflicting worktree paths or branches. Record any original uncommitted state so it can be protected and verified later.
2. Create a uniquely named task branch and worktree from the captured original branch's latest commit. Create the task document with the sanitized user input and brief plan.
3. Establish a clean baseline, implement the task, and verify it. Complete the task document's result section, then consolidate all task changes, including the document, into one commit before synchronization unless the user or repository policy requires multiple commits.
4. Merge the latest local original branch into the task branch. Resolve conflicts only in the worktree, then verify and commit the synchronized result.
5. Confirm the original checkout is still on the captured branch, its committed HEAD is unchanged since synchronization, and any pre-existing uncommitted state remains intact. If HEAD advanced, repeat synchronization; stop after three races.
6. Show the target branch, task commit, change summary, verification results, any original uncommitted state, and proposed cleanup items once; reuse this summary if already shown. Ask directly whether to merge and clean up, merge and keep, or defer.
7. Only after confirmation, merge the synchronized task branch back without creating an additional integration commit when possible. If integration would overwrite uncommitted state, preserve the worktree and report that integration is pending instead.
8. Verify the committed trees match, checks pass, the original branch contains the consolidated task commit, and pre-existing uncommitted state was preserved.
9. After successful integration verification, remove this run's clean worktree, task branch, and disposable temporary files covered by the cleanup approval, after proving needed content is preserved. Reuse combined approval from step 6 without asking again. If only merging was approved, retain these items until the user approves cleanup; request further confirmation for any additional cleanup items.

Report the resulting branch, commit, verification, cleanup state, and task document path.

</skill>
```

## 计划

- 复现报告的空断言误判，补上单次运行与版本校验。
- 为遥远星辰、相信我们、读些什么、系住梦想的纽扣和星屑补齐遗漏的断言。
- 在独立 Beta 实例中验证上述卡牌并录制关键分支，保存结果后确认合并与清理。

## 结果

起点为干净的 `main`，提交 `87b9f52`。

- 逐场景结果写入独立的运行目录。汇总核对运行标识、卡池版本、完成范围和非空断言。8 项报告检查通过，其中 784 份空断言文件、错轮次和错版本数据均被拒绝。
- r6148 在 v0.113.1 Beta 中定向复测 5 张牌，基础和升级版各跑 4 个变体，40 个场景、1,174 项断言全部通过。覆盖遥远星辰的目标筛选、相信我们的 3／5 张上限及零张确认、读些什么的 1／2 张原件、系住梦想的纽扣的三回合延迟及星屑的首次伤害和衍生。
- 录制 5 段升级版实机录像，并校验全部音视频。纽扣录像通过三个自然回合，星屑录像中升级来源和两张基础衍生合计造成 28 点伤害。录像和结果索引归档于 `out/task-deliverables/card-audit-followup-r6148-20261123/index.html`。
