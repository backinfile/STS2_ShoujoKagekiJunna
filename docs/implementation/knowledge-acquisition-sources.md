# 知识牌获取来源清单

关联牌补发采用白名单。只放行登记的具体类型和方法；未登记的来源、通用入牌和通用变化默认只产生指定卡牌。复制、显式单张生成及关联牌自身的发放不递归补发。

表中“堆叠”指候选牌界面中的知识组预览，要求卡牌已有可显示的关联牌。堆叠展示和关联牌补发分别判断；现有预览界面不因来源登记状态改变。

核对游戏：测试版 0.111.0。下表原生类型均以 `MegaCrit.Sts2.Core.Models.` 为前缀。调用行对应 `out/tmp/acquisition-sources-20261111/native/<类别>.<模型>.cs` 的反编译快照；同一方法的多个入牌位置分别列出。

运行登记：[KnowledgeAcquisitionSources.cs](../../src/Combat/Knowledge/KnowledgeAcquisitionSources.cs)。通用入牌判定：[KnowledgeCombat.cs](../../src/Combat/Knowledge/KnowledgeCombat.cs)。

## 奖励、商店和查看入口

| 具体位置 | 堆叠 | 补发关联牌 | 状态 |
| --- | --- | --- | --- |
| 战斗卡牌奖励：`CardReward` 构造函数的 `CardCreationSource.Encounter` → `CardReward.OnSelect` | 是 | 是 | 已适配 |
| 通用获取网格：`NSimpleCardSelectScreen`，候选卡牌尚未入堆 | 是 | 依具体来源白名单 | 堆叠、滚轮及右键详情已适配；已有牌堆中的卡牌保持实际单张显示 |
| 商店售牌：`MerchantCardEntry.OnTryPurchase` → `CreationResult.Card` 入永久牌组 | 是 | 是 | 已适配，只处理本次购买的牌 |
| 未登记来源创建的奖励：`CardReward.OnSelect` 的来源校验失败分支 | 是 | 否 | 未适配，默认不补发 |
| 普通永久入牌：`CardPileCmd.Add(IEnumerable<CardModel>, CardPile, …)`，未命中来源登记 | 否 | 否 | 默认不补发 |
| 普通永久变化：`CardCmd.Transform(IEnumerable<CardTransformation>, …)`，未命中来源登记 | 否 | 否 | 默认不补发 |
| 图鉴候选牌：`KnowledgeCardStack.IsSupported` 的 `NCardLibraryGrid` 分支 | 是 | 否 | 仅查看 |
| 卡牌详情：`KnowledgeGroupPreview.Prepare` → `Refresh`，默认平铺，可切换堆叠 | 可切换 | 否 | 仅查看 |

## 事件的具体选项

