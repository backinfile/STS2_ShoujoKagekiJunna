# 舞台少女心得：真实跨回合验收

本规范依据运行卡池 r4878 的实际牌效：回合开始时从抽牌堆选择1张牌放入手牌，类别按知识→流星→闪耀循环。基础费用2，升级费用1。

## 旧记录的覆盖更正

旧版 `FullCardAuditProbe.CheckCycle` 通过 `IncrementTurnNumber` 和直接调用 `AfterPlayerTurnStart` 检查类别、缺少对象、升级、满手和重复安装。其通过结果保留为**钩子级检查**。旧录像只展示首次取牌；它不能证明真实的结束回合、敌方行动、自然抽牌和下回合轮换。

原逐牌条目的“基础结算”“升级结算”大多复述牌效，“持续触发”由类型模板生成。832个场景和20,417项断言是已执行的检查数量，不等于所有牌效分支都完成了实际流程验收。

## 正常流程：基础和升级各执行一次

准备：隔离 Beta 游戏，移除干扰能力；抽牌堆顶部放置足够的普通防御供自然抽牌，后面分别放6张立论、引弦、戒备。每类含一张升级牌，戒备的闪耀余量预设为2。以实际牌对象记录初始顺序、升级和闪耀状态。

所有推进均使用游戏的结束回合操作，完整经过敌方行动；选择界面使用原生选牌。观察器只记录候选、所选原牌和抽牌事件，不修改回合号或调用受测能力的回合钩子。

|步骤|操作|预期|
|---|---|---|
|安装|正常支付并打出舞台少女心得|基础扣2能量，升级扣1能量；当回合不弹选择，不取牌|
|第1次真实下回合|结束回合→敌方行动→开始回合；选择立论+|候选只有知识牌；所选原牌入手，升级保留|
|第2次真实下回合|同上，选择引弦+|候选只有流星牌；知识不再作为本次类别|
|第3次真实下回合|同上，选择戒备+|候选只有闪耀牌；升级保留，余量仍为2|
|第4次真实下回合|同上，选择立论|类别回到知识，证明循环回绕|
|第5次真实下回合|同上，选择引弦|第二轮流星，未停在知识|
|第6次真实下回合|同上，选择戒备|第二轮闪耀，余量仍为2|

每次检索额外断言：只选择1张实际候选；原牌进入手牌；不创建副本；检索不触发抽牌事件；正常自然抽牌仍为5张；除自然抽牌和所选牌外，抽牌堆原有相对顺序保持不变。每回合保存完整候选、所选对象编号、牌堆、升级、闪耀及抽牌事件。

## 独立进度与边界

|场景|操作|逐回合预期|
|---|---|---|
|延后重复安装|第一张安装；第1次真实下回合后，从自然抽到的手牌合法打出第二张|第1次：知识；第2次：第一张流星、第二张知识；第3次：第一张闪耀、第二张流星；第4次：第一张知识、第二张闪耀。分别记录来源快照身份|
|知识类别缺失|准备时抽牌堆不放知识牌，保留流星和闪耀；走6次真实下回合|空知识→流星→闪耀→空知识→流星→闪耀；空集合正常结束，不能阻塞或停留在知识|
|手牌已满|安装后准备10张保留的零费防御；走第1次真实下回合；合法打出6张防御，再继续两个回合|第1次：手满跳过知识，也无自然抽牌；腾出空间后，第2次：流星；第3次：闪耀。跳过仍推进类别|

## 执行方式与证据

测试模组：`tests/StageNotesTurns/NativeTurnProbe.cs`。脚本：`tests/StageNotesTurns/run-native-turns.py`。测试环境采用 BaseLib 3.4.7、Core 1.6.0，以及提交 f28e7a3 的 Junna Beta 包；新测试没有修改运行牌效。

先使用 `tools/dev/start-test-game.ps1 -Port 0` 准备隔离实例，并将测试输出的 `StageNotesTurns.dll` 和 `tests/StageNotesTurns/StageNotesTurns.json` 安装到该实例的 mods 目录。游戏启动前把 `JUNNA_CYCLE_OUTPUT` 指向观察输出目录。模组构建需传入 `CoreApiPath` 和 `JunnaAssemblyPath`。脚本的 `--output-dir` 必须与观察目录一致。

```powershell
dotnet build tests/StageNotesTurns/StageNotesTurns.csproj -c Release -p:CoreApiPath=<Core API DLL> -p:JunnaAssemblyPath=<Junna Beta DLL>
python tests/StageNotesTurns/run-native-turns.py --port <隔离端口> --output-dir <观察输出目录> --case normal --record
python tests/StageNotesTurns/run-native-turns.py --port <隔离端口> --output-dir <观察输出目录> --case normal --upgrade
python tests/StageNotesTurns/run-native-turns.py --port <隔离端口> --output-dir <观察输出目录> --case staggered
python tests/StageNotesTurns/run-native-turns.py --port <隔离端口> --output-dir <观察输出目录> --case missing
python tests/StageNotesTurns/run-native-turns.py --port <隔离端口> --output-dir <观察输出目录> --case capacity
```

执行结果：正常基础68项、正常升级68项、错开安装66项、缺少知识62项、满手27项，共291项通过，25个真实回合。满手场景脚本初次未等待出牌完成、重跑时读到旧快照和把合并图标数当作安装份数的问题已修正；准备试跑另存，不混入上述结果。

本轮结果与 Beta 录像保存在 `artifacts/task-deliverables/stage-notes-real-turns-20261111/`。用户展示版保留安装和知识→流星→闪耀→知识；完整原始录像保留六个实际回合。具体断言数量以该目录 `verification.json` 为准。

## 其余验证的状态

本轮补齐本牌的正常轮换、独立进度、缺少知识候选和满手边界。原生流程中的牌堆在选择期间被其他能力改变、移动失败、战斗结束后重装，以及与关联阅读/知识力量的完整组合，仍须独立场景证明，不能由本轮结果推定通过。对应旧钩子检查也不能替代这些证据。
