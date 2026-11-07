# 代码结构

## 入口与依赖

`loader/Bootstrap.cs` 按游戏版本加载实现程序集。`src/JunnaMod.cs` 注册牌堆、存档字段、伤害资格、Core 延迟结算及 Harmony 补丁。`src/Combat/JunnaCombatHooks.cs` 连接回合、战斗、出牌与死亡事件。

角色通过 Core V1 接口接入闪耀，运行依赖为 Core 1.0.1。`build.ps1` 先校验官方发行包及角色要求的最低版本，再编译正式版、测试版和加载器，导入资源并随包生成 Core 锁文件。

## 模块分工

| 目录 | 职责 |
| --- | --- |
| `src/Combat/Arrows/` | 箭矢牌堆、部署状态、调度、目标继承及类型转换 |
| `src/Combat/Cards/` | 选牌、费用、复制打出、变形与具体牌效 |
| `src/Combat/Damage/` | 伤害资格、实例加成、来源作用域、额外次数与原生指令接入 |
| `src/Combat/Knowledge/` | 成组获取、关联抽取、动态关联与永久进度 |
| `src/Models/` | 游戏模型、卡池元数据和知识组定义 |
| `src/UI/Arrows/` | 箭矢 HUD、布局、卡框、动画与移牌表现 |
| `src/UI/Cards/` | 强化标记、卡框特效与强化提示 |
| `src/UI/Knowledge/` | 知识组堆叠、平铺及获取页面预览 |
| `src/Compatibility/` | 正式版与测试版游戏接口差异 |

文件按职责归类，已有类型沿用原命名空间和 Godot UID，以兼容外部引用。牌堆使用游戏的自定义转场接口，箭矢调度在结算位置调用 HUD，表现变化集中在 `UI/Arrows/`。

`ArrowCombat` 负责调度，`ArrowDeployment` 保存一次部署的目标、回合数和重放状态，`ArrowPile` 接入原生牌堆。公共战斗钩子独立于箭矢文件维护。

`KnowledgeGroup.Members` 统一提供有序成员及数量，主牌在前，关联牌按配置顺序排列。缺少主牌数量或成员数量时沿用 1 张。发放、关联抽取、提示和预览读取同一规则。

## 数据与生成

`data/card-pool.json` 是编译输入，通过 `Junna.CardPool` 嵌入角色程序集。`docs/implementation/` 保存实现说明。工作台内容通过 MCP 维护，`tools/assets/generate-card-catalog.py` 读取 MCP 导出快照，生成运行卡池、模型声明、本地化和配图。

卡牌类名与模型 ID 保存在卡池定义中，运行时通过 `CardCatalog` 查找。`CardEffects` 显式分派牌效，`CatalogPower` 按游戏钩子分派持续效果。两处使用相同卡池 ID，修改对应牌效时同时核对其能力、描述及事件。

## 构建与定向检查

`Directory.Build.props` 统一 .NET 版本、空值检查、隐式引用、游戏目录和 NuGet 依赖版本。通过 `-p:Sts2GameDir=<游戏目录>` 覆盖本机默认路径。Core API 由官方发行包提供，通过 `CoreApiPath` 传给工程。

主 Godot 工程显式声明 `net9.0`。MegaDot 的编辑器会在该声明缺失时写入 `net8.0`，因此这一项同时保留在主工程中。

```powershell
./build.ps1 -CorePackage <Core发行目录>
dotnet run --project tests/KnowledgeGroups/KnowledgeGroupChecks.csproj -c Release
dotnet run --project tests/ArrowLayout/ArrowLayoutChecks.csproj -c Release
dotnet build tests/Integration/JunnaIntegration.csproj -c Release -p:CoreApiPath=<Core的API程序集>
```

`tests/Integration/` 使用 SDK 默认源码发现，新增探针直接放入该目录。测试模组与角色发布程序集分别构建。

2026-11-06 结构审查将战斗规则与表现文件归类，拆开箭矢文件中的多项职责，集中知识组数量规则和公共构建配置，并移除了闲置的伤害描述拼接方法。工作台应用已有独立的领域、统计、预览和 MCP 模块，源码与部署入口继续在 `docs/design/workbench/` 维护。
