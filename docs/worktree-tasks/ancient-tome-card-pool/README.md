# 尘封魔典接入纯那先古牌

## 原始输入

### 工作区约定

用户提供了全局工作区约定和仓库说明，见 [工作区约定](workspace-instructions.md)。

### 任务

> [$worktree-task](worktree-task.md)  尘封魔典你怎么没有接入我的先古牌

### 工作树技能

用户随任务附上了 $worktree-task 的完整说明，见 [技能原文](worktree-task.md)。

### 后续消息

> 修改好后，给我正在跑的游戏加上那张先古牌

> 给我命令

> bug你是怎么修复的

> 关闭 computer use

> 合并并清理本次工作树、分支和临时测试文件

## 计划

1. 从 `main` 的当前提交建立工作树 `../worktrees/STS2_ShoujoKagekiJunna/ancient-tome-card-pool`。
2. 核对原版尘封魔典的筛选与发牌入口，验证纯那候选缺失并修复。
3. 对改动执行定向验证，提交后请求合并及清理确认。在用户正在运行的游戏中发放先古牌。

## 结果

- `src/Models/Cards/JunnaCardJK127.cs` 将「铭刻」声明为 BaseLib `ITomeCard`，并指定适用角色为星见纯那。原版尘封魔典继续负责选牌、升级和加入永久牌组。工作台规则指定「全力释放」为古老牙齿替换牌，本次魔典候选只有「铭刻」。
- 当前游戏的失败基线为 BaseLib 报告 0 张 `ITomeCard`、尘封魔典没有发牌。Beta Release 构建为 0 警告、0 错误。装载修复的 v0.114.0 独立测试局报告 1 张候选。选择达尔的尘封魔典后，存档确认遗物选中 `JUNNA_JK127`，永久牌组从 10 张增为 11 张，新增牌的 `current_upgrade_level` 为 1，原有十张牌的顺序不变。已录制原生发牌动画。
- 用户原战局通过 `card JUNNA_JK127 Deck` 补发基础版「铭刻」。原版 `upgrade` 控制台命令只支持战斗手牌。牌组界面和后续存档确认永久牌组从 18 张增为 19 张，其中恰好一张 `JUNNA_JK127`。已按用户要求停止 Computer Use。
- `main` 的原有未提交改动保留在原工作区。用户已确认合并及清理本次工作树、分支和临时测试文件。