| 名称 | 触发位置 | 原生类型、方法和调用行 | 堆叠 | 补发 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 熔合者 | 选项「融合打击」 | `Events.Amalgamator.CombineStrikes`，入牌调用第 51 行 | 否 | 否 | 未适配，默认不补发 |
| 熔合者 | 选项「融合防御」 | `Events.Amalgamator.CombineDefends`，入牌调用第 65 行 | 否 | 否 | 未适配，默认不补发 |
| 混沌芳香 | 选项「放任自流」 | `Events.AromaOfChaos.LetGo`，变化调用第 33 行 | 否 | 是 | 已适配，补发关联牌 |
| 脑蛭 | 选项「分享知识」 | `Events.BrainLeech.ShareKnowledge` → `EventModel.SelectCardsToAddToDeckFromGrid` | 是 | 是 | 已适配，5 选 1，仅为选中的知识牌补发 |
| 脑蛭 | 选项「把它扯下来」 | `Events.BrainLeech.Rip`，卡牌奖励调用第 57 行 | 是 | 是 | 已适配，补发关联牌 |
| 害虫杀手 | 选项「学习压扁的技巧」 | `Events.Bugslayer.Squash`，入牌调用第 43 行 | 否 | 否 | 未适配，默认不补发 |
| 多尼斯异鸟巢 | 选项「带走这颗蛋」 | `Events.ByrdonisNest.Take`，入牌调用第 48 行 | 否 | 否 | 未适配，默认不补发 |
| 色彩哲学家 | 初始页选择其他角色颜色后的三个稀有度奖励 | `Events.ColorfulPhilosophers.OfferRewards`，卡牌奖励调用第 61 行 | 是 | 是 | 已适配，补发关联牌 |
| 色彩哲学家 | 初始页选择其他角色颜色后的三个稀有度奖励 | `Events.ColorfulPhilosophers.OfferRewards`，卡牌奖励调用第 62 行 | 是 | 是 | 已适配，补发关联牌 |
| 色彩哲学家 | 初始页选择其他角色颜色后的三个稀有度奖励 | `Events.ColorfulPhilosophers.OfferRewards`，卡牌奖励调用第 63 行 | 是 | 是 | 已适配，补发关联牌 |
| 水晶球 | 选项「分期付款」 | `Events.CrystalSphere.PaymentPlan`，入牌调用第 77 行 | 否 | 否 | 未适配，默认不补发 |
| 无尽传送带 | 选项「从传送带上拿份果冻肝」 | `Events.EndlessConveyor.JellyLiver`，变化调用第 178 行 | 否 | 是 | 已适配，补发关联牌 |
| 无尽传送带 | 选项「从传送带上拿份海洋混混沙拉」 | `Events.EndlessConveyor.SeapunkSalad`，入牌调用第 185 行 | 否 | 否 | 未适配，默认不补发 |
| 无尽传送带 | 选项「从传送带上拿份炸鳗鱼」 | `Events.EndlessConveyor.FriedEel`，入牌调用第 192 行 | 否 | 否 | 未适配，默认不补发 |
| 人形洞穴之地 | 选项「抵抗诱惑」 | `Events.FieldOfManSizedHoles.Resist`，入牌调用第 48 行 | 否 | 否 | 未适配，默认不补发 |
| 遗忘之墓 | 选项「让它直面真相」 | `Events.GraveOfTheForgotten.Confront`，入牌调用第 53 行 | 否 | 否 | 未适配，默认不补发 |
| 被寄生的自动机械 | 选项「学习」 | `Events.InfestedAutomaton.Study`，入牌调用第 34 行 | 否 | 否 | 未适配，默认不补发 |
| 被寄生的自动机械 | 选项「触碰核心」 | `Events.InfestedAutomaton.TouchCore`，入牌调用第 49 行 | 否 | 否 | 未适配，默认不补发 |
| 迷失鬼火 | 选项「抓住这团鬼火」 | `Events.LostWisp.Claim`，入牌调用第 49 行 | 否 | 否 | 未适配，默认不补发 |
| 冷光合唱团 | 选项「探入菌肉」 | `Events.LuminousChoir.ReachIntoTheFlesh`，入牌调用第 57 行 | 否 | 否 | 未适配，默认不补发 |
| 变形灵林谷 | 选项「大群变形灵」 | `Events.MorphicGrove.Group`，变化调用第 50 行 | 否 | 是 | 已适配，补发关联牌 |
| 重拳出击 | 选项「顺走」 | `Events.PunchOff.Nab`，入牌调用第 109 行 | 否 | 否 | 未适配，默认不补发 |
| 镜中倒影  影倒中镜 | 选项「打碎」 | `Events.Reflections.Shatter`，复制调用第 68 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 镜中倒影  影倒中镜 | 选项「打碎」 | `Events.Reflections.Shatter`，入牌调用第 69 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 镜中倒影  影倒中镜 | 选项「打碎」 | `Events.Reflections.Shatter`，入牌调用第 73 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 灵魂嫁接者 | 选项「接纳」 | `Events.SpiritGrafter.LetItIn`，入牌调用第 41 行 | 否 | 否 | 未适配，默认不补发 |
| 淹水金库 | 选项「第二个箱子」 | `Events.SunkenTreasury.SecondChest`，入牌调用第 48 行 | 否 | 否 | 未适配，默认不补发 |
| 共生体 | 选项「用火烧杀」 | `Events.Symbiote.KillWithFire`，变化调用第 72 行 | 否 | 是 | 已适配，补发关联牌 |
| 药水的未来？ | 初始页交出所选药水后 | `Events.TheFutureOfPotions.Trade`，卡牌奖励调用第 128 行 | 是 | 是 | 已适配，补发关联牌 |
| 传说是真的 | 选项「顺走地图」 | `Events.TheLegendsWereTrue.NabTheMap`，入牌调用第 44 行 | 否 | 否 | 未适配，默认不补发 |
| 这个还是那个？ | 选项「那个」 | `Events.ThisOrThat.Ornate`，入牌调用第 48 行 | 否 | 否 | 未适配，默认不补发 |
| 打造时间 | 完成卡牌组件选择后 | `Events.TinkerTime.RiderChosen`，入牌调用第 158 行 | 否 | 否 | 未适配，默认不补发 |
| 垃圾堆 | 选项「随便拿点垃圾」 | `Events.TrashHeap.Grab`，入牌调用第 74 行 | 否 | 否 | 未适配，默认不补发 |
| 审判 | 选项「判决：有罪」 | `Events.Trial.MerchantGuilty`，入牌调用第 146 行 | 否 | 否 | 未适配，默认不补发 |
| 审判 | 选项「判决：无罪」 | `Events.Trial.MerchantInnocent`，入牌调用第 156 行 | 否 | 否 | 未适配，默认不补发 |
| 审判 | 选项「判决：无罪」 | `Events.Trial.NobleInnocent`，入牌调用第 172 行 | 否 | 否 | 未适配，默认不补发 |
| 审判 | 选项「判决：有罪」 | `Events.Trial.NondescriptGuilty`，入牌调用第 179 行 | 否 | 否 | 明确不补发，此处直接发牌不属于奖励选择 |
| 审判 | 选项「判决：有罪」 | `Events.Trial.NondescriptGuilty`，卡牌奖励调用第 183 行 | 是 | 是 | 已适配，补发关联牌 |
| 审判 | 选项「判决：无罪」 | `Events.Trial.NondescriptInnocent`，入牌调用第 191 行 | 否 | 否 | 明确不补发，此处直接发牌不属于奖励选择 |
| 审判 | 选项「判决：无罪」 | `Events.Trial.NondescriptInnocent`，变化调用第 195 行 | 否 | 是 | 已适配，补发关联牌 |
| 无休之处 | 选项「就这样休息」 | `Events.UnrestSite.Rest`，入牌调用第 48 行 | 否 | 否 | 未适配，默认不补发 |
| 泉水 | 选项结算中增加内疚时 | `Events.Wellspring.AddGuilty`，入牌调用第 54 行 | 否 | 否 | 未适配，默认不补发 |
| 低语空谷 | 选项「拥抱树木」 | `Events.WhisperingHollow.Hug`，变化调用第 66 行 | 否 | 是 | 已适配，补发关联牌 |
| 木雕 | 选项「鸟」 | `Events.WoodCarvings.Bird`，变化调用第 58 行 | 否 | 是 | 已适配，补发关联牌 |
| 木雕 | 选项「圆环」 | `Events.WoodCarvings.Torus`，变化调用第 83 行 | 否 | 是 | 已适配，补发关联牌 |
| 修禅织网者 | 选项「呼吸技法」 | `Events.ZenWeaver.BreathingTechniques`，入牌调用第 54 行 | 否 | 否 | 未适配，默认不补发 |

