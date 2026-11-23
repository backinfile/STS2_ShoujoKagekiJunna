# 工作台最新卡池同步

## 用户输入

上一轮范围说明：

> 备用区的不需要找寻契合

本轮请求：

> $worktree-task  将最新卡池同步修改到mod中

随请求提供的技能说明：

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

后续补充：

> 不要和游戏本体卡牌的名字一样

> 打击  防御  是通用的不用改，其他改下。星轨没有重复？

> 星轨不是和储君有张能力牌重名了吗？

> 星轨卡图不对，怎么是其他角色

> 合并并清理（推荐）

## 计划

- 从工作台 MCP 读取纯那最新修订，区分正式卡池、备用区及退役牌，和当前运行数据逐项对比。
- 同步正式卡池与相关牌的文本、数值、资源和结算。参考本体现有效果，复用纯那现有模型和事件。
- 在独立工作树内运行最小定向验证，记录提交与合并状态。

## 结果

- 从 `main` 的 `d835daa` 开始，原工作区干净。任务工作树位于 `../worktrees/STS2_ShoujoKagekiJunna/sync-workbench-r6144-20261123`。
- 正式卡池由工作台 r6052 同步到 r6148，包含 98 张牌和 31 个知识组。新增星轨与直至世界成灰的牌效、卡图和运行元数据。星轨按指向目标的己方单体飞矢累加伤害。直至世界成灰是普通攻击牌，先打击所有敌人，再对符合条件的各个敌人追加一次。
- 星轨换用 TV 第 1 话纯那本人拉弓射出多支飞矢的镜头，裁切后保留眼镜、拉弓的手和出箭方向。作品画面与角色参照原作片段和无字幕截图。 [原作片段](https://www.bilibili.com/video/BV1Bt411a7Ub/?p=2&t=105.75) · [第 1 话截图](https://xenodude.com/2024/06/18/revue-starlight-ep-1/)
- 安可改为返场，箭雨改为流星之矢。工作台的契合、出处、正式关联牌和 Mod 的能力标题同步更新。比对本体 v0.111.0 和 v0.113.1 的简中卡名后，重名仅有用户保留的打击与防御。储君的能力牌 ORBIT 在两版均叫环绕轨道。 [《Bright!Light!》歌词](https://utaten.com/lyric/qk19043006/) · [《世界を灰にするまで》歌词](https://www.uta-net.com/song/254335/)
- 瞄准射击的基础伤害由 8 调为 7。遥远星辰的升级文案明确同目标飞矢的随机选择。未指定回合数的转换飞矢默认等待 3 回合。
- Stable、Main、Beta 均已编译并打包。Beta 使用三敌人战斗，星轨、直至世界成灰与瞄准射击的基础和升级版共通过 24 个定向场景、578 项断言。实机录像核对了两张新牌的卡图及目标伤害，基础星轨造成 16／0／0 伤害，基础直至世界成灰造成 8／4／4 伤害。录像与卡面另存于会话交付目录。
- 用户确认将任务提交快进合并至 `main`，并清理本次临时工作树、分支和其中的临时构建及测试文件。
