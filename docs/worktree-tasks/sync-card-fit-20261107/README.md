# 最新卡池契合与 Mod 同步

## Original user input

工作台中将星之壁修改为知识牌，并为其关联一张0费抽3/4.消耗

你来读取下工作台上的最新卡池，然后继续为修改过或者新增的卡牌寻找契合名字和契合的卡图。然后 $worktree-task 实现在mod中

补充确认：安装时也立即减少已有箭矢1回合，消耗优先。

追加：继续看，我又改了

追加范围确认：只完成正式卡池。

追加授权：合并并清理

## Brief task plan

- 通过云端 MCP 核对最新正式卡池，按当前牌效补齐名字、原作图片、关系与出处。
- 同步运行目录、本地化、模型、卡图及必要结算，保留退出卡池的旧模型。
- 在独立满解锁存档中执行必要的定向验证，录制合法出牌效果。
- 整理为一个任务提交，同步本地 main 后按 $worktree-task 请求合并与清理确认。

## Result and changes

正式卡池已同步至 r3677：96 张正式牌、17 个知识组。再次核对 r3680 增量，变化均属于备用或退役条目，正式卡池保持一致。

- 相对任务起点新增 10 张正式牌，29 张退出正式卡池的模型继续保留。
- 为应变、速射、肌肉记忆确定名字；补齐这三张、观星、论文六张组及学有所成共 11 张牌的原作图片和契合记录。基础与升级牌面、关联、契合池和能力说明通过 MCP 同步。
- 启明星恢复正式卡池，沿用已核对的原名和夜间星光原作图；0 费布置，等待 2 回合后获得 3/4 点当回合能量。移除旧版能量保留结算，并通过 MCP 更新契合记录及能量保留能力的关联名单。
- 原作裁图直接复用不可变资源，并核对 SHA-256。出处和最终资源映射记录于 docs/design/assets/card-sources/2026-11-07/card-fit.json。
- 应变先获得格挡，再选择推进目标为自己或敌人的箭矢。速射安装时先赋予已有箭矢消耗，再按显示顺序加速；后续布置按层数减少等待，最低 0 回合，消耗优先于回手和重打。
- 星之壁取得时附带一张 0 费抽 3/4 张并消耗的观星。论文按本次取得的实际关联牌逐张真实耗尽增加永久伤害次数，各组独立保存。
- 肌肉记忆永久保存每场第一次正数伤害加成；当前战斗的加成和转移保持完整。学有所成的真实耗尽添加战后可选知识复制奖励，升级最多选择两张，复制不附带其他关联牌，奖励及永久改动可保存恢复。
- 合唱练习按最新 1 费牌面每回合造成 3/4 点群体伤害。连贯练习为 0 费当回合效果，在出牌牌效前给予加成。布阵升级支持选择两张实际手牌。
- 修正生成器对“永久保留”“具有消耗”的误判；退出正式卡池的闪耀模型继续声明预算。大型知识组悬停保留完整组名列表，卡面预览最多两张。
- Beta、Stable、加载器、Godot 导入和资源打包通过，零编译警告、零编译错误。使用 Core 1.0.1 发行包。
- r3650 阶段的 16 项定向实机检查全部通过，包括基础与升级抽牌、选牌、已有与后续箭矢、0 回合、消耗优先、永久成长、保存恢复、复制数量、即时强化和双卡布阵。源码见 tests/Integration/Workbench3650Probe.cs；报告保存在 out/tmp/card-fit/r3650/results.json。
- r3677 追加的 4 项定向实机检查全部通过：启明星基础与升级等待及返能、其他来源能量保留不受影响、两层速射将其降至 0 回合后立即完成且消耗。源码见 tests/Integration/WorkbenchEnergyUpdateProbe.cs，报告位于 out/tmp/card-fit/energy-update-results.json。
- 96 张正式牌的原文、费用、类型、目标及图片逐项匹配 MCP 快照，报告位于 out/tmp/card-fit/source-verification.json。共享的打击版本素材保持既有规则。
- 最小化、静默模式下以随机空闲端口使用独立满解锁存档；启动监测未获得前台焦点。r3650 演示约 20.47 秒，启明星追加演示约 28.53 秒，均为 H.264 与 engine_mix 音轨。录制前完成准备，录制中仅使用原生悬停、选牌、出牌和结束回合。追加演示中两张启明星在第 3 回合完成，能量由 3 变为 10，第 4 回合恢复为 3。测试实例已关闭。
- 录像：out/tmp/card-fit/video/junna-r3650-demo.mp4、out/tmp/card-fit/video/junna-r3677-energy.mp4。实卡预览：out/tmp/card-fit/paper-final.png、out/tmp/card-fit/energy-star-final.png。
- 最新角色安装包为 out/tmp/card-fit/Junna-r3677.z01、Junna-r3677.z02、Junna-r3677.zip，分卷完整性检查通过；三个文件放在同一目录后从 .zip 解压。Core 1.0.1 沿用此前交付包。
- 按 $worktree-task 复核跟踪文档并将历史文档中的本机绝对路径替换为描述性占位符；工作台在线内容继续由 MCP 持久化。

### Original checkout and integration

- 起始分支 main，提交 a988a0d7adaf0de680e69b15420520b7b08df778。
- 任务分支 task/sync-card-fit-20261107，工作树 ../worktrees/STS2_ShoujoKagekiJunna/sync-card-fit-20261107。
- 主目录起始 13 项未提交文件中，12 项 SHA-256 保持一致；app.js 出现后续编辑，并另有 workbench.css 与 trash-pagination.test.cjs 两项工作台改动，均按当前内容保留。本任务与这 15 项未提交路径无重叠。起始文件列表：README.md、build.ps1、docs/design/workbench/app.js、docs/design/workbench/build-readonly.cjs、docs/design/workbench/deploy/build-editor.cjs、docs/design/workbench/mobile-layout.js、docs/design/workbench/mobile.css、docs/design/workbench/route-position-tags.test.cjs、tools/dev/start-test-game.ps1、docs/design/workbench/mobile-sort.test.cjs、docs/design/workbench/natural-sort.test.cjs、play.ps1、play.py。
- 用户已授权合并与清理。main 随后加入 Core 工坊依赖调整，已同步 ddcac8a，未发生冲突；18 项打包检查通过，运行源码及角色包元数据与已验证交付版一致。同步报告为 out/tmp/card-fit/synchronization-checks.json。
- 合并前再次按当前状态核对主目录未提交编辑。清理工作树和任务分支前，将安装包、录像、截图和验证报告保留到 artifacts/card-fit-20261107-delivery/。