## 遗物的具体触发位置

| 名称 | 触发位置 | 原生类型、方法和调用行 | 堆叠 | 补发 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 奥术卷轴 | 获得遗物时 | `Relics.ArcaneScroll.AfterObtained`，入牌调用第 27 行 | 否 | 是 | 已适配，补发关联牌 |
| 古老牙齿 | 获得遗物时 | `Relics.ArchaicTooth.AfterObtained`，变化调用第 171 行 | 否 | 是 | 已适配，补发关联牌 |
| 星盘 | 获得遗物时 | `Relics.Astrolabe.AfterObtained`，变化调用第 27 行 | 否 | 是 | 已适配，补发关联牌 |
| 大帽子 | 阵营回合开始后 | `Relics.BigHat.AfterSideTurnStart`，入牌调用第 35 行 | 否 | 否 | 未适配，默认不补发 |
| 大～抱抱 | 洗牌后 | `Relics.BiiigHug.AfterShuffle`，入牌调用第 35 行 | 否 | 否 | 未适配，默认不补发 |
| 宾邦 | 卡牌改变牌区后 | `Relics.BingBong.AfterCardChangedPiles`，复制调用第 34 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 宾邦 | 卡牌改变牌区后 | `Relics.BingBong.AfterCardChangedPiles`，入牌调用第 36 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 赐福鹿角 | 抽取回合手牌前 | `Relics.BlessedAntler.BeforeHandDraw`，入牌调用第 55 行 | 否 | 否 | 未适配，默认不补发 |
| 血染玫瑰 | 获得遗物时 | `Relics.BloodSoakedRose.AfterObtained`，入牌调用第 33 行 | 否 | 否 | 未适配，默认不补发 |
| 燃烧木棍 | 卡牌消耗后 | `Relics.BurningSticks.AfterCardExhausted`，复制调用第 49 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 燃烧木棍 | 卡牌消耗后 | `Relics.BurningSticks.AfterCardExhausted`，入牌调用第 50 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 异鸟宝宝 | 获得遗物时 | `Relics.Byrdpip.AfterObtained`，变化调用第 56 行 | 否 | 是 | 已适配，补发关联牌 |
| 召唤铃铛 | 获得遗物时 | `Relics.CallingBell.AfterObtained`，入牌调用第 29 行 | 否 | 否 | 未适配，默认不补发 |
| 选择悖论 | 战斗首回合开始后 | `Relics.ChoicesParadox.AfterPlayerTurnStart`，入牌调用第 48 行 | 是 | 是 | 已适配，5 选 1，保留原生保留和满手牌处理 |
| 利爪 | 获得遗物时 | `Relics.Claws.AfterObtained`，变化调用第 29 行 | 否 | 是 | 已适配，补发关联牌 |
| 十字弓 | 阵营回合开始后 | `Relics.Crossbow.AfterSideTurnStart`，入牌调用第 36 行 | 否 | 否 | 未适配，默认不补发 |
| 诅咒珍珠 | 获得遗物时 | `Relics.CursedPearl.AfterObtained`，入牌调用第 21 行 | 否 | 否 | 未适配，默认不补发 |
| 卓越斗篷 | 获得遗物时 | `Relics.DistinguishedCape.AfterObtained`，入牌调用第 42 行 | 否 | 否 | 未适配，默认不补发 |
| 卓越斗篷 | 获得遗物时 | `Relics.DistinguishedCape.AfterObtained`，入牌调用第 51 行 | 否 | 否 | 未适配，默认不补发 |
| 多利之镜 | 获得遗物时 | `Relics.DollysMirror.AfterObtained`，复制调用第 21 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 多利之镜 | 获得遗物时 | `Relics.DollysMirror.AfterObtained`，入牌调用第 22 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 寻龙尺 | 获得遗物时 | `Relics.DowsingRod.AfterObtained`，入牌调用第 22 行 | 否 | 否 | 未适配，默认不补发 |
| 捕梦网 | 休息处休息后的奖励 | `Relics.DreamCatcher.TryModifyRestSiteHealRewards`，卡牌奖励调用第 22 行 | 是 | 是 | 已适配，补发关联牌 |
| 尘封魔典 | 获得遗物时 | `Relics.DustyTome.AfterObtained`，入牌调用第 62 行 | 否 | 是 | 已适配，补发关联牌 |
| 菲涅耳透镜 | 执行 EnchantCard 时 | `Relics.FresnelLens.EnchantCard`，复制调用第 71 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 冻结之蛋 | 执行 TryModifyCardBeingAddedToDeck 时 | `Relics.FrozenEgg.TryModifyCardBeingAddedToDeck`，复制调用第 58 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 葬礼面具 | 抽取回合手牌前 | `Relics.FuneraryMask.BeforeHandDraw`，入牌调用第 31 行 | 否 | 否 | 未适配，默认不补发 |
| 玻璃眼珠 | 获得遗物时 | `Relics.GlassEye.AfterObtained`，卡牌奖励调用第 30 行 | 是 | 是 | 已适配，补发关联牌 |
| 亮片 | 执行 TryModifyCardRewardOptionsLate 时 | `Relics.Glitter.TryModifyCardRewardOptionsLate`，复制调用第 30 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 沉重石板 | 获得遗物时 | `Relics.HeftyTablet.AfterObtained`，入牌调用第 38 行 | 是 | 是 | 已适配，仅为选中的知识牌补发 |
| 宝石面具 | 抽取回合手牌前 | `Relics.JeweledMask.BeforeHandDraw`，入牌调用第 35 行 | 否 | 否 | 未适配，默认不补发 |
| 珠宝盒 | 获得遗物时 | `Relics.JewelryBox.AfterObtained`，入牌调用第 22 行 | 否 | 否 | 未适配，默认不补发 |
| 万花筒 | 获得遗物时 | `Relics.Kaleidoscope.AfterObtained`，卡牌奖励调用第 47 行 | 是 | 是 | 已适配，补发关联牌 |
| 巨大扭蛋 | 获得遗物时 | `Relics.LargeCapsule.AfterObtained`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 巨大扭蛋 | 获得遗物时 | `Relics.LargeCapsule.AfterObtained`，入牌调用第 34 行 | 否 | 否 | 未适配，默认不补发 |
| 熔岩灯 | 执行 TryModifyCardRewardOptionsLate 时 | `Relics.LavaLamp.TryModifyCardRewardOptionsLate`，复制调用第 83 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 铅制镇纸 | 获得遗物时 | `Relics.LeadPaperweight.AfterObtained`，入牌调用第 27 行 | 是 | 是 | 已适配，仅为选中的知识牌补发 |
| 树叶药膏 | 获得遗物时 | `Relics.LeafyPoultice.AfterObtained`，变化调用第 36 行 | 否 | 是 | 已适配，补发关联牌 |
| 失物盒 | 获得遗物时 | `Relics.LostCoffer.AfterObtained`，卡牌奖励调用第 20 行 | 是 | 是 | 已适配，补发关联牌 |
| 巨大卷轴 | 获得遗物时 | `Relics.MassiveScroll.AfterObtained`，入牌调用第 36 行 | 是 | 是 | 已适配，仅为选中的知识牌补发 |
| 熔火之蛋 | 执行 TryModifyCardBeingAddedToDeck 时 | `Relics.MoltenEgg.TryModifyCardBeingAddedToDeck`，复制调用第 62 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 音乐盒 | 打出卡牌后 | `Relics.MusicBox.AfterCardPlayed`，复制调用第 78 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 音乐盒 | 打出卡牌后 | `Relics.MusicBox.AfterCardPlayed`，入牌调用第 80 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 涅奥骨骰 | 获得遗物时 | `Relics.NeowsBones.AfterObtained`，入牌调用第 54 行 | 否 | 否 | 未适配，默认不补发 |
| 涅奥的牺牲 | 获得遗物时 | `Relics.NeowsSacrifice.AfterObtained`，入牌调用第 33 行 | 否 | 否 | 未适配，默认不补发 |
| 涅奥的苦痛 | 获得遗物时 | `Relics.NeowsTorment.AfterObtained`，入牌调用第 22 行 | 否 | 否 | 未适配，默认不补发 |
| 新叶 | 获得遗物时 | `Relics.NewLeaf.AfterObtained`，变化调用第 27 行 | 否 | 是 | 已适配，补发关联牌 |
| 橙色团块 | 阵营回合开始后 | `Relics.OrangeDough.AfterSideTurnStart`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 星系仪 | 获得遗物时 | `Relics.Orrery.AfterObtained`，卡牌奖励调用第 25 行 | 是 | 是 | 已适配，补发关联牌 |
| 佩尔之角 | 获得遗物时 | `Relics.PaelsHorn.AfterObtained`，入牌调用第 23 行 | 否 | 否 | 未适配，默认不补发 |
| 佩尔之牙 | 战斗结束后 | `Relics.PaelsTooth.AfterCombatEnd`，入牌调用第 109 行 | 否 | 否 | 未适配，默认不补发 |
| 潘多拉魔盒 | 获得遗物时 | `Relics.PandorasBox.AfterObtained`，变化调用第 25 行 | 否 | 是 | 已适配，补发关联牌 |
| 能量电池 | 阵营回合开始前 | `Relics.PowerCell.BeforeSideTurnStart`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 转经轮 | 生成战斗奖励时 | `Relics.PrayerWheel.TryModifyRewards`，卡牌奖励调用第 24 行 | 是 | 是 | 已适配，补发关联牌 |
| 腌制活雾 | 获得遗物时 | `Relics.PreservedFog.AfterObtained`，入牌调用第 26 行 | 否 | 否 | 未适配，默认不补发 |
| 发光珍珠 | 抽取回合手牌前 | `Relics.RadiantPearl.BeforeHandDraw`，入牌调用第 41 行 | 否 | 否 | 未适配，默认不补发 |
| 卷轴箱 | 获得遗物时 | `Relics.ScrollBoxes.AfterObtained`，入牌调用第 42 行 | 是 | 是 | 已适配，仅为选中的知识牌补发 |
| 海玻璃 | 获得遗物时 | `Relics.SeaGlass.AfterObtained`，入牌调用第 91 行 | 是 | 是 | 已适配，15 张候选可多选或全跳过，仅为选中的知识牌补发 |
| 原初之爪 | 获得遗物时 | `Relics.SereTalon.AfterObtained`，入牌调用第 38 行 | 否 | 否 | 未适配，默认不补发 |
| 华美发束 | 执行 TryModifyCardRewardOptionsLate 时 | `Relics.SilkenTress.TryModifyCardRewardOptionsLate`，复制调用第 67 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 白银熔炉 | 执行 TryModifyCardRewardOptionsLate 时 | `Relics.SilverCrucible.TryModifyCardRewardOptionsLate`，复制调用第 113 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 故事书 | 获得遗物时 | `Relics.Storybook.AfterObtained`，入牌调用第 20 行 | 否 | 否 | 未适配，默认不补发 |
| 坦克斯的哨子 | 获得遗物时 | `Relics.TanxsWhistle.AfterObtained`，入牌调用第 20 行 | 否 | 否 | 未适配，默认不补发 |
| 无礼之茶 | 战斗开始前 | `Relics.TeaOfDiscourtesy.BeforeCombatStart`，入牌调用第 60 行 | 否 | 否 | 未适配，默认不补发 |
| 工具箱 | 抽取回合手牌前 | `Relics.Toolbox.BeforeHandDraw`，入牌调用第 31 行 | 是 | 是 | 已适配，补发关联牌 |
| 毒素之蛋 | 执行 TryModifyCardBeingAddedToDeck 时 | `Relics.ToxicEgg.TryModifyCardBeingAddedToDeck`，复制调用第 58 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 烦人机关盒 | 玩家回合开始后 | `Relics.VexingPuzzlebox.AfterPlayerTurnStart`，入牌调用第 23 行 | 否 | 否 | 未适配，默认不补发 |
| 白星 | 生成战斗奖励时 | `Relics.WhiteStar.TryModifyRewards`，卡牌奖励调用第 29 行 | 是 | 是 | 已适配，补发关联牌 |
| 羽翼护符 | 执行 TryModifyCardRewardOptionsLate 时 | `Relics.WingCharm.TryModifyCardRewardOptionsLate`，复制调用第 41 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |

