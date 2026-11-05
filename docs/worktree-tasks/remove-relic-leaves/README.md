# 去掉遗物图左下角叶子

## Original user input

> $worktree-task  去掉初始遗物和升级版遗物图上左下角的叶子

> 没有去干净，应该最后只留下宝石

> 宝石的下面的棱角有点尖了

> 修改后放实际游戏中我看看效果

> 确认合并

## Brief task plan

- 从 `main` 的 `7c01273635ed02da048a0494c5b264d71b4ab207` 创建 `task/remove-relic-leaves`。
- 工作树位于 `../worktrees/STS2_ShoujoKagekiJunna/remove-relic-leaves`。
- 移除两版遗物全部金色包托与叶片，只保留宝石，将底部尖角收为短切面，同步悬停轮廓。
- 使用项目 Godot 渲染器检查资源，并在工作树的独立游戏和存档中查看两版遗物、录制效果视频。
- 汇总为一个任务提交，同步本地 `main`，等待用户确认合并与清理。

## Result and changes

- 已移除两版遗物全部金色包托、叶片及叶脉，仅保留宝石。底部由单点改为短切面，并同步相接的两个切面及白色悬停轮廓，升级版呈现宝石发光效果。
- 修改 `ShoujoKagekiJunna/images/relics/` 下的 `green_gem.svg`、`burning_gem.svg`、`green_gem_outline.svg` 和 `burning_gem_outline.svg`。
- 直接编辑 SVG 路径，核对图由 MegaDot 4.5.1 的 SVG 渲染器栅格化。四张图均为 128×128，完成 32、48、64 像素检查。相对调整前的纯宝石版本，两版主体图分别改变 153、157 个像素，两版轮廓各改变 98 个像素，变化集中于底部切面。宝石实体超出白色轮廓的像素数为 0。
- 构建通过，0 警告、0 错误，Godot 资源导入及 PCK 打包成功。独立游戏成功加载两版新图，完成正常出牌和结束回合的实机显示检查。
- 测试采用独立满解锁存档，录制前同时装备两版遗物以便对照。录制期间通过 MCP 正常出牌及结束回合。
- 预览实例采用最小化与静默音频，确认 `minimized_startup_guard=true`、`silent_test=true`、`audio_source=engine_mix`。启动采样共 120 次，0 次获得前台焦点，出现窗口的 39 次采样均为最小化。
- 实机视频为 `out/tmp/gem-relic-preview/gem-relics-gameplay.mp4`，1280×720、30 fps、13.93 秒，394 个采集帧、24 个补帧、0 个丢弃帧，含游戏内部音轨。已向用户展示视频及 `out/tmp/gem-relic-preview/relics-detail.png` 遗物栏近景。
- 原始实机截图为 `out/tmp/gem-relic-preview/01-both-gems.png` 和 `02-both-gems-next-turn.png`。渲染验证记录为 `artifacts/remove-relic-leaves/bevel-validation.json`，录像记录为 `out/tmp/gem-relic-preview/recording.json`。
- 启动预载出现已有的 Godot `Invalid Task ID` 日志，进入战斗后的正常出牌与录像完成。验证范围为本次遗物图稿、轮廓、构建和实机显示。
- 已审阅 Git 跟踪的文本说明文档及本任务文档，检查敏感信息、本地绝对路径和中文编码。`git diff --check` 通过。
- 实机录像完成后同步本地 `main` 至 `30b9db7`，四张 SVG 与录像中的版本内容一致，同步后重新构建、导入与打包通过。原目录初始的 9 项能量相关修改已随本地 `main` 更新提交，原目录当前干净。任务结果已完成，用户已确认合并到 `main`，工作树与任务分支清理待单独确认。

- 本次实机视频、截图和验证记录已按相对路径保留到主目录的 `out/tmp/gem-relic-preview/` 与 `artifacts/remove-relic-leaves/`，共 43 个文件逐项哈希一致。
