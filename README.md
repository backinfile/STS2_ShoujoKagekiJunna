# 星见纯那

首批实现：纯那角色、9 张工作台卡牌、箭矢牌堆、卡牌实例伤害加成与额外伤害次数。

- 游戏版本：v0.111.0；BaseLib：3.4.7。
- 初始牌组：5 打击、4 防御、1 满弦射击。
- 生命暂设 70，起始遗物暂用锚，待专属角色数值与遗物设计确定后替换。
- 战斗立绘：2026-11-04 精修版 04 待机、02 攻击。
- 悬浮箭矢：细长菱形、加厚灰边，箭尾侧显示圆形实时倒计时，按充能球尺寸制作；标记位于第二行遗物的位置，上方留一行遗物。单体箭按目标由前到后排列，尖端旋转朝向目标，直线在半空渐隐；群体箭使用 A「三箭束」框体，在玩家前上方展开加宽 25%、中途渐隐的扇面。

## 构建与安装

执行 `./build.ps1`。脚本的 `GameDir`、`Godot` 参数可指定游戏与 MegaDot 编辑器路径。
产物位于 `dist/ShoujoKagekiJunna/`；将整个目录放到游戏的 `mods/`，同时安装 BaseLib 3.4.7。

卡牌设计与美术来源记录在 `docs/implementation/first-slice-design.json`。
`tools/assets/prepare-game-assets.py` 从工作台 MCP 导出快照提取素材；工作台内容仍统一通过 MCP 编辑。

## 项目目录

```text
src/                   模组 C# 源码
ShoujoKagekiJunna/      游戏图片、场景与本地化资源
docs/design/           工作台源码、设计数据与素材来源
docs/implementation/   实现记录与兼容说明
tools/assets/          素材提取、场景生成与图像处理脚本
tools/dev/             独立测试环境与录像脚本
tools/balance-sim/     卡牌数值模拟
tools/pack.gd          Godot 资源打包入口
tests/Integration/     独立测试模组
```

Git 保存源码、运行资源、工作台初始数据与当前设计数据。构建输出 `dist/`、验证产物 `artifacts/`、素材处理中间文件 `output/`、下载工具 `.tools/`、依赖缓存、工作台预览与历史备份保留在本地。工作台生成页面包含本机连接信息，也由 `.gitignore` 排除。

工具依赖与入口见 [tools/README.md](tools/README.md)，工作台启动与编辑流程见 [工作台说明](docs/design/workbench/README.md)。

## 本轮验证

使用独立游戏目录、独立存档和 MCP mod。`tools/dev/start-test-game.ps1` 默认使用端口 15727，开启静默音频、最小化启动保护。

`tests/Integration/` 是单独的测试模组，不进入发布包。它提供 `junna_probe run`、`junna_probe real`、`junna_probe prepare` 和 `junna_probe render` 命令，分别用于机制断言、华恋实际卡牌检查、录像准备与最小化离屏绘制。
`tools/dev/record-first-slice.py` 通过 MCP 操作准备活动、重复练习和满弦射击，检查强化结果及三回合后的 54 点伤害，并录制视频。

C-1 的构建、1/3/6 支排列与倒计时结算检查保存在 `artifacts/floating-arrows/c1/`。悬浮卡图优先读取 `images/floating_arrows/<卡牌 ID>.png`，其余沿用卡牌原图。满弦射击的竖版图使用内置 imagegen 按已选 C-1 整理，提示词位于 `output/imagegen/floating-arrow-c/portrait-prompt.txt`；随后使用 GPT Image 2 Artifact Cleaner 以 0.5 强度清理摩尔纹，原尺寸对比与处理记录位于 `output/artifact-cleanup/2026-11-04/floating-arrow-c1/`。

高空横列版本的实机截图与倒计时检查保存在 `artifacts/overhead-arrows/`。当前源码恢复 240∶416 的细长菱形比例，加厚灰色边框；1920×1080 游戏画布上箭矢框体约为 42×73，在 1280×720 窗口中约为 28×49。群体扇面引导线在目标距离的 70% 内渐隐。群体箭显示按卡牌的 `AllEnemies` 目标类型识别，并由独立测试模组的群体箭检查。此修订直接编译到测试环境并读取源码 SVG 预览，尚未重新打包 `dist/`。

机制接入与适配边界见 `docs/implementation/runtime-compatibility.md`。