## 药水的具体使用位置

| 名称 | 触发位置 | 原生类型、方法和调用行 | 堆叠 | 补发 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 攻击药水 | 使用药水时 | `Potions.AttackPotion.OnUse`，入牌调用第 33 行 | 是 | 是 | 已适配，补发关联牌 |
| 瓶装潜能 | 使用药水时 | `Potions.BottledPotential.OnUse`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 无色药水 | 使用药水时 | `Potions.ColorlessPotion.OnUse`，入牌调用第 32 行 | 是 | 是 | 已适配，补发关联牌 |
| 宇宙药剂 | 使用药水时 | `Potions.CosmicConcoction.OnUse`，入牌调用第 33 行 | 否 | 否 | 未适配，默认不补发 |
| 预知之滴 | 使用药水时 | `Potions.DropletOfPrecognition.OnUse`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 液态记忆 | 使用药水时 | `Potions.LiquidMemories.OnUse`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 欧洛巴斯之酸 | 使用药水时 | `Potions.OrobicAcid.OnUse`，入牌调用第 40 行 | 否 | 否 | 未适配，默认不补发 |
| 能力药水 | 使用药水时 | `Potions.PowerPotion.OnUse`，入牌调用第 33 行 | 是 | 是 | 已适配，补发关联牌 |
| 技能药水 | 使用药水时 | `Potions.SkillPotion.OnUse`，入牌调用第 33 行 | 是 | 是 | 已适配，补发关联牌 |

