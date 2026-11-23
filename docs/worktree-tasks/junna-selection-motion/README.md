# 纯那选角动效设计

## Original user input

```text
[$worktree-task]($worktree-task)  外部浏览器访问 [<本机 WanGP LoRA 库>](<本机 WanGP LoRA 库>) 用

## Anime Motion I2V这个lore帮我设计下选角界面
```

```text
可以录制本地角色选角界面参考下
```

```text
目标像素一开始不要太大，先确定效果
```

```text
先看样片，暂不合并
```

```text
我发现原图有个问题，弓挡住的裙子和身体的连接处有问题，现在显得人物有些胖了
```

```text
我是说裙子显得腰太粗了，你找人物设定图对比下，修改下裙子
```

```text
我是说裙子显得腰太粗了，你找人物设定图对比下，修改下裙子和腰部，必要时重新全部生成
```

```text
放游戏内我看看
```

```text
感觉动作太频繁了
```

```text
我是让你重新生成
```

```text
现在好了，重试下吧
```

```text
不太好，你继续分析原版职业的，看看怎么设计动作好
```

```text
继续设计吧
```

```text
启动游戏我看看
```

```text
裙子前半（左侧）不太对吧
```

```text
这才是裙子的范围
```

附图为上一版的 1536×1024 裙腰局部。用户用红线标出腰沿及两侧裙片边界，红线跨过手、弓和前景栏杆。

```text
合并 清理分支
```

## Brief task plan

- 起始检出为干净的 `main`，提交 `7434a31b989ea081de96fc524e1f825065e19190`。任务分支为 `codex/junna-selection-motion`，工作树位于 `../worktrees/STS2_ShoujoKagekiJunna/junna-selection-motion`。
- 在外部浏览器查看 WanGP 的 Anime Motion I2V，用隔离档案录制纯那选角的构图与切换，再用现有立绘制作固定机位的低分辨率动效预览。
- 局部修正弓后腰身与百褶裙的连接，清理生成素材，再用修正版更新低分辨率样片。
- 将新版立绘和动效作为纯那游戏内的选角背景，使用原生选角控件录制实机预览。
- 根据实机观感延长待机间隔，每轮保留一次眨眼和短暂的披风轻动。
- 用修正版立绘作为首帧，以 Anime Motion I2V 重新生成低频待机，核对原片动作后放进实机录制。
- 对照当前版本六个原版职业的切选与待机动作，形成可用于下一轮纯那动效的动作设计。
- 将稳弓、宝石短亮和慢呼吸做成可在游戏内选择的草案，用原生选角切换验证。
- 对照舞台服侧面校正弓把下方画面左侧的前裙片，同步修订立绘与抬弓时的服装底图。
- 以用户标出的整条裙片轮廓为准重画遮挡后的布料，同步修正立绘、抬弓底图和主体透明度。

## Result and changes

- [选角动效预览](selection-preview.mp4) 为 640×360、24 fps、4.46 秒。角色信息、进阶、头像与操作按钮取自实机画面。纯那在右侧轻眨眼，发尾和披风随呼吸微动，画面下沿的绿色宝石闪一下。首尾回到同一姿态。
- [现有选角实录](live-reference.mp4) 为 640×360、24 fps、12.31 秒，含游戏原声。隔离档案经 `unlock all` 准备，实录通过原生菜单切入纯那、切到静默猎手、再切回纯那。
- [WanGP 生成原片](wangp-original.mp4) 为 832×480、107 帧。模型为 H3 HyperFlow 14B，LoRA 为 Anime Motion I2V 的 1400 号权重，强度 0.9，6 步，种子 361848。首帧使用 `ShoujoKagekiJunna/images/character_select.png`。

### 按用户标线修正整条裙片

