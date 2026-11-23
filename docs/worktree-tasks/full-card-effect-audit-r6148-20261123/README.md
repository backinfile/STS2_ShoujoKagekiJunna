# r6148 逐牌效果测试

## 用户输入

> 针对所有卡牌的效果创建测试条目，你来完整测试下所有卡牌的效果，并录制每张卡牌的主要测试视频。 $worktree-task

随请求提供的技能说明见下方。技能路径已用 `$worktree-task` 代替。

```markdown
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
```

## 计划

- 核对 r6148 启用牌清单和既有测试预期，先跑基线以辨别过期断言和实际牌效问题。
- 为每张牌记录基础与升级版、主要效果、触发条件、目标和可观察结果。补足涉及回合、胜利、生成、选择、飞矢和知识关联的断言。
- 在最小化、静默、独立的 Beta 游戏中执行逐牌用例并录像。逐个核对录像与测试的对应关系，保存可独立查看的索引和证据。
- 整理结果，提交并与原 `main` 同步后请用户确认是否合并及清理。

## 结果

基于 `main` 的 `b46b534`，使用《杀戮尖塔 2》v0.113.1 Beta 验证 r6148 的 98 张启用牌，包含初始牌、衍生牌与关联牌。

- 用例见 [逐牌测试条目](../../implementation/card-tests-r6148.md)。每张牌分别验证基础版和升级版，覆盖常规资源、空资源、机制组合和满手牌四种场景。
- 在独立游戏实例中执行真实出牌和效果回调，共完成 784 个场景、19,265 项断言，全部通过。修正了首轮暴露的旧版预期和测试准备问题，随后复测 47 张涉及目标选择、飞矢和条件分支的牌。
- 录制 98 段主要效果和 1 段「绝美的凋零」入战自动布置与自然完成的补充录像。17 段采用三敌人场景。录像、逐项结果、运行版本和校验记录归档于 `out/task-deliverables/card-effect-audit-r6148-20261123/`，从 `index.html` 可逐张查阅。
- 变更集中在测试用例、测试模组和验收工具，运行卡池仍为 r6148。

## 后续用户输入

合并确认答复

```json
[{"questionItemId":"[\"request_user_input_async\",\"call_7cbb006e473b4bf6b65a0519c1606a6d\",0]","question":"r6148 的 98 张牌已完成 784 个场景，19,265 项断言全部通过，98 段主要录像和 1 段补录已保存。测试条目和工具已提交为 `bf7c548`，原 `main` 干净。按 `$worktree-task` 约定，是否合并到 `main` 并清理本次工作树、分支和临时构建？归档录像会保留。","answer":"合并并清理（推荐）"}]
```
