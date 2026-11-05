# 箭矢攻击姿势优化

## Original user input

> $worktree-task 优化下角色的攻击动作，只有当箭矢触发时才切换箭矢攻击动作，当连续触发时应该维持姿势不变

> 另外，要求箭矢切换目标时，以箭头的中心旋转，不要以箭头尾部为中心

> 是，改到菱形主体的视觉中心

> 合并并清理worktree

## Brief task plan

- 起始分支：`main`；起始提交：`7c01273635ed02da048a0494c5b264d71b4ab207`。
- 工作树：`../worktrees/STS2_ShoujoKagekiJunna/arrow-attack-pose`；任务分支：`task/arrow-attack-pose`。
- 原工作区有能量球素材及记录的未提交修改，保留原状。
- 分开普通攻击与箭矢结算的动画入口，箭矢结算期间维持射箭姿势，连续触发时延续当前姿势。
- 将箭矢切换目标的旋转支点改到菱形主体视觉中心，同步部署、倒计时、指引及发射的坐标衔接。
- 执行必要构建与定向实机检查，录制正常出牌效果；整理单个任务提交，同步本地 `main` 后请求合并确认。

## Result and changes

攻击姿势与箭矢转向支点均已实现并通过必要验证。用户已明确授权合并至 `main`，保存验证证据后清理本任务工作树与分支。

- `src/Combat/ArrowCombat.cs` 在实际箭矢结算开始时进入专用动画作用域，覆盖飞行、重复结算与结算后移牌，并在异常时结束作用域。
- `src/Combat/ArrowAttackAnimation.cs` 按角色管理嵌套结算与延迟恢复。连续触发时保持当前射箭姿势，忽略期间的普通攻击、施法和待机切换；最后一次结算结束 0.2 秒后恢复待机，不增加结算等待。受击与死亡动画保留优先响应。
- `ShoujoKagekiJunna/scenes/junna_combat.tscn` 将射箭立绘移入独立的固定位置循环动画 `ArrowAttack`，普通攻击继续使用站姿立绘及原有轻微前移动作；`tools/assets/prepare-scenes.py` 同步相同配置。
- `tests/Integration/ArrowAttackPoseProbe.cs` 新增攻击姿势的实机验证命令，并在验证项目中注册。
- `src/Combat/ArrowHud.cs` 将旋转支点由整张长图的矩形中心改为菱形主体最宽处的视觉中心，原图坐标为 `(120, 138)`。`ArrowHud.Deployment.cs` 与 `ArrowHud.Feedback.cs` 同步该坐标，倒计时保持直立，部署和发射按同一中心衔接。
- `tests/Integration/ArrowPivotProbe.cs` 的 10 项定向实机断言与完成标记通过，覆盖大角度转向中心稳定、箭尖等半径旋转、倒计时对齐与直立、目标死亡后的实际转向、排位过程中心一致，以及发射衔接。结果：`out/tmp/arrow-attack-pose/pivot-checks/results.json`。本轮代码与验证模组 Release 编译均为 0 警告、0 错误。
- 初始基线与最终 Release 编译、资源导入、PCK 打包、独立验证模组编译通过，0 警告、0 错误。
- 满解锁隔离存档中，15 项实机断言与完成标记通过：普通攻击、箭矢部署、触发前切换、连续触发及重复结算姿势与位置不变、只启动一次射箭动画、伤害与移牌保持、结束后恢复、受击和死亡优先，以及异常后恢复。结果：`out/tmp/arrow-attack-pose/checks/results.json`。
- 正常出牌录像展示打击、三张满弦射击的部署和弦声齐鸣触发连续箭矢。录制中只通过正常出牌及悬停操作继续战斗。视频：`out/tmp/arrow-attack-pose/video/arrow-attack-pose-demo.mp4`，9.67 秒、1280×720、30 FPS，H.264 与引擎内录 AAC 音轨；抽帧检查及完整解码通过。
- 追加正常出牌录像展示两支满弦射击等待期间，打击击倒原目标后转向剩余敌人，再通过弦声齐鸣连续触发。视频：`out/tmp/arrow-attack-pose/pivot-video/arrow-body-pivot-demo.mp4`，9 秒、1280×720、30 FPS，包含引擎内录音轨；抽帧检查及完整解码通过。录像期间只使用正常出牌操作。
- 测试采用最小化和静默输出，最终启动最小化保护及静默设置已确认；本任务启动的游戏已关闭，原有游戏进程保留。
- 已审查 147 份跟踪文档与本任务文档。按 `$worktree-task` 要求，将一处历史图像生成记录中的私人目录替换为描述性占位符，保留产物标识；其他工作台内容保持原状。本任务文档仅使用仓库相对路径及技能名。
- 原工作区已有的能量球修改持续保留；任务期间外部继续更新了小图标、素材记录和能量状态栏，任务操作均在隔离工作树中完成。
- 验证范围限于本次改动所需检查；全面回归尚未运行。