## 原生卡牌的具体效果位置

| 名称 | 触发位置 | 原生类型、方法和调用行 | 堆叠 | 补发 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 富足 | 打出卡牌时 | `Cards.Abundance.OnPlay`，入牌调用第 40 行 | 是 | 是 | 已适配，补发关联牌 |
| 适应打击 | 打出卡牌时 | `Cards.AdaptiveStrike.OnPlay`，入牌调用第 31 行 | 否 | 否 | 未适配，默认不补发 |
| 万物一心 | 打出卡牌时 | `Cards.AllForOne.OnPlay`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 愤怒 | 打出卡牌时 | `Cards.Anger.OnPlay`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 天选 | 打出卡牌时 | `Cards.Anointed.OnPlay`，入牌调用第 24 行 | 否 | 否 | 未适配，默认不补发 |
| 下去！ | 打出卡牌时 | `Cards.Begone.OnPlay`，变化调用第 31 行 | 否 | 否 | 未适配，默认不补发 |
| 流星锤 | 抽取回合手牌前 | `Cards.Bolas.BeforeHandDraw`，入牌调用第 46 行 | 否 | 否 | 未适配，默认不补发 |
| 高速脱离 | 打出卡牌时 | `Cards.BoostAway.OnPlay`，入牌调用第 29 行 | 否 | 否 | 未适配，默认不补发 |
| 新生之喜 | 打出卡牌时 | `Cards.BundleOfJoy.OnPlay`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 捕捉灵魂 | 打出卡牌时 | `Cards.CaptureSpirit.OnPlay`，入牌调用第 35 行 | 否 | 否 | 未适配，默认不补发 |
| 冲锋！！ | 打出卡牌时 | `Cards.Charge.OnPlay`，变化调用第 30 行 | 否 | 否 | 未适配，默认不补发 |
| 碰撞轨迹 | 打出卡牌时 | `Cards.CollisionCourse.OnPlay`，入牌调用第 30 行 | 否 | 否 | 未适配，默认不补发 |
| 压缩 | 打出卡牌时 | `Cards.Compact.OnPlay`，变化调用第 46 行 | 否 | 否 | 未适配，默认不补发 |
| 宇宙冷漠 | 打出卡牌时 | `Cards.CosmicIndifference.OnPlay`，入牌调用第 49 行 | 否 | 否 | 未适配，默认不补发 |
| 迫降 | 打出卡牌时 | `Cards.CrashLanding.OnPlay`，入牌调用第 36 行 | 否 | 否 | 未适配，默认不补发 |
| 挽歌 | 打出卡牌时 | `Cards.Dirge.OnPlay`，入牌调用第 47 行 | 否 | 否 | 未适配，默认不补发 |
| 发现 | 打出卡牌时 | `Cards.Discovery.OnPlay`，入牌调用第 37 行 | 是 | 是 | 已适配，补发关联牌 |
| 声东击西 | 打出卡牌时 | `Cards.Distraction.OnPlay`，入牌调用第 31 行 | 否 | 否 | 未适配，默认不补发 |
| 探寻 | 执行 BeforeRoomEntered 时 | `Cards.Dowsing.BeforeRoomEntered`，变化调用第 63 行 | 否 | 否 | 未适配，默认不补发 |
| 清淤 | 打出卡牌时 | `Cards.Dredge.OnPlay`，入牌调用第 29 行 | 否 | 否 | 未适配，默认不补发 |
| 双持 | 打出卡牌时 | `Cards.DualWield.OnPlay`，复制调用第 35 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 双持 | 打出卡牌时 | `Cards.DualWield.OnPlay`，入牌调用第 36 行 | 否 | 否 | 未适配，默认不补发 |
| 强撑 | 打出卡牌时 | `Cards.FightThrough.OnPlay`，入牌调用第 31 行 | 否 | 否 | 未适配，默认不补发 |
| 微光 | 打出卡牌时 | `Cards.Glimmer.OnPlay`，入牌调用第 33 行 | 否 | 否 | 未适配，默认不补发 |
| 彼岸一瞥 | 打出卡牌时 | `Cards.GlimpseBeyond.OnPlay`，入牌调用第 38 行 | 否 | 否 | 未适配，默认不补发 |
| 坟冢爆射 | 打出卡牌时 | `Cards.Graveblast.OnPlay`，入牌调用第 34 行 | 否 | 否 | 未适配，默认不补发 |
| 守墓人 | 打出卡牌时 | `Cards.GraveWarden.OnPlay`，入牌调用第 33 行 | 否 | 否 | 未适配，默认不补发 |
| 护驾！！！ | 打出卡牌时 | `Cards.Guards.OnPlay`，变化调用第 34 行 | 否 | 否 | 未适配，默认不补发 |
| 污秽攻击 | 打出卡牌时 | `Cards.GunkUp.OnPlay`，入牌调用第 38 行 | 否 | 否 | 未适配，默认不补发 |
| 头槌 | 打出卡牌时 | `Cards.Headbutt.OnPlay`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 传承之锤 | 打出卡牌时 | `Cards.HeirloomHammer.OnPlay`，复制调用第 38 行 | 否 | 否 | 明确不补发，沿复制或原效果数量 |
| 传承之锤 | 打出卡牌时 | `Cards.HeirloomHammer.OnPlay`，入牌调用第 39 行 | 否 | 否 | 未适配，默认不补发 |
| 全息影像 | 打出卡牌时 | `Cards.Hologram.OnPlay`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 地狱之刃 | 打出卡牌时 | `Cards.InfernalBlade.OnPlay`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 花样百出 | 打出卡牌时 | `Cards.JackOfAllTrades.OnPlay`，入牌调用第 36 行 | 否 | 否 | 未适配，默认不补发 |
| 大奖 | 打出卡牌时 | `Cards.Jackpot.OnPlay`，入牌调用第 44 行 | 否 | 否 | 未适配，默认不补发 |
| 慷慨捐助 | 打出卡牌时 | `Cards.Largesse.OnPlay`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 疯狂科学 | 执行 ExecuteRider 时 | `Cards.MadScience.ExecuteRider`，入牌调用第 286 行 | 否 | 否 | 未适配，默认不补发 |
| 如此甚好 | 执行 AfterCardPlayedLate 时 | `Cards.MakeItSo.AfterCardPlayedLate`，入牌调用第 43 行 | 否 | 否 | 未适配，默认不补发 |
| 君权自授 | 打出卡牌时 | `Cards.ManifestAuthority.OnPlay`，入牌调用第 38 行 | 否 | 否 | 未适配，默认不补发 |
| 羽化 | 打出卡牌时 | `Cards.Metamorphosis.OnPlay`，入牌调用第 31 行 | 否 | 否 | 未适配，默认不补发 |
| 涅奥之怒 | 打出卡牌时 | `Cards.NeowsFury.OnPlay`，入牌调用第 39 行 | 否 | 否 | 未适配，默认不补发 |
| 群情激愤 | 打出卡牌时 | `Cards.Outrage.OnPlay`，入牌调用第 41 行 | 否 | 否 | 未适配，默认不补发 |
| 超频 | 打出卡牌时 | `Cards.Overclock.OnPlay`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 光子切割 | 打出卡牌时 | `Cards.PhotonCut.OnPlay`，入牌调用第 36 行 | 否 | 否 | 未适配，默认不补发 |
| 原始力量 | 打出卡牌时 | `Cards.PrimalForce.OnPlay`，变化调用第 31 行 | 否 | 否 | 未适配，默认不补发 |
| 类星体 | 打出卡牌时 | `Cards.Quasar.OnPlay`，入牌调用第 32 行 | 是 | 是 | 已适配，补发关联牌 |
| 剥夺 | 打出卡牌时 | `Cards.Reave.OnPlay`，入牌调用第 42 行 | 否 | 否 | 未适配，默认不补发 |
| 重启 | 打出卡牌时 | `Cards.Reboot.OnPlay`，入牌调用第 27 行 | 否 | 否 | 未适配，默认不补发 |
| 得力助手 | 执行 AfterCardPlayedLate 时 | `Cards.RightHandHand.AfterCardPlayedLate`，入牌调用第 51 行 | 否 | 否 | 未适配，默认不补发 |
| 降灵 | 打出卡牌时 | `Cards.Seance.OnPlay`，变化调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 秘密技法 | 打出卡牌时 | `Cards.SecretTechnique.OnPlay`，入牌调用第 25 行 | 否 | 否 | 未适配，默认不补发 |
| 秘密武器 | 打出卡牌时 | `Cards.SecretWeapon.OnPlay`，入牌调用第 25 行 | 否 | 否 | 未适配，默认不补发 |
| 探寻打击 | 打出卡牌时 | `Cards.SeekerStrike.OnPlay`，入牌调用第 40 行 | 否 | 否 | 未适配，默认不补发 |
| 切断 | 打出卡牌时 | `Cards.Severance.OnPlay`，入牌调用第 32 行 | 否 | 否 | 未适配，默认不补发 |
| 切断 | 打出卡牌时 | `Cards.Severance.OnPlay`，入牌调用第 33 行 | 否 | 否 | 未适配，默认不补发 |
| 切断 | 打出卡牌时 | `Cards.Severance.OnPlay`，入牌调用第 34 行 | 否 | 否 | 未适配，默认不补发 |
| 小刀 | 执行 CreateInHand 时 | `Cards.Shiv.CreateInHand`，入牌调用第 103 行 | 否 | 否 | 未适配，默认不补发 |
| 灵魂 | 执行 CreateInHand 时 | `Cards.Soul.CreateInHand`，入牌调用第 34 行 | 否 | 否 | 未适配，默认不补发 |
| 飞溅 | 打出卡牌时 | `Cards.Splash.OnPlay`，入牌调用第 54 行 | 是 | 是 | 已适配，补发关联牌 |
| 添柴 | 打出卡牌时 | `Cards.Stoke.OnPlay`，入牌调用第 33 行 | 否 | 否 | 未适配，默认不补发 |
| 征召上前 | 打出卡牌时 | `Cards.SummonForth.OnPlay`，入牌调用第 40 行 | 否 | 否 | 未适配，默认不补发 |
| 狩猎 | 打出卡牌时 | `Cards.TheHunt.OnPlay`，卡牌奖励调用第 47 行 | 是 | 是 | 已适配，补发关联牌 |
| 深谋远虑 | 打出卡牌时 | `Cards.ThinkingAhead.OnPlay`，入牌调用第 29 行 | 否 | 否 | 未适配，默认不补发 |
| 无休手斧 | 抽取回合手牌前 | `Cards.ThrummingHatchet.BeforeHandDraw`，入牌调用第 40 行 | 否 | 否 | 未适配，默认不补发 |
| 内核加速 | 打出卡牌时 | `Cards.Turbo.OnPlay`，入牌调用第 30 行 | 否 | 否 | 未适配，默认不补发 |
| 指导 | 打出卡牌时 | `Cards.Tutor.OnPlay`，入牌调用第 28 行 | 否 | 否 | 未适配，默认不补发 |
| 不死 | 打出卡牌时 | `Cards.Undeath.OnPlay`，入牌调用第 26 行 | 否 | 否 | 未适配，默认不补发 |
| 白噪声 | 打出卡牌时 | `Cards.WhiteNoise.OnPlay`，入牌调用第 29 行 | 否 | 否 | 未适配，默认不补发 |
| 许愿 | 打出卡牌时 | `Cards.Wish.OnPlay`，入牌调用第 25 行 | 否 | 否 | 未适配，默认不补发 |

