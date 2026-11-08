# Power 图标重绘

36 枚图标，256×256 透明 PNG。参照本体 Power 的黑色轮廓、饱和主色、大块明暗和紧凑剪影，按各项名称与效果重绘。

- `icons/`：本地清理后的成品。
- `generated/`：36 张生成原图。
- `generation-final.json`、`native-reference.png`：内置 image_gen 的完整提示词和本体风格参考。
- `cleanup.json`：本地 Artifact Cleaner 参数、原图与成品校验和。
- `manifest.json`、`workbench-receipt.json`：工作台资产映射及提交回执。
- `mod-icons.json`：Mod 中 38 个资源路径的源图映射与像素校验和。
- `contact.png`、`native-comparison.png`：128 / 32 像素整套预览与本体对照。

所有原图在生成尺寸运行本地 GPT Image 2 Artifact Cleaner，strength 0.25、interior_blend 0.25，之后导出主体最长边 232 像素的 256×256 成品。JA35 内部误透明斑先恢复实色色面，再执行清理；外轮廓与气泡间隙保留。其余 35 张的原尺寸透明通道保持一致。

Mod 资源位于 `ShoujoKagekiJunna/images/powers/`。通用 Power 统一读取 PNG；练习成果读取独立的 `NEXT_CARD_BONUS.png`。誊写（JK127）沿用反复研习（JK15）的书本图标，领悟（JK139）沿用晨星（JZ06）的能量图标。
