# 箭矢整体提速

## Original user input

> $worktree-task 你来整体将箭矢移动速度加快一些

> 合并并清理worktree

## Brief task plan

- 起始检出位于仓库根目录，分支 `main`，提交 `d410f31a82c74b8646ddd04930c1fda5774e0012`，工作区干净。
- 任务分支 `codex/faster-arrow-motion`，工作树 `../worktrees/STS2_ShoujoKagekiJunna/faster-arrow-motion`。
- 检查箭矢部署、排位与转向、发射的时间控制，小幅统一提速，保留平滑曲线及出牌与命中的时序关系。
- 执行必要构建与定向验证，在满解锁、最小化静默测试实例中通过正常出牌录制效果。
- 整理为单个任务提交，同步本地 `main` 后请求合并确认，合并完成后再请求工作树清理确认。

## Result and changes

实现与必要验证完成。用户已明确授权合并至 `main` 并清理本任务工作树与分支。

- `src/Combat/ArrowLayoutMotion.cs` 统一设置 1.25 倍速，排位与转向的阻尼频率同步提高；保留平滑起步、收尾及转向中的速度衔接。
- `src/Combat/ArrowHud.Deployment.cs` 加快部署时间轴，并同步出牌放行时刻。默认轨迹的完整部署由 2.05 秒缩短为 1.64 秒，拉弓开始与出牌放行由 1.01 秒缩短为 0.808 秒；装填音效仍提前 0.21 秒播放。
- `src/Combat/ArrowHud.Feedback.cs` 加快发射与命中特效，并同步实际结算等待。发射至命中由 0.44 秒缩短为 0.352 秒，同样动作耗时整体减少 20%。
- 起始基线及修改后的 Release 构建、资源导入、PCK 打包和独立验证模组编译均通过，0 警告、0 错误。
- 复用 `junna_arrow_cadence check` 与 `junna_followup check`，44 项定向断言通过，保存 47 条检查及完成记录。覆盖部署重叠、提前完成、视觉清理、排位与转向、最短角度、转向中发射及 30／120 FPS 一致性。报告位于 `out/tmp/faster-arrow-motion/checks/arrow-cadence/results.json` 和 `out/tmp/faster-arrow-motion/checks/arrow-followup/results.json`。
- 实机录制在满解锁隔离存档中最小化、静默进行，录像期间仅通过悬停与正常出牌操作展示连射、横扫、再练一组、满弦射击和弦声齐鸣，包含敌人死亡后重新排位与瞄准。视频 `out/tmp/faster-arrow-motion/video/arrow-speed-demo.mp4` 长 16.23 秒，1280×720、30 FPS，H.264 与引擎内部 AAC 音轨；抽帧检查与完整解码通过。胜利后的奖励状态已确认。
- 已审查 144 份已跟踪文本资料及本任务文档，未发现凭据或私人联系方式；本任务文档使用仓库相对路径及技能名。

- 合并前 `main` 有 8 项新的能量图标与场景调整，已记录逐文件哈希，合并后据此校验。视频、验证记录与测试日志已按哈希校验后保存至主目录 `out/tmp/faster-arrow-motion/`，验证构建保存在 `artifacts/faster-arrow-motion/validated-build/`。合并与清理结果记录于 `out/tmp/faster-arrow-motion/integration-result.json`。