## 纯那及运行状态入口

| 具体位置 | 堆叠 | 补发关联牌 | 规则 |
| --- | --- | --- | --- |
| `KnowledgeCombat.GrantPartners`：发放当前知识牌的关联牌 | 否 | 否 | 关联牌自身不递归补发 |
| `KnowledgeCombat.GrantUpgrade`：学无止境每次实际升级产生新知 | 否 | 否 | 只生成升级效果要求的张数 |
| `CatalogPower.AfterCardPlayed` 的 `JK15` 分支：反复研习复制已打出的知识牌 | 否 | 否 | 单张复制 |
| `ArrowCombat.CopyAll` 的 `JA176` 入口：复制已有流星布置 | 否 | 否 | 按已有实际流星逐张复制 |
| `KnowledgeProgress.CopyDeckCard`：战后选择永久知识牌复制 | 否 | 否 | 单张永久复制 |
| `KnowledgeProgress.CopySingleCards`：闪耀复制奖励的每张选中牌 | 否 | 否 | 按选中的实际牌逐张复制 |
| `KnowledgeProgress.WonCombat` 的故事奖励循环：发放 `U_CARD_008B` | 否 | 否 | 按累计奖励次数发放指定牌 |
| `KnowledgeProgress.OpenCombat`：发放此前累计的开场洞见 | 否 | 否 | 按累计次数生成指定牌 |
| `CardActions.DrawMatching`：把选中的已有实际牌置顶后正常抽取 | 否 | 否 | 移动已有牌 |
| `CardActions.Recall`：召回已有牌到手牌 | 否 | 否 | 移动已有牌 |
| `Player.FromSerializable` → `DynamicKnowledge.RestoreGroups` | 否 | 否 | 恢复存档与关联信息，不补发牌 |
| `Player.SyncWithSerializedPlayer` → `DynamicKnowledge.SyncGroups` | 否 | 否 | 同步存档与关联信息，不补发牌 |

