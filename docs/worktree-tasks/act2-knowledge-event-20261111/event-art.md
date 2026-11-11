# 原版普通事件配图与本事件接入

## 核对依据

核对当前游戏解包资源的 `images/events/`、`images/events/tea_master.png.import`、`scenes/events/default_event_layout.tscn`、`src/Core/Models/EventModel.cs` 与 `src/Core/Nodes/Events/NEventLayout.cs`。解包目录记为 `<原版资源目录>`。

自定义图片入口由 BaseLib 的 `CustomEventModel.CustomInitialPortraitPath` 提供。[BaseLib 源码](https://github.com/Alchyr/BaseLib-StS2/blob/master/Abstracts/CustomEventModel.cs)

## 图片格式

| 项目 | 原版观察 | 本事件采用 |
|---|---|---|
| 主图尺寸 | 57张事件主图均为3440×1616，约2.129∶1 | 3440×1616 |
| 源格式 | PNG，32位RGBA | PNG，RGBA |
| 透明度 | 茶师、落水书库、迷失精灵主图抽样均为不透明像素 | 全屏书桌场景，右侧暗部 |
| 导入 | Texture2D，原版茶师为VRAM高质量压缩 | 无损压缩，compress/mode=0 |
| Mipmap | 原版茶师关闭 | 关闭 |
| 颜色及Alpha处理 | fix_alpha_border=false，premult_alpha=false | fix_alpha_border=true，premult_alpha=false，主图为不透明背景 |
| 资源位置 | 原版按模型ID小写寻找事件主图 | `ShoujoKagekiJunna/images/events/stage_girl_notes.png` |

目录另有2560×1200的 `dense_vegetation_foreground.png` 和972×1024的 `mirror_mask3.png`，分别为前景和遮罩辅助资源。

## 构图和显示

默认布局在2560×1200的TextureRect中使用保持宽高比居中显示，缩放1.04，主体轻微出血。超宽屏、窗口缩放与入场动画均会影响边缘可见范围。

标题、正文和选项位于画面右半。正文容器相对屏幕中心从x=-38延伸到x=762，宽800，顶部y=255。以默认2560宽度换算，文字主要覆盖原图宽度约48.5%至80%。人物、手册和武器等关键信息应集中在原图左侧约20%至47%，四边保留余量。

茶师与落水书库呈现手绘轮廓、简化色块、硬边明暗、低饱和环境和局部亮色焦点。右侧以深蓝黑和深棕的大色块收束，正文区域保持低对比。画面不嵌入标题、选项、字幕或卡牌边框。

上述尺寸、导入参数与坐标来自资源和场景。色彩、画法和焦点范围来自样本观察，是本次美术方向的依据。

## 参考来源与最终画面

参考图为KINEMA CITRUS在“おしえて！スタァライト劇場”系列公开的TV第2话纯那房间、书桌道具设定，2019年7月11日发布，所选书桌图600×424。以官方推文为来源，原始JPEG及SHA-256保存在 [official-art-source.json](official-art-source.json)。[官方原帖](https://x.com/KinemacINFO/status/1149363052682215425)

按用户要求，最终画面使用内置imagegen参考官方图的俯视透视、书桌结构、细线稿和淡蓝灰色块重新绘制。左侧为电脑、书本、台本和打开的笔记，桌面与房间连续延伸至四边，右侧使用自然暗部承载原版正文和选项。成品3440×1616，不使用左侧白底设定图的独立展示框。

生成原图为 `docs/assets/events/stage-girl-notes-desk-original.png`，经本地Artifact Cleaner 0.25同尺寸清理，核对电脑轮廓、书页和细线后，适配原版画布。成品为 `ShoujoKagekiJunna/images/events/stage_girl_notes.png`。最终提示词在 [source.json](source.json)，处理参数与摘要在 [desk-art-cleanup.json](desk-art-cleanup.json)。

事件名、故事与知识牌面沿用当前工作台设计；选项二按用户修订为“反复推敲：升级1张知识牌”。九张卡图继续采用工作台已选的同系列官方ReLIVE卡面。

保持宽高比居中的枚举值为5，对应场景的 stretch_mode=5。[Godot 4.5 TextureRect](https://docs.godotengine.org/en/4.5/classes/class_texturerect.html#enum-texturerect-stretchmode)
