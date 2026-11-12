# 纯那选角大图候选

## Original user input

> $worktree-task 你来多生成几张选角界面的大图，要求更加炫酷一些

## Brief task plan

- 起始分支：`main`；起始提交：`e4aae718fb0ec202b6213c893b21ab29c0aeeba3`；起始检出无未提交改动。
- 任务分支：`task/character-select-cool-variants-20261105`。
- 工作树：`../worktrees/STS2_ShoujoKagekiJunna/character-select-cool-variants-20261105`。
- 以现有选角图为人物、服装和舞台参考，保留右侧主体及左侧选角信息空间，使用内置 imagegen 逐张生成四个候选。
- 原图随任务保存；使用已安装的本地 Artifact Cleaner 按原尺寸清理，核对脸、眼镜、发丝、手、弓弦、服装边缘与构图。
- 素材及记录合为一个任务提交，同步原分支后请求合并确认；清理工作树另行确认。

## Result and changes

完成四张候选，均为原生 **1672 × 941** PNG；生成与清理均保留原尺寸。现有游戏资源引用继续使用 `ShoujoKagekiJunna/images/character_select.png`，这次新增素材用于选图。

| 编号 | 候选 | 方向 |
| --- | --- | --- |
| 1 | [满弓瞄准](cleaned/01-full-draw.png) | 满弓蓄力、集中星芒、蓝白箭光 |
| 2 | [凌空放箭](cleaned/02-aerial-shot.png) | 腾空出箭、披风展开、向右上方的弹道 |
| 3 | [星轨环绕](cleaned/03-star-orbit.png) | 多条星箭轨道、环绕动势、星形舞台光 |
| 4 | [舞台逆光](cleaned/04-stage-backlight.png) | 金色聚光、向前迈步、红披风与蓝色轮廓光 |

- [四图总览](contact-sheet.png)；生成原图位于 `originals/`；原尺寸局部对比位于 `comparisons/`。
- [生成提示词](prompts.json) 记录内置 imagegen 模式、参考资源、四个方向及第二张的箭迹和弓弦修正提示词。
- [清理记录](cleanup.json)：GPT Image 2 Artifact Cleaner，LatentR + FLUX.2-VAE，strength `0.25`，本地 CUDA / bfloat16，原尺寸、tiling 开启、compile 关闭；保留源图及成品 SHA-256。
- [处理脚本](process_assets.py) 复用项目本地清理配置。重跑时使用 `python process_assets.py --cleaner-config <local-cleaner-config.json>`；仅重建对比图可附加 `--previews-only`。
- 已检查四张的眼镜、绿色眼睛、发丝、金铃、手部、弓弦及服装边缘；第二张已修正出箭方向并恢复连续弓弦。清理前后主体及细线保持清晰。
- 必要素材验证通过：四组文件可读取、尺寸一致、源图哈希保持、alpha 一致，清理确实改变 RGB；四图平均 RGB 变化约为 `1.79–2.03 / 255`。
- 原选角图与场景保持原字节，改动集中在本任务目录。验证范围为素材及 Git 检查；当前为候选图交付，游戏构建与游戏运行验证安排在选图接入阶段。
- 提交前审阅受版本控制及新增文档；本任务记录使用仓库相对路径，历史素材出处沿用既有工作台记录。

集成状态：候选已完成，等待用户确认合并到 `main`。任务工作树与分支保留。