## 纯那生成牌的具体调用位置

| 卡牌及触发位置 | 实现位置 | 堆叠 | 补发关联牌 |
| --- | --- | --- | --- |
| 横扫（JA76） | `src/Combat/Cards/CardEffects.cs:194`，`Resolve` 的 `JA76` 分支 | 否 | 否 |
| 连射（JA81） | `src/Combat/Cards/CardEffects.cs:200`，`Resolve` 的 `JA81` 分支 | 否 | 否 |
| 箭雨（JA82） | `src/Combat/Cards/CardEffects.cs:203`，`Resolve` 的 `JA82` 分支 | 否 | 否 |
| 瞄准射击（JA145） | `src/Combat/Cards/CardEffects.cs:241`，`Resolve` 的 `JA145` 分支 | 否 | 否 |
| 稳步前进（JA144） | `src/Models/CatalogPower.Turns.cs:18`，`BeforeHandDraw` 的 `JA144` 生成 | 否 | 否 |
| 持续阅读（JK46） | `src/Models/CatalogPower.Turns.cs:29`，`AfterPlayerTurnStart` 的 `JK46` 分支 | 否 | 否 |
| 埋头学习（A） | `src/Models/CatalogPower.Turns.cs:57`，`BeforeSideTurnEnd` 的 `A` 分支 | 否 | 否 |

