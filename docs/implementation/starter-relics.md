# 初始遗物

| 遗物 | 效果 | 获取 |
| --- | --- | --- |
| 绿色宝石 | 战斗开始时，获得1点能量并可保留到下一回合。 | 纯那初始遗物 |
| 燃烧宝石 | 战斗开始时，获得3点能量并可保留到下一回合。 | 奥罗巴斯之触替换绿色宝石 |

契合与数值通过 `junna_workbench` 写入 `meta.relics`、`meta.startingRelics` 及两个契合池条目。

绿色宝石取自[TV第2话约17:05—17:12](https://www.bilibili.com/bangumi/play/ep232368?t=1025)纯那弓端发光的宝石。核对素材为 `docs/design/assets/card-sources/2026-11-02/tv02-energy-gem-1704-20261103.png` 与 `tv02-energy-gem-1710-20261103.png`，名称是物件概括。

燃烧宝石取自[剧场版狩猎的Revue结尾](https://www.bilibili.com/video/BV1u3411v7i7/)奈奈形容纯那的“燃える宝石のようなキラめき”。两版沿用绿色宝石与中央星光，升级版增强亮度并增加绿色火光；3点能量表现更充分的行动力量。火光是图标设计，名称对应原文中的“燃える宝石”。

存档ID为 `JUNNA_GREEN_GEM` 与 `JUNNA_BURNING_GEM`，显示名称分别为绿色宝石与燃烧宝石。

图标位于 `ShoujoKagekiJunna/images/relics/`，均为128×128透明SVG，附独立白色轮廓。预览PNG由CairoSVG直接栅格化，未使用AI位图生成。

首次自己的回合刷新能量后、常规抽牌前结算，和本体开局额外能量的时序一致。按实际获得量增加一次性保留额度；下次刷新时，结转量为剩余能量与累计额度的较小值，随后清除额度。复用卡牌已有的 `EnergyReserve`，卡牌与遗物额度相加，完整保留能量时沿用原结转方式。

两版沿用本体升级初始遗物的 `Starter` 稀有度。`GetUpgradeReplacement()` 接入BaseLib 3.4.7的奥罗巴斯之触升级接口。

定向验证使用 `junna_relics check`，仅运行两版遗物相关检查；报告位于 `artifacts/starter-relics/results.json`。覆盖新开局、1／3点获得量、剩余0／1／9点时的限额结转、一次性交接、额度叠加、完整保留兼容、图标／本地化和奥罗巴斯之触实际替换。

2026-11-05验证：构建与打包通过，60项定向断言通过。升级后另开一场实战，不消耗能量，前三回合能量依次为6、6、3，保留状态在第二回合交接后清除。报告为 `artifacts/starter-relics/natural-turns.json`，开局截图为 `artifacts/starter-relics/upgraded-opening.png`。

最终契合采用用户选定的绿色宝石与燃烧宝石。两套语言的名称与图标路径同步更新。