- [局部对照](marked-skirt-before-after.png) 左为上一版，右为修正版。根据 1536×1024 附图描出腰沿与两侧轮廓。左缘绕过虎口后方向画面左下延伸，右缘在白毛边之前收住。红线穿过的手、弓、腰带与紫色栏杆仍压在裙片上。
- 两张局部稿均用内置 ImageGen 生成。[轮廓原稿](marked-skirt-imagegen-original.png) 用于对照裙片和相邻背景，[无弓遮挡的底稿](marked-skirt-underpaint-original.png) 提供连续的裙褶。两张均在 1536×1024 原尺寸用本地 GPT Image 2 Artifact Cleaner 清理，LatentR + FLUX.2-VAE、CUDA/bfloat16、强度 0.25、tiling 开启、compile 关闭。清理稿用于下方三份游戏素材。
- 修正结果用于 `ShoujoKagekiJunna/images/character_select/` 中的 `motion_poster.png`、`waist_backing.png` 与 `subject.png`。立绘局部共改动 36,347 个像素，核对了手和弓的保护区以及栏杆的遮挡，试渲了停稳和抬弓两个姿势。
- [实机录像](in-game-marked-skirt.mp4) 为 1280×720、24 fps、5.54 秒，含游戏原声。MegaDot 导入和 PCK 打包通过。录像 133 帧完整解码，录制丢帧为 0，隔离游戏窗口停在纯那选角页。

轮廓稿提示词

```text
Use case: precise-object-edit and character animation underpainting. The provided image is a 1536×1024 artwork crop with a thin hand-drawn red GUIDE across the waist, the gripping hand, and the diagonal magenta stage rail. Trace the THREE sides drawn by the user to create the complete blue pleated skirt UNDER the arm, hand, and bow. Honour the left contour exactly: from roughly (520,674) it curves down and left through the foreground hand to about (340,1024). Honour the right contour exactly: from (1138,579) at the gold waist it falls to about (1390,1024), near (1280,881) as it meets the top of the magenta rail. The waist is the shallow diagonal line from (520,674) to (1138,579), with the existing tucked, narrow gold belt preserved at the right. Produce a paint plate, revealing the skirt fabric INSIDE the outline where the arm, hand, grip and bow lie over it. Those foreground pieces will be composited back separately. Outside the skirt guide, leave the actual stage layout and colours in place, including the background, cape, jacket and diagonal magenta foreground rail. Remove all of the red guide strokes from the result. Match the existing illustration precisely in registration, framing, colours and textured anime cel-shaded style. Inside the outline use the same muted navy and blue-violet narrow skirt pleats, tight at the belt, gently widening below the hand. Add the left/front folds behind the arm and hand as continuous fabric attached to the waist, and keep the existing readable shaded rhythm on the right. Do not replace the painted world with a transparent background. No characters or objects added, no anatomy or new visible body parts, no extra trim or belt, no labels, no graphic marks. Output only the edited crop, exact composition and bounds.
```

底稿提示词

```text
Use case: precise-object-edit. The provided image is the exact 1536x1024 crop of a painted anime stage illustration. This output is a hidden, unoccluded fabric backing plate for the exact blue skirt. Delete the foreground wooden bow including its single grip wrapping, the large foreground hand and fingers, and the blue and red forearm only where these overlap the skirt below its gold waist ribbon. Replace their overlap with the same navy blue pleated skirt fabric and extend each visible fold seamlessly up to the narrow belt. Where the hand and bow protrude past the skirt silhouette on the left, fill the erased area with the matching smooth purple theatre background. Preserve the exact existing contours of the fitted skirt on both sides, from the left edge at (520,674) curving down left to (340,1024), and from the right edge at (1138,579) down to (1280,880) at the rail. Preserve all existing bare blue cloth on either side of the bow, rail and purple background at exactly their current coordinates. Keep the original textured anime brush shading, blue-violet folds and deep cool shadows. No bow, hand, skin or sleeve remaining inside the skirt shape. The gold waist ribbon itself and everything above it stay in place. Preserve exact crop, lighting and alignment. No outlines or annotations.
```

### 前裙片修正

- [局部对照](front-skirt-before-after.png) 左为上一版，右为修正版。画面左侧的前裙片从握弓手指后方接出，外侧两道褶向舞台栏杆逐渐展开。局部合成修改了 9,080 个像素，手、弓与栏杆保留原画。成品为 `ShoujoKagekiJunna/images/character_select/motion_poster.png`，抬弓时露出的底图为同目录的 `waist_backing.png`。
- [内置 ImageGen 原稿](front-skirt-imagegen-original.png) 为 1536×1024，使用本地 GPT Image 2 Artifact Cleaner 按原尺寸清理。LatentR + FLUX.2-VAE，强度 0.25，CUDA/bfloat16，tiling 开启，compile 关闭。对照原尺寸的前裙褶、指尖和弓柄边缘后，将清理稿局部合回原立绘。
- [更新后的实机录像](in-game-front-skirt.mp4) 为 1280×720、24 fps、5.54 秒，含游戏原声。MegaDot 导入和 PCK 打包通过，核对了静止与抬弓两帧的前裙片，实机录像 133 帧完整解码，录制丢帧为 0。窗口停在纯那选角页。

