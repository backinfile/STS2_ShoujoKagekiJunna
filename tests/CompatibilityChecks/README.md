# 报错兼容回归

使用实际 beta 游戏、RitsuLib 0.6.10 与 Core API 程序集检查原版/联用出牌边界、前后监听及攻击命中桥。先构建 Junna，再从仓库根目录执行：

```powershell
dotnet run --project tests/CompatibilityChecks -c Release -- <Junna-Beta-DLL> <RitsuLib-directory> <Core-API-DLL>
```

实机卡面检查位于 `tests/CompatibilityProbe`，编译时提供 `-p:CoreApiPath=<Core-API-DLL>`。将探针 DLL 与依赖 Junna 的测试清单放入隔离游戏，进入战斗后通过 MCP 执行 `junna_compatibility check`。命令返回 JSON，检查真实原生/转化箭矢及普通卡的 NCard 预览、类型名称和知识获得补丁目标。测试产物位于隔离工作树。
