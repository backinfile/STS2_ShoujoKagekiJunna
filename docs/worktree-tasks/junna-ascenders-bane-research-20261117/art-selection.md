# 想太多：卡图选择

用户已选定“想太多”。卡图采用 **TV第2话结束Revue后，纯那抱膝坐在落下的幕布里、闭目叹气的近景**。

## 原作与成品

- 原片：[第2话《命运之舞台》](https://www.bilibili.com/bangumi/play/ep232368)，本地播放器约23:06。
- 原始截图：[tv02-2305-proof.jpg](../../design/assets/card-sources/2026-11-02/tv02-2305-proof.jpg)。原图保留，包含片尾名单与播放界面。
- 清理成品：[overthinking-clean.png](art/overthinking-clean.png)，1448×1086，4:3。
- 生成原图：[overthinking-restored-original.png](art/overthinking-restored-original.png)。
- 入框预览：[overthinking-card-preview.png](art/overthinking-card-preview.png)。

这帧把抱膝、叹气与反复思量放在同一构图里，能直接表达角色自身的纠结。人物居中，脸、眼镜、双手及膝盖在卡图区域中可辨认，幕布保留场景联系。

## 处理与检查

先检查Xenodude的[2024年第2话页面](https://xenodude.com/2024/06/19/revue-starlight-ep-2/)与[2018年原始页面](https://xenodude.com/2018/07/23/revue-starlight-ep-2-original/)，这些页面没有收录所选抱膝近景。最终采用仓库已有原作截图，经内置image_gen编辑清理字幕、片尾名单与播放界面，并改为4:3构图；成品属于基于原作帧的修复图。

使用本地 **GPT Image 2 Artifact Cleaner**，参数alpha **0.25**，CUDA，按原尺寸1448×1086处理。核对处理前后眼镜、发丝、双手与服装边缘，形状与线条清楚。原图和生成原图均保留，参数与SHA-256见 [cleanup.json](art/cleanup.json)，复现脚本见 [clean-art.py](clean-art.py)。

工作台通用卡框预览复用现有框、横幅、类型牌及排版比例，核对头部、眼镜与双手的可见范围。预览用于选图检查；运行牌尚未实现。生成脚本见 [render-art-preview.py](render-art-preview.py)。

查重记录：

- MCP revision 5389中，对卡牌和契合池分别检索幕布、抱膝、2305、23:06、23:05，均无已有来源记录。
- 对华恋116张、纯那225张本地卡图做感知哈希比较，人工复核各自最接近的两张，未发现同镜头或同一抱膝构图；详见 [local-image-similarity.json](art/local-image-similarity.json)。
- 与华恋“困意”的卧床近景区分清楚。

## 工作台保存

已保存到纯那契合池 **想太多**，条目编号 `FIT_JUNNA_ASCENDERS_BANE_OVERTHINKING`，包含成品图片、原作出处、修复说明及进阶之灾替换用途。MCP提交后已重新读取确认，revision **5392**；条目尚未关联运行卡牌。

完整元信息见 [art-selection.json](art-selection.json)，提交回执见 [workbench-receipt.json](art/workbench-receipt.json)，回读见 [workbench-readback.json](art/workbench-readback.json)。