内置 ImageGen 局部提示词

```text
Use case: precise-object-edit. Image 1 is the exact 1536×1024 landscape crop to edit. Image 2 is a side view of the official stage costume, reference only for how the short blue pleated skirt hangs in this pose. Correct only the two screen-left/frontmost blue skirt folds immediately behind and underneath the gripping hand. The current left edge appears as an unnaturally straight cut that starts too far to the right beneath the wrapped grip, making the skirt look disconnected from the front hip. Extend the left/front edge by a small amount at the bottom, about 95–115 pixels LEFT at y=985 in image 1, and less toward the top. In image 1 at y=930 the cloth should just begin to appear from directly behind the right two lower fingertips; toward the diagonal magenta foreground rail, add one or two softly opening narrow knife folds with their lines logically continuing back toward the existing cinched waist behind the hand. The leftmost edge has a gentle convex cloth curve, initially dropping behind the fingers then opening slightly to the left at the rail. Let the last fold beside it merge into the existing blue pleats at about x=820 without a seam. Preserve exactly the fitted gold waistband, upper body, cuff, every finger, wooden bow and wrap, bowstring, cape, white trim, right-side skirt silhouette, entire purple stage rail, background and scene framing. Keep the exact colors and painterly cel-shaded surface in Image 1, including subdued blue-violet folds with deep shadows. Keep the skirt hem hidden below the diagonal rail. Edit only the fabric and the narrow purple gap the additional front fold covers. No visible skin added, no side drape or sash, no extra trim, no full-skirt expansion. Output only the edited Image 1 crop.
```

### 稳弓版实机

- [愚者与纯那切选实录](in-game-bow-settle.mp4) 为 1280×720、24 fps、16.58 秒，含游戏原声。约第 1.6 秒和第 10.5 秒两次切到纯那。实机两次都触发稳弓，弓尖约抬 6 像素，披风随后约下移 3 像素。绿色宝石在弓回落时短亮，随后以 5.6 秒的周期轻呼吸，视线保持回望，星屑继续漂移。
- 选角场景恢复手、弓、弦和服装的分层动画。用修正版立绘和经过 Artifact Cleaner 的裙腰底稿补齐弓后露出的裙褶与背景。局部试渲核对了静止、抬弓及回落时的腰带、弓后布料与披风边缘。
- 披风参考由 WanGP 以修正版立绘生成，Anime Motion I2V 1400、576×320、107 帧、6 步、权重 0.50、种子 476209。原片披风下落幅度较大。取开头 0.5 至 1.75 秒的外缘位移，压缩并转为原图披风网格的跟随曲线。
- MegaDot 导入和隔离 PCK 打包通过。实机录像 398 帧完整解码，录制丢帧为 0，游戏停在最小化、静音的纯那选角页。

### 原版职业动作对照

用 v0.113.1 的隔离实机依次选中六个原版职业，每个保留约 8 秒。完整实录长 51.71 秒，录制丢帧为 0。截取切选后的相同时间轴制作[六职业并排录像](native-selection-comparison.mp4)。画面从左到右、从上到下为铁甲战士、静默猎手、储君、亡灵契约师、故障机器人、愚者。

| 职业 | 切选后画面里最显眼的动点 | 可借鉴的分工 |
| --- | --- | --- |
| 铁甲战士 | 举剑构图很快稳定，剑和上身轻晃，红色余烬穿过背景 | 留住武器轮廓，让余烬承担持续运动 |
| 静默猎手 | 骨面和视线很稳，披肩、环状挂件和绿雾持续慢动 | 面部提供稳定焦点，布料和雾负责延续 |
| 储君 | 坐姿始终明确，抬起的手轻动，星图和光点变化 | 角色站得住，背景主题可以自己动 |
| 亡灵契约师 | 巨手和镰刀轮廓保持可读，巨手指节稍有起伏，紫焰持续翻动 | 旁侧召唤物和火焰可以比主体更活跃 |
| 故障机器人 | 机械头和抬起的机械部件微动，蓝色电光和光点更醒目 | 把节奏集中到真正能说明职业的能量效果 |
| 愚者 | 保持蓄弦姿势，搭箭手与袖口微移，绿光沿着箭和眼部持续可见 | 持弓本身就能交代准备感，手、弦与箭沿同一方向配合 |

