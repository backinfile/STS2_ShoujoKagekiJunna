# 删减连锁知识提示

## 用户输入

~~~~text
[$worktree-task]($worktree-task)  &#x20;

### 连锁知识

获得连锁知识时，同时获得同一选牌界面的其他牌，并将它们设为关联牌。战斗中，这些牌进入同一牌堆，关联持续到战斗结束。  后半“战斗中，这些牌进入同一牌堆，关联持续到战斗结束。” 你又多余了
~~~~

~~~~text
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
~~~~

~~~~text
看看其它关键字，卡牌文本等
~~~~

~~~~text
“获得连锁知识时，同时获得同一奖励界面的其他牌，并将它们设为关联牌。”
~~~~


~~~~text
你给出当前的文本我对比下
~~~~

~~~~text
游戏本体有 	两句合成「给予9层易伤和虚弱。」 这种吗？
~~~~

~~~~text
还需要遵守游戏本体的风格
~~~~

~~~~text
推荐修改结果呢
~~~~

~~~~text
放一起对比下
~~~~

~~~~text
知识的关联牌中有自己，所以不要遮盖了，其他按推荐改吧
~~~~


## 计划

先修订连锁知识提示，再审核其他玩家文案并对照游戏本体同类牌。按用户确认更新游戏资源、卡池文本、生成器和工作台条目，核对两种语言的资源与定向生成结果。

## 结果

- 连锁知识改用「同一奖励界面」，删除第二句。英文本地化与两处现有探针断言已同步。
- 飞矢提示写明剩余回合数降为0时触发效果。「去寻找故事吧」去掉「朋友！」后的句号，「牢笼」写明攻击伤害，两张牌的基础、升级及能力提示同步。「失势」的知识背景保留剧情。
- 工作台在 r6213 原子保存了 10 个词条、机制、卡牌和能力条目。回读核对通过，闪耀的计数细节保留在机制规则内。
- 「知识」的关联牌列表可含自身，提示继续使用「关联的其他牌」。「拔去獠牙」以本体「腐败」「何人僭越」的分行方式为参照，保留现有牌面。
- 本地化 JSON 和卡池差异核对通过。用 4 张相关牌的定向快照运行文本生成器，确认两种语言的目标牌面、关键词、能力提示和背景均与提交内容一致。Git 空白检查通过。

## 文案复核

复核了工作台 r6212 的 106 条卡牌基础及升级描述、本地 r6196 的 97 张运行卡牌和简中提示。以本体简中 v0.113.0 的「巨像」为「牢笼」的句式参照，并依据现有实现明确攻击伤害。

「全力接招」「全力相搏」仍有使用旧牌名的选牌提示资源，「全都一起带去」仍有「选择2张关联牌」提示资源。已核对当前出牌入口，这三个效果都直接处理对应卡牌，未打开这些选牌提示。
