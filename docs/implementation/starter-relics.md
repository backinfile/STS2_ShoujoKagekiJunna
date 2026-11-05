# 初始遗物

| 遗物 | 效果 | 获取 |
| --- | --- | --- |
| 绿色宝石 | 战斗开始时，获得1点能量并可保留到下一回合。 | 纯那初始遗物 |
| 燃烧宝石 | 战斗开始时，获得3点能量并可保留到下一回合。 | 奥罗巴斯之触替换绿色宝石 |

契合与数值通过 `junna_workbench` 写入 `meta.relics`、`meta.startingRelics` 及两个契合池条目。

绿色宝石取自[TV第2话约17:05—17:12](https://www.bilibili.com/bangumi/play/ep232368?t=1025)纯那弓端发光的宝石。核对素材为 `docs/design/assets/card-sources/2026-11-02/tv02-energy-gem-1704-20261103.png` 与 `tv02-energy-gem-1710-20261103.png`，名称是物件概括。

燃烧宝石取自[剧场版狩猎的Revue结尾](https://www.bilibili.com/video/BV1u3411v7i7/)奈奈形容纯那的“燃える宝石のようなキラめき”。名称对应原文中的“燃える宝石”，3点能量表现更充分的行动力量。

存档ID为 `JUNNA_GREEN_GEM` 与 `JUNNA_BURNING_GEM`，显示名称分别为绿色宝石与燃烧宝石。

图标位于 `ShoujoKagekiJunna/images/relics/`，均为128×128透明SVG，附独立白色轮廓。预览PNG由CairoSVG直接栅格化，未使用AI位图生成。

2026-11-05图标重绘：造型参考TV第2话17:07近景 `docs/design/assets/card-sources/2026-11-02/tv02-1705-action.jpg`，保留多面切割绿色主体，金色包托改为沿宝石侧面向上延伸的羽叶形。两版采用完全相同的主体切面、位置和包托轮廓；初始版使用深绿色切面与局部高光，升级版加强内部白光、青绿色溢光和放射光芒。光效参考狩猎片段05:29的 `film-hunt-528.jpg`，替换旧版外圈火焰设计。SVG使用Godot支持的线性及径向渐变；光晕保持透明，悬停白色轮廓只包围实体主体。

官方[《Memory of Revue》书目](https://bushiroad-store.com/products/2000148901649)确认收录剧场版分镜及未公开设定；[读者记录](https://humandistortion.hatenablog.com/entry/2023/12/03/201419)提到纯那宝石为祖母绿，材质线索待原设定页核对，当前造型依据为原作近景。工作台配图为 `relic-green-gem-vector-v2-20261105` 与 `relic-burning-gem-vector-v2-20261105`，MCP修订2586。旧版SVG、128像素原尺寸预览、32／48／64像素检查图及导入日志保存在 `artifacts/starter-relics/art-v2/`。

本次图标验证通过：Godot导入与PCK打包成功；实机分别显示两版遗物，确认径向渐变、透明边缘和遗物栏小尺寸显示；四张图标均为128×128，白色轮廓覆盖宝石与包托的实体主体。验证记录为 `artifacts/starter-relics/art-v2/validation.json`，实机截图为同目录的 `green-in-game.png` 与 `burning-in-game.png`。本次按图标改动范围验证，原有能量机制检查记录保留如下。

首次自己的回合刷新能量后、常规抽牌前结算，和本体开局额外能量的时序一致。按实际获得量增加一次性保留额度；下次刷新时，结转量为剩余能量与累计额度的较小值，随后清除额度。复用卡牌已有的 `EnergyReserve`，卡牌与遗物额度相加，完整保留能量时沿用原结转方式。

两版沿用本体升级初始遗物的 `Starter` 稀有度。`GetUpgradeReplacement()` 接入BaseLib 3.4.7的奥罗巴斯之触升级接口。

定向验证使用 `junna_relics check`，仅运行两版遗物相关检查；报告位于 `artifacts/starter-relics/results.json`。覆盖新开局、1／3点获得量、剩余0／1／9点时的限额结转、一次性交接、额度叠加、完整保留兼容、图标／本地化和奥罗巴斯之触实际替换。

2026-11-05验证：构建与打包通过，60项定向断言通过。升级后另开一场实战，不消耗能量，前三回合能量依次为6、6、3，保留状态在第二回合交接后清除。报告为 `artifacts/starter-relics/natural-turns.json`，开局截图为 `artifacts/starter-relics/upgraded-opening.png`。

最终契合采用用户选定的绿色宝石与燃烧宝石。两套语言的名称与图标路径同步更新。
