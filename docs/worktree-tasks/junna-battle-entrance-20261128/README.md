# 纯那战斗开场动画

## 原始输入

> 你来搜集相关设定资料，用wangp设计一个战斗开始时的角色动画

## 计划

查阅官方角色设定与动画剧情，用现有待机立绘作为 WanGP 的起始图。把选定的动作接入战斗开始流程，在实机全景检查入场、回到待机与首张攻击牌的衔接。

## 设定与动作

官方人物介绍写到纯那擅长判断和分析，希望有一天作为主角站在舞台中央。TV 第二话《運命の舞台》写到她在败北后再次拿起弓。开场的原创设计取自她面对对手时的专注和舞台礼节。[武士道官方人物资料，第 2 页](https://bushiroad.co.jp/media/593) · [动画官网第二话剧情](https://revuestarlight.com/animation/story/)

纯那保持低持弓，空手从裙侧抬至胸前绶带，肩膀略转，随即低头致意。手停在胸前时先抬眼，再把手放回裙侧。双脚落在原待机位置。

## 制作与接入

- WanGP 使用现有的 `ShoujoKagekiJunna/images/combat_motion/idle.png` 作为图生视频参考。原片为 480×832、24 fps、107 帧，使用 Anime-Motion LoRA（MiniMax-H3-I2V 1400）、6 步、种子 381524。
- 选取 21 张动作画面。抠像后用本地 GPT Image 2 Artifact Cleaner 处理生成的角色区域，修复强度 0.25，处理结果以 25% 混入不透明色块。握弓手和金弓沿原片中的手腕轨迹整体移动，弓弦沿用待机画稿。站位和缩放沿用待机动画。
- 图集为 `ShoujoKagekiJunna/images/combat_motion/entrance.png`。`ShoujoKagekiJunna/scenes/junna_combat.tscn` 中的 `Entrance` 长 1.67 秒，首尾共用原待机图。
- `src/Combat/JunnaCombatHooks.cs` 在战斗开始时通过 `src/UI/Arrows/ArrowAttackAnimation.cs` 播放一次。沿用纯那现有的表现开关和存活判断，动画在出牌流程之外独立播放。
- 原片、输入图、生成参数与取帧表归档在主检出的 `artifacts/task-deliverables/junna-battle-entrance-20261128/wangp-source.zip`。

## 验证

- 构建了模组及资源包。Godot 场景检查通过 21 个图集区域、首尾待机图、固定的缩放与位置、自动返回待机，以及普通攻击和受击接管播放。
- 在独立游戏实例从第一层地图正常进入战斗，逐帧核对了抬手、胸前停顿和落手，再打出「打击」。全景录像长 8.1 秒，1280×720、30 fps，含游戏声音。成片位于主检出的 `artifacts/task-deliverables/junna-battle-entrance-20261128/junna-battle-entrance.mp4`。
