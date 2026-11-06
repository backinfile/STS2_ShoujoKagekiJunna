# 星见纯那

当前实现：纯那角色、115 张工作台有效牌面（含关联牌与生成牌）、19 组知识关系、箭矢牌堆、卡牌实例伤害加成与额外伤害次数。

- 游戏版本：正式版 v0.107.1 与测试版 v0.111.0；BaseLib：3.4.7。
- 初始牌组：5 打击、4 防御、1 满弦射击。
- 生命暂设 70。起始遗物为绿色宝石：战斗开始时获得 1 点能量，可保留至下回合；升级为燃烧宝石后获得 3 点。通过奥罗巴斯之触替换升级。
- 战斗立绘：2026-11-04 精修版 04 待机、02 攻击。
- 强化标记采用 C 型底边嵌条，伤害加成与额外次数沿卡图底边靠左横排，单项存在时按内容收紧。1～3 次逐枚显示小箭头，4 次起显示箭头和次数数字。箭矢牌避开中央回合数徽牌，场上悬浮箭矢只显示额外次数。[显示预览与定向验证](docs/worktree-tasks/card-investment-marks/README.md)。
- 获得过伤害加成或额外伤害次数的卡牌，在本场战斗持续带有暖金色卡框粒子；加成转移后保留，战斗结束清除。知识牌堆叠悬停时可用滚轮切换，左键直接获取、右键平铺预览。[效果与验证](docs/worktree-tasks/card-bonus-border-vfx-20261105/README.md)。
- 箭矢卡面：使用工作台选定的 D 型瞄准卡框，中央独立显示初始回合数；边框随稀有度着色，远古牌保留全幅卡图与独立计数徽牌。
- 箭矢出牌动画保留卡牌沿贴弓卷入轨迹移动，带细灰蓝拖尾，随后从固定虚幻弓的后方前送，收成箭矢并搭弦。搭箭停 0.09 秒后快拉、慢绷，配合装填声和满弓落定声；满弓保留 0.40 秒，再用 0.24 秒淡去。目标线或柔和扇面在拉弓时出现，随拉弓延长至原长度，换形处播放 8 帧灰蓝薄片动画。默认贴弓卷入总长 2.05 秒，另保留大回环、S 形回摆、直接前送三种实机对比方案。素材与验证记录见 [转场特效任务](docs/worktree-tasks/arrow-transformation-vfx/README.md)。
- 悬浮箭矢：细长菱形、加厚灰边，箭尾侧显示圆形实时倒计时，按充能球尺寸制作；标记位于第二行遗物的位置，上方留一行遗物。单体箭按目标由前到后排列，尖端旋转朝向目标，直线在半空渐隐；群体箭以单枚菱形卡图为主体，三枚小箭头贴近前半段两侧，以随机高度和相位绕转，交替被主体遮挡或叠在主体前方，尖端正前方留空；加宽 25% 的扇面从箭尖前方淡入，近端、远端与侧边均渐隐，引导线柔化。

## 构建与安装

执行 `./build.ps1 -CorePackage <Core发行目录>`，或通过 `SHOUJO_CORE_PACKAGE` 指定同一目录。脚本的 `GameDir`、`Godot` 参数可指定游戏与 MegaDot 编辑器路径。
产物位于 `dist/ShoujoKagekiJunna/` 和 `dist/ShoujoKagekiAijoCore/`。将两个完整目录放到游戏的 `mods/`，同时安装 BaseLib 3.4.7。加载顺序为 BaseLib → Core → Junna。入口 DLL 按游戏版本选择 `lib/ShoujoKagekiJunna.Stable.dll` 或 `lib/ShoujoKagekiJunna.Beta.dll`；构建脚本使用正式版 0.107.1 引用包与 `GameDir` 中的测试版程序集分别编译。

当前卡牌与升级数值记录在 `docs/implementation/card-pool.json`，效果覆盖与验证见 [卡池实现记录](docs/implementation/card-effects.md)。
`python tools/assets/generate-card-catalog.py <MCP导出快照>` 更新卡牌清单、模型入口、配图与本地化；具体牌效在 C# 中实现。工作台内容仍统一通过 MCP 编辑。

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
`junna_probe catalog` 与 `junna_probe catalog-events` 验证本轮新增卡牌共用机制；结果分别写入 `artifacts/dev-game/card-pool-results.json` 与 `card-event-results.json`。测试项目可用 `JunnaAssemblyPath` 指向当前编译 DLL。
`junna_probe catalog-all` 验证 115 张牌的基础／升级版本，`junna_probe catalog-edges` 验证持续触发与组合边界；命令后可接卡牌 ID 或场景前缀进行定向复查。本轮 230 个牌面场景、1490 项断言通过，记录在 `artifacts/card-completion/`。使用 `start-test-game.ps1 -Instance card-completion -Port 15729 -UseInstalledMod` 可启动单独的测试实例。
`tools/dev/record-first-slice.py` 通过 MCP 操作准备活动、重复练习和满弦射击，检查强化结果及三回合后的 54 点伤害，并录制视频。

C-1 的构建、1/3/6 支排列与倒计时结算检查保存在 `artifacts/floating-arrows/c1/`。悬浮卡图优先读取 `images/floating_arrows/<卡牌 ID>.png`，其余沿用卡牌原图。满弦射击的竖版图使用内置 imagegen 按已选 C-1 整理，提示词位于 `output/imagegen/floating-arrow-c/portrait-prompt.txt`；随后使用 GPT Image 2 Artifact Cleaner 以 0.5 强度清理摩尔纹，原尺寸对比与处理记录位于 `output/artifact-cleanup/2026-11-04/floating-arrow-c1/`。

高空横列版本的实机截图与倒计时检查保存在 `artifacts/overhead-arrows/`。当前源码恢复 240∶416 的细长菱形比例，加厚灰色边框；1920×1080 游戏画布上箭矢框体约为 42×73，在 1280×720 窗口中约为 28×49。群体扇面引导线在目标距离的 70% 内渐隐。群体箭显示按卡牌的 `AllEnemies` 目标类型识别，并由独立测试模组的群体箭检查。本轮已重新打包 `dist/` 并验证正式版与测试版。

机制接入与适配边界见 `docs/implementation/runtime-compatibility.md`。

本轮预览着色排查、知识按钮与文字、箭矢层级、宝石尺寸、能量保留和双星重放的修改及定向验证见 [任务记录](docs/worktree-tasks/pursuit-upgrade-preview-analysis-20261105/README.md)。
