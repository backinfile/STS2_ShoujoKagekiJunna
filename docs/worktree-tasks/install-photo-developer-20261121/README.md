# 同步显影液到本机 Mod

## 用户输入

> 同步到mod中 [$worktree-task](<worktree-task 技能路径>)

## 计划

核对工作台和已集成的 Mod 源码。复用经过实机验证的显影液安装包，在工作树内校验内容。关闭状态下替换游戏目录中的旧版纯那 Mod，保留旧版备份，并逐文件核对安装结果。

## 结果

- 工作台 r5957 中 JP02 为「显影液」，选择任意手牌作为 3 回合的飞矢打出。工作台的原创舞台少女背影图标与提交 8e8c9a2 中的 Mod PNG SHA-256 一致。
- 复用 [显影液安装包](../../../artifacts/photo-developer-release-20261121/Junna-photo-developer-mod.zip)，核对归档的 8 个文件和 CRC。游戏 v0.113.1 使用已安装的 Core 1.9.8 与 BaseLib 3.4.8，Core 提供所需的 17 项特性。
- 游戏关闭时将旧安装移到 [旧版备份](../../../artifacts/photo-developer-release-20261121/installed-mod-backup/)，再更新游戏的 `mods/ShoujoKagekiJunna/`。回读 8 个生效文件与经过验证的安装包逐文件 SHA-256 一致，备份也与更新前逐文件一致。
- 安装内容沿用通过 13 项实机断言的构建，本轮验证范围为源图、归档和安装文件校验。[实现与实机验证记录](../sports-drink-fit-20261121/README.md)。
