# 项目工具

从仓库根目录运行以下入口。

| 目录或入口 | 用途 | 输入与依赖 |
| --- | --- | --- |
| `assets/prepare-game-assets.py` | 提取卡图、本地化和角色素材 | `.tools/implementation/workbench.json` MCP 导出快照、`output/` 中选定素材 |
| `assets/prepare-scenes.py` | 生成角色战斗场景 | Python 标准库 |
| `assets/` 其余脚本 | 图像清理与方案比较 | 本机 Python 图像处理环境、`.tools/` 下载工具、`output/` 配置与原图 |
| `dev/start-test-game.ps1` | 启动独立游戏和存档 | 本机游戏、BaseLib、MCP 模组、FFmpeg；默认复制 `dist/` 中的模组 |
| `dev/record-first-slice.py` | 检查并录制首批卡牌 | 已运行的独立测试战斗，MCP 端口 15727 |
| `balance-sim/` | 数值模拟与设计校验 | Node.js，详见该目录 README |
| `pack.gd` | 打包游戏资源 | 由根目录 `build.ps1` 调用 |
| `core-package.ps1` | 校验 Core 版本并随角色打包 | 官方 Core 发行目录、角色清单中的最低版本 |

图像处理脚本记录了素材制作过程，需要对应的本地输入。日常构建直接使用 `ShoujoKagekiJunna/` 中已选定的资源。

`assets/generate-card-catalog.py` 从 MCP 导出快照更新 `data/card-pool.json`、模型声明、本地化和配图。运行卡池通过 `Junna.CardPool` 嵌入程序集。

`start-test-game.ps1 -UseInstalledMod` 复用测试目录中已经安装的模组。测试环境与录像写入 `artifacts/`，素材处理结果写入 `output/`。