在宽 460 像素的录像上跟踪切选后 0.55 到 7.25 秒的局部纹理。六个职业头部取样横向变化约 1 到 4 像素，愚者搭箭手的取样约横向 8 像素、纵向 4 像素。当前 PCK 的六个背景都含 Spine 动画及独立粒子层，愚者在两个眼部骨骼挂有光点发射器。跟踪数值用于比较画面的稳定程度。

重试生成的原片用新版裙腰立绘、Anime Motion I2V 1400、576×320、107 帧、6 步、权重 0.54、种子 384191。原片在一次闭眼中持续约一秒多，并带出低头，成为画面主要动作。用户评审后，工作树的运行素材回到此前的待机版本。

### 纯那下一版动作设计

主动作采用回望持弓时的一次稳弓。愚者的搭箭手提供了受力参照，静默猎手提供了披肩和人物的分层参照。纯那沿用现有持弓姿势，用弓尖的移动表达手腕刚稳住武器。

| 时间 | 动作 |
| --- | --- |
| 切选后 0–0.25 秒 | 人物以回望持弓姿势出现，视线先落稳 |
| 0.25–0.85 秒 | 握弓手收 1–2 像素，弓尖上抬约 5–7 像素，弓弦和弓一起运动。幅度以 1672×941 的立绘为准 |
| 0.85–1.4 秒 | 弓回落少许并停稳。披风外端延迟约 0.15 秒跟上，宝石在停稳时短亮一次 |
| 待机 | 肩胸以约 5–6 秒的周期起伏 1–2 像素，舞台低亮度的星屑以不同速度漂移。眼神维持回望的焦点 |

握弓手、弓和弓弦复用 `ShoujoKagekiJunna/scripts/character_select_layers.gd` 的分层控制，修正版裙腰作为稳定底图。用 Anime Motion I2V 提供披风外端的运动参考。看片时跟踪握弓手、弦的两个端点和裙腰外缘，核对选中动作的触发次数及待机起伏幅度。

### 前两版游戏内预览

- [低频待机实录](in-game-idle.mp4) 为 1280×720、24 fps、19.04 秒，含游戏原声。待机改为 12 秒一轮，入场先停留 2.5 秒，再眨眼一次并短暂带动发尾和披风。宝石在同一段轻闪，人物在两次动作间静止约 10 秒。实机相邻两次动作的峰值间隔为 11.96 秒。
- [首版实机录像](in-game-motion.mp4) 保留原来的 4.46 秒循环，供对照选角切换与动作频率。
- 当时的选角背景加载修正后的立绘及 640×360 的 Anime Motion I2V 循环素材。腰带和裙褶取自新版立绘。角色数据、进阶、头像和操作按钮由实际游戏绘制。
- 预览资源经 MegaDot 导入和 PCK 打包，使用隔离的 v0.113.1、Core 2.0.2、纯那 r6148 游戏实例。新版 Theora 独立帧编码的 288 帧完整解码，静止段像素一致。实机录像确认连续两轮的眨眼和中间停顿，录制丢帧为 0。游戏留在最小化、静音的纯那选角页。

### 裙腰轮廓修正版

