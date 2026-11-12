# 武器与宝石参考

2026-11-12重新检索并直接查看了两本设定集的公开预览。

当前成品为 `art-v3/` 的初始版和 `art-v4/` 的升级火效版。v3按用户要求仅以原作参考从零独立生成两张新图；初始版采用弓端完整切面，升级版采用纯那短刃柄端不规则凸起与断面。v4在该升级宝石外侧添加剧场版燃烧段的金黄色光焰、白亮火芯及暖色晕光，参考狩猎片段05:45与06:09，保留绿色切面。两版均无包托、底座或刀柄，各有独立轮廓。提示词及所有输入参考记录见对应目录的prompts.json。原尺寸Artifact Cleaner采用strength=0.25、interior_blend=0.25，原图alpha和深色轮廓保持；处理记录、原尺寸对照、小尺寸预览、Godot资源检查位于对应目录。工作台r5007已同步升级火效，初始版文件哈希保持v3原值。下文保留参考核对与此前草稿过程。

## 九人武器

[《少女☆歌劇 レヴュースタァライト 設定資料集》公开预览](https://artsee.cc/collection/327)的第26张为武器章导言，第27—35张覆盖九人的Revue武器。下表为预览图片序号，不等同于书籍印刷页码。

| 角色 | 武器页标题 | 预览序号 | 已保存的参考图 |
| --- | --- | --- | --- |
| 爱城华恋 | Possibility of Puberty | 27 | [华恋](research/weapons/tv-preview-27.webp) |
| 神乐光 | Caliculus Bright / Blossom Bright | 28 | [光，含变化前后](research/weapons/tv-preview-28.webp) |
| 露崎真昼 | Love Judgement | 29 | [真昼](research/weapons/tv-preview-29.webp) |
| 西条克洛迪娜 | Etincelle de Fierte | 30 | [克洛迪娜](research/weapons/tv-preview-30.webp) |
| 天堂真矢 | Odette the Mavericks | 31 | [真矢](research/weapons/tv-preview-31.webp) |
| 星见纯那 | 翡翠弓 | 32 | [纯那，含宝石放大图](research/weapons/tv-preview-32.webp) |
| 大场奈奈 | 輪 / 舞 | 33 | [奈奈，含双刀](research/weapons/tv-preview-33.webp) |
| 石动双叶 | Determinator | 34 | [双叶](research/weapons/tv-preview-34.webp) |
| 花柳香子 | 水仙花 | 35 | [香子](research/weapons/tv-preview-35.webp) |

每页均有整体、细节与持握/比例参考，不同武器提供不同方向的视图。补充页：[第99回圣翔祭舞台道具，第36张](research/weapons/tv-preview-36.webp)；[伦敦Revue武器，第37张](research/weapons/tv-preview-37.webp)。

[九人武器对照表](research/weapons/all-nine-weapons.jpg)保留相同预览序号。

武器设计小出卓史在[导言](research/weapons/tv-preview-26.webp)说明：设计以适合Revue服装的18世纪左右西洋武器为基准，并为角色各自设置作为视觉焦点的宝石。色彩设计吉村智惠说明，先确认材质，再确定质感和色彩方向。

## 纯那宝石

2026-11-12用户复核后补充：弓端完整宝石与纯那短刃柄端的造型应分别核对。原升级草稿沿用完整弓端轮廓并加强亮光，未体现短刃设定的形状差异，暂缓合并。

- [原设定局部对照](research/crops/setting-comparison.png)：弓端为完整圆润切面；《Memory of Revue》书内119页的纯那脇差放大图标注“イビツ”（不规则）及“盛り上がり”（凸起），柄底另有强调粗糙、不规则外形的备注。不能将该细节简化为完整弓端宝石的同轮廓发光版。
- 奈奈原本刀柄的宝石另列：[TV设定预览33的局部](research/crops/nana-original-topaz.png)，原图标注“トパーズです”（托帕石）。该参考与剧场版纯那脇差的改造后外观分开记录。
- [弓端彩图及TV截图局部](research/crops/bow-color-references.png)；[纯那短刃刀柄、破碎宝石及光效局部](research/crops/short-blade-context.png)。独立裁切共11张，来源与原图矩形见 `research/crops/manifest.json`，保留原有绘画与文字。拼图只排版和放大显示，没有生成或补绘原图细节。

- 直接造型依据：第32张“宝石のUP”放大图。主体为圆润多边形的立体切面宝石，内部以不规则三角切面向中心聚拢。按用户要求最终图标提取完整宝石，移除弓身、包托和底座。
- 色彩依据：[TV第2话17:07近景](https://www.bilibili.com/bangumi/play/ep232368?t=1025)，项目已有截图 `docs/design/assets/card-sources/2026-11-02/tv02-1705-action.jpg`；其绿色大切面、青白反光与深绿阴影共同决定两版配色。
- 剧场版直接补充：[《Memory of Revue》公开预览](https://artsee.cc/collection/326)第115张有纯那弓与宝石的彩色角色页，[第122张、书内标示119页](research/weapons/movie-preview-122.webp)有纯那脇差及宝石嵌入柄底的细节。[第115张](research/weapons/movie-preview-115.webp)亦已保存。
- 材质线索：[读者记录](https://humandistortion.hatenablog.com/entry/2023/12/03/201419)称《Memory of Revue》确认纯那宝石为祖母绿；本轮查看的原设定图尚未找到对应材质文字，因此记录为待原文核实。游戏名称沿用绿色宝石。
- 升级视觉：同一切面布局增强内部青白光、青绿色溢光和短放射光。燃烧宝石的既有名称依据为狩猎的Revue中“燃える宝石のようなキラめき”；强化发光是本Mod的两版图标设计。

官方[出版公告](https://revuestarlight.com/news/9619/)及[书目](https://bushiroad-store.com/products/2000148901649)确认《Memory of Revue》收录剧场版Revue分镜与设定资料。

## 绘制与处理

- 采用内置 `image_gen`。首轮带包托草稿与原始提示词保留在 `art/`、`prompts.json`；用户修正后的最终原图、完整提示词位于 `art-v2/originals/`、`prompts-bare.json`。
- 本地工具：[GPT Image 2 Artifact Cleaner](https://github.com/Larryvrh/gpt-image-2-artifact-cleaner)，复用已安装的 LatentR R_ema 与 Flux2 VAE；CUDA bfloat16，strength=0.25、interior_blend=0.25。清理阶段原尺寸与alpha完全保留，深色轮廓及半透明边缘保持原像素。
- 清理脚本复用 `docs/worktree-tasks/junna-potions-workbench-20261111/process_potion_art.py`；配置由调用方传入本地已安装工具配置，配置本身不入库。记录见 `art-v2/cleanup.json`，原尺寸对比见 `art-v2/cleanup-native-comparison.png`。
- `prepare_icons.py`对两图采用共同裁切、共同缩放和共同定位，导出128×128透明PNG；白色轮廓沿基础宝石实体，排除升级溢光。实体与光效均保留透明边距。
- 游戏成品为 `ShoujoKagekiJunna/images/relics/{green_gem,burning_gem}.png` 及对应 `_outline.png`；32／48／64像素在深浅背景检查，见 `art-v2/preview.png`。
- 四张资源通过MegaDot 4.5.1原生导入、纹理尺寸/透明角检查及PCK写入重载，见 `art-v2/godot-validation.json`；数值检查见 `art-v2/validation.json`。按图标改动范围验证。
- 工作台r5001原子更新两个遗物及两个契合池条目的配图，并更新契合池图像说明；现有名称、数值与效果保持原内容。回执为 `workbench-receipt.json`。