| 运行状态建立位置 | 堆叠 | 补发关联牌 |
| --- | --- | --- |
| `Player.PopulateStartingDeck` → `Player.PopulateDeck`：建立初始牌组 | 否 | 否 |
| `Player.PopulateCombatState(Rng, CombatState)`：从永久牌组逐张建立战斗副本 | 否 | 否 |

## 验证位置

`tests/AcquisitionSources/JunnaAcquisitionChecks.csproj` 单独编译本次定向探针。`junna_acquisition_sources check` 检查普通入牌、批量入牌、复制、通用变化、尘封魔典、显式单张覆盖、异步作用域退出及发现牌效。`reward` / `unknown-reward` 配合正常奖励选择检查已登记与未登记奖励。

`junna_event_copies setup` 和 `check` 检查镜中倒影「打碎」对实际牌组逐张复制；准备阶段显式建立测试牌组，不借用默认补发。

2026-11-11：主模组测试版和正式版编译通过，独立工程的 14 项定向检查全部通过，包含厚重石板选牌、星盘变化及毒素之卵升级后的兼收并蓄奖励。录像为 `out/tmp/acquisition-sources-20261111/acquisition-whitelist.mp4`。

现有综合探针工程有三处旧 `CardActions.Generate` 调用尚未提供必填位置参数：`KnowledgeGroupRegressionProbe.cs:57`、`CardPoolProbe.cs:161`、`CardBranchesProbe.cs:583`。本次使用上述独立工程编译和执行定向检查。

## 通用选牌网格的全部原生获取入口

对正式版 0.107.1 和 Beta 0.111.0 的完整程序集 IL 调用扫描得到以下 5 个具体场景。两版均由 `FromSimpleGridForRewards` 或 `FromSimpleGrid` 创建 `NSimpleCardSelectScreen`；Beta 的两个事件先经过 `EventModel.SelectCardsToAddToDeckFromGrid`，正式版直接调用奖励网格。未发现其他原生调用方。

| 场景 | 候选与选择 | 白名单具体方法 | 目标 |
| --- | --- | --- | --- |
| 脑蛭「分享知识」 | 5 选 1 | `Events.BrainLeech.ShareKnowledge` | 永久牌组 |
| 满屋芝士「大快朵颐」 | 8 选 2 | `Events.RoomFullOfCheese.Gorge` | 永久牌组 |
| 海玻璃 | 15 张候选，可多选或跳过 | `Relics.SeaGlass.AfterObtained` | 永久牌组 |
| 选择悖论 | 战斗首回合 5 选 1 | `Relics.ChoicesParadox.AfterPlayerTurnStart` | 战斗手牌 |
| SealedDeck 自定义模式 | 开局 30 选 10 | `Modifiers.SealedDeck.ChooseCards` | 初始牌组 |

上述获取入口均按具体来源登记。选择完成后只标记实际选中对象，再由通用获取流程补发知识关联牌。兼收并蓄保留其他候选对象作为本次关系，在批量入牌中同一关系成员只展开一次。

删牌使用 `NDeckCardSelectScreen`，升级、变化、附魔、战斗牌堆及手牌选择各有独立界面；不属于本表的通用获取网格。