《Re LIVE》的全身舞台服立绘显示，贴身上衣收进一条金色腰带，百褶裙从腰带下方起，越靠下越展开。TV 第 2 话的持弓回望镜头则提供了侧面角度与披风遮挡的参照。[Ateam 舞台服全身立绘存档](https://static.zerochan.net/Hoshimi.Junna.full.2553660.png) · [TV 第 2 话截图](https://xenodude.com/2024/06/19/revue-starlight-ep-2/)

- [修正版立绘](character-select-waist-skirt.png) 收进裙腰两侧的布料外轮廓，在裙子和白毛边之间留出一条暗色间隔。窄金腰带标出衔接处，弓下的裙褶从腰带起，向下逐渐展开。[原尺寸局部对照](skirt-waist-before-after.png) 左原图，右修正版。
- 内置 ImageGen 生成[裙腰局部稿](skirt-imagegen-original.png)和[合成底稿](skirt-underlay-original.png)，两张均为 1536×1024，按原尺寸通过本地 GPT Image 2 Artifact Cleaner 清理。参数为 LatentR + FLUX.2-VAE、CUDA/bfloat16、强度 0.25、tiling 开启、compile 关闭。将修正的腰身与裙子合入 1672×941 原画布，前景叠回原图手、弓和弓弦。核对原尺寸处理前后、武器边缘和裙腰两侧；61,223 个像素在局部蒙版内变化，武器保护区像素一致。
- [新版样片](selection-preview-v3.mp4) 沿用 Anime Motion I2V 的眨眼和披风姿态，应用修正版立绘。640×360、24 fps、107 帧完整解码通过，首尾帧一致。

内置 ImageGen 局部重绘提示词

```text
Use case: precise-object-edit, wardrobe draftsmanship and garment construction. Image 1 is the exact 3:2 anime illustration crop to edit. Image 2 is the official Re LIVE full-body costume reference. Image 3 is the TV episode 2 side-view pose reference. Use images 2 and 3 solely to understand how Junna's blue skirt is cut and how it connects to her fitted, belted navy jacket. Retain image 1's painterly finish, pose, costume colors under violet lighting, crop, camera and dimensions.

Correct the actual shape and silhouette of the waist and skirt, not just the lighting. In image 1 the blue skirt is currently drawn as a very broad fan starting high and immediately behind the bow, making the waist and hips look like one wide cylinder. Carefully redraw the whole small blue skirt area visible around and BELOW the wooden bow. Bring the high skirt's left edge from under the wrist in toward the center of her body and bring the right edge in from under the fur trim so the skirt's upper body is a noticeably smaller, tapered trapezoid. Make the skirt at the waist about one fifth narrower than the current waist read, proportionate and believable for this pose. Use a small patch of the existing dark purple backdrop or the deep cape shadow where the wide blue skirt edge has been pulled inward. The far/right skirt edge should be clearly readable as its own contour a little left of the white fur, giving the materials a narrow visual separation. Do not leave old wide blue skirt outside the corrected contour. Under the bow, draw four to five crisp, nearly vertical blue knife pleats, packed tightly at the waist. Let them gradually open only lower down closer to the purple foreground rail; do not draw a broad radial fan. Draw a slim, subdued antique-gold belt segment in the little space visible above the bow under the navy jacket, partly obscured by the arm and bowstring, for the same fitted waist construction shown in images 2 and 3. The lower crop is still fully clothed and the purple foreground rail masks the skirt hem. Do not add a new shiny seam, extra straps, ornaments or exposed skin.

Treat the illustration's objects in front of the skirt as a protected layer: keep original gripping hand and every finger, red sleeve and gold sleeve trim, the recurved wooden bow and wrapped handle, exact thin single bowstring, white fur, red cape, background architecture, and diagonal purple foreground rail in their exact same shapes, positions and scale. Maintain the original composition so the corrected patch can be placed back without shifting any edges. Preserve original moody colors, deep navy fold shadows and muted mid-blue highlights. Output the one edited version of image 1 only, no framing, guides, text, or graphic overlays.
```

内置 ImageGen 合成底图提示词

```text
Use case: precise-object-edit, background-extraction underpainting for the exact image. Edit this existing cropped anime illustration, preserving all framing and pixel alignment. Delete only the golden-brown exposed wooden bow limb to the RIGHT of the wrapped handle, from just past the hand grip across the skirt to its exposed right tip. This narrow curved strip is roughly x570 to 1050 and y650 to 850 in this 1536x1024 image. Also remove the shadow painted by this wooden limb on the cloth inside that strip. Fill the erased narrow strip by connecting the SAME existing crisp muted blue knife pleats immediately above and below it, and by continuing the existing deep plum negative gap to their right. Maintain the clear, distinctly narrowed skirt and waist, slight opening of pleats near the rail, and its exact visible contour. This is only an inpainted backing layer; a separate copy of the original wood will be put in front afterwards. Preserve the bowstring line above, cinched gold belt, hand and fingers, wrapped grip, red sleeve, all fur edging, cape and the bottom purple stage rail, and the original cel-shaded painterly texture, palette and lighting. Do not redraw or move any visible pleat outside the erased narrow strip. Do not change the crop. No labels or lettering.
```

### 腰线修正版

- [修正版样片](selection-preview-v2.mp4) 保持 640×360、24 fps、4.46 秒，沿用已有 Anime Motion I2V 的眨眼和披风动效，将修正后的腰线与裙褶合入实机构图。
- [修正版立绘](character-select-waist-refined.png) 保持 1672×941。把弓后宽厚的棕色面收进蓝色衣褶阴影，让上衣腰线与弓下百褶裙从同一腰点衔接。[原尺寸局部对照](waist-before-after.png) 左为原图，右为修正版。
- [局部生成原稿](waist-imagegen-original.png) 由内置 ImageGen 生成，尺寸 1536×1024。本地 GPT Image 2 Artifact Cleaner 使用 LatentR + FLUX.2-VAE、CUDA/bfloat16、强度 0.25、tiling 开启、compile 关闭，按生成尺寸清理。清理后缩回原画对应局部，仅把弓后布料合回原图。实测 30,308 像素变化全部位于布料蒙版，手、弓身和弓弦的边缘经原尺寸目视核对。

内置 ImageGen 局部编辑提示词

```text
Use case: precise-object-edit. Edit target: this exact crop of an existing anime stage illustration. Correct only the blue uniform cloth at the junction of the torso and the pleated skirt behind the wooden bow, in the central-lower portion of the crop. The dark navy bodice currently reads as a thick barrel continuous with the skirt. Give it a believable narrow waist under the rib cage through the existing navy shadow, a short understated navy waist seam in the little area visible above the bow, and a compact set of vertical blue pleats emerging from the same waist immediately below the bow. Keep the outfit modest and fully clothed. Connect the folds naturally across the occlusion, with the right-facing hip falling back into the existing dark blue shadow under the fur. Replace the confusing wide brown patch beneath the white fur and above the bow with a slimmer muted shadow break in the existing blue fabric, preserving the nearby gold sleeve trim. Keep the brush texture, cel-shaded shapes, original dark purple lighting and dark blue colors exactly. The wooden bow, visible tips of the bow, grip, hand and every finger, taut diagonal bowstring, red sleeve, gold elbow cuff, white fur and its edges, red cape, purple backdrop and bottom diagonal stage rail are protected: keep their shape, exact position, color and details. Preserve the exact crop boundaries and scale, no zoom, no change of camera or perspective. Do not add a belt, ornament, exposed skin, text, labels, arrows, or anything outside the tiny cloth correction. Output only the corrected image.
```

### 选角动效

固定机位，保留右侧肖像和紫色舞台。左侧沿用角色名、生命、金币和起始遗物，下方沿用进阶、头像及确认和返回。动画集中在眼睛、发尾及披风，左侧保持暗色背景。原生按钮、手、弓身和弓弦使用实机静帧，宝石闪光单独叠加。最后约 0.42 秒回到首帧。

游戏接入评审时以 `ShoujoKagekiJunna/scenes/junna_select.tscn` 为纯那背景入口。选中后从原生立绘过渡到待机，换角时随原生选角过渡退出。循环节奏和画面位置先用这版预览确认。交付状态为动效设计样片，游戏接入待方向确认。

### 生成提示词

```text
Anime-Motion: Locked-off 2D anime stage illustration, the exact input framing and painting. Hoshimi Junna stays on the right, in round black glasses, blue and gold stage uniform and a red cape, gripping the wooden bow at waist height. She holds her pose and breathes, with a single natural blink, a barely perceptible sway at the very tips of her tied hair and the outer edge of the scarlet cape. The green gemstone on her bow gives one restrained glint, and two small dust motes drift up in the dark purple theatre. Keep the glasses, green eyes, facial features, gold fastenings, hand and taut straight bowstring intact and consistently placed. Dark and quiet on the left with the existing arches, consistent exposure. Return to the starting pose and lighting for a continuous seamless idle loop. No camera motion, no extra characters, arrows, graphic overlays or typography.
```

### 验证

- 三段视频完整解码通过。对照实录检查人脸、眼镜、手、弓弦和选角控件位置。预览解码后的首尾帧一致，确认了眨眼和宝石闪光，左侧角色信息保持稳定。
- WanGP 原片的 SHA-256 与工作台导出一致。实录来自 v0.113.1、Core 2.0.2、纯那 r6148；录制完成后退出隔离实例。
- 修正版 107 帧完整解码，首尾帧一致。对照样片确认腰线和裙褶可见，角色信息及控件保持稳定。

用户确认将按标线修正的版本合入本地 `main` 并清理任务分支。已提交的实机样片、生成原稿、成品和本文随选角改动保留。
