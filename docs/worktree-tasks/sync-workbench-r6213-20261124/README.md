# 同步纯那工作台 r6213

## 用户原始输入

> [$worktree-task]($worktree-task)  将最新工作台卡池同步到mod

> <skill>
> <name>worktree-task</name>
> <path>$worktree-task</path>
> ---
> name: worktree-task
> description: Use when completing a repository change in an isolated Git worktree, consolidating its task changes into one commit, and merging it back to the starting branch.
> ---
>
> # Worktree Task
>
> Complete the requested change in a temporary worktree, then integrate it safely into the branch that was current when the task began.
>
> ## Rules
>
> - Capture the original checkout, branch, and revision once; never re-infer the integration target.
> - Refuse an active Git operation. If the original checkout is dirty, warn that its uncommitted changes will not enter the worktree, preserve them untouched, and proceed from the current branch's latest commit by default.
> - Place the worktree at `../worktrees/<projectName>/<task-slug>` relative to the repository root, unless repository instructions override it.
> - Perform all modification and verification work inside the worktree.
> - Keep a task document under `docs/worktree-tasks/<task-slug>/` inside the worktree and include it in the task commit, so it survives integration and worktree cleanup.
> - Sanitize every document tracked by Git or being added to Git, including existing unchanged documents. Replace sensitive data such as private personal details and credentials with descriptive placeholders while preserving meaning.
> - Use repository-relative paths in persisted documents and express worktree locations relative to the repository root. Never persist local absolute paths, including in quoted user input or Markdown links. Use descriptive placeholders for paths outside the repository and `$skill-name` for local skill references. Tool calls and safety checks may use absolute paths internally.
> - Do not perform remote operations or rewrite existing original-branch history unless explicitly requested.
> - For merge and cleanup confirmation, use `request_user_input_async` when available and permitted; otherwise ask in chat. Ask one short, direct question with the action choices. One explicit confirmation may approve both merging and the listed cleanup; the original task request alone is not approval.
> - Preserve the task branch and worktree whenever a safety check or integration fails.
>
> ## Task Document
>
> Use `docs/worktree-tasks/<task-slug>/README.md` by default, with the same task slug as the worktree. Create it before implementation. Keep the input record complete and the plan and results concise:
>
> - **Original user input:** Record the full text of every task-related user message in chronological order, including introductory guidance, skill invocations, constraints, and follow-up corrections. Preserve the original wording and formatting, applying only the sanitization above. Append later messages in full.
> - **Brief task plan:** Record the intended approach and main steps; update it when the approach changes.
> - **Result and changes:** After implementation, summarize the actual completion status, changed files and behavior, verification results, and any remaining work or limitations. If work stops early, record partial progress accurately.
>
> Before each commit, check the input record against the user messages, finalize the result section, and review all Git-tracked documents and documents being added for sensitive data and local absolute paths, including quoted input and link targets. If synchronization changes the implementation or verification results, update the document in the synchronization commit. Preserve the document with the task changes and include its path in the final report.
>
> ## Workflow
>
> 1. Check the repository state and refuse conflicting worktree paths or branches. Record any original uncommitted state so it can be protected and verified later.
> 2. Create a uniquely named task branch and worktree from the captured original branch's latest commit. Create the task document with the sanitized user input and brief plan.
> 3. Establish a clean baseline, implement the task, and verify it. Complete the task document's result section, then consolidate all task changes, including the document, into one commit before synchronization unless the user or repository policy requires multiple commits.
> 4. Merge the latest local original branch into the task branch. Resolve conflicts only in the worktree, then verify and commit the synchronized result.
> 5. Confirm the original checkout is still on the captured branch, its committed HEAD is unchanged since synchronization, and any pre-existing uncommitted state remains intact. If HEAD advanced, repeat synchronization; stop after three races.
> 6. Show the target branch, task commit, change summary, verification results, any original uncommitted state, and proposed cleanup items once; reuse this summary if already shown. Ask directly whether to merge and clean up, merge and keep, or defer.
> 7. Only after confirmation, merge the synchronized task branch back without creating an additional integration commit when possible. If integration would overwrite uncommitted state, preserve the worktree and report that integration is pending instead.
> 8. Verify the committed trees match, checks pass, the original branch contains the consolidated task commit, and pre-existing uncommitted state was preserved.
> 9. After successful integration verification, remove this run's clean worktree, task branch, and disposable temporary files covered by the cleanup approval, after proving needed content is preserved. Reuse combined approval from step 6 without asking again. If only merging was approved, retain these items until the user approves cleanup; request further confirmation for any additional cleanup items.
>
> Report the resulting branch, commit, verification, cleanup state, and task document path.
>
> </skill>

> 打包一个junna+core的发布包，并安装到本地

> 合并到 main 并清理任务分支和工作树

## 计划

1. 在独立工作树取得工作台卡池并比对本机已提交的运行卡池。
2. 同步改动的卡池、牌面和所需的运行实现，执行定向验证。
3. 将通过验证的 Junna 和 Core 打包，备份本地已安装版本后安装。
4. 提交并同步本地 main，确认后合并。

## 结果

- 将内嵌卡池从 r6196 同步到 r6213，与工作台核对了 97 张启用卡牌及 31 个知识组。提交前复查工作台 revision 仍为 r6213。
- 星屑基础与升级均为 2 费，伤害分别为 25 和 30。完成后免费打出两张独立的自身复制。参考本体双持的复制方式，使用 Core 的战斗复制接口保留升级和当前伤害，并沿用原目标及完整的 3 回合倒计时。
- 超常发挥升级版改为 2 费并获得保留。同步星屑牌面、独立卡池预期和逐牌用例。
- Core 2.0.4 下，Stable、Main、Beta、加载器和 PCK 构建通过。Beta v0.113.1 隔离实机对两张牌的基础和升级版执行 16 个场景、474 项断言，全部通过。构建产物与实机中的 8 个 Junna 文件逐一核对 SHA-256。两张升级牌的实机录像检查后按约定删除。
- 发布包为 `dist/Junna-r6213-Core-2.0.4-20261124.zip`，已安装到 Steam v0.113.1 的 `<game-install>/mods`。包内与安装目录的 20 个文件逐一通过 SHA-256 校验。旧版备份位于 `<game-install>/mod-backups/Junna-Core-before-r6213-20261124-164127`。
- 原 `main` 从 `d0c6f04` 建立工作树，已有的未跟踪 Godot UID 文件保留在原检出。
