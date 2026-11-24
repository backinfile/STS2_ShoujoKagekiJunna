# Windows 工作树长路径清理

清理 [教科书伤害叠加工作树](../worktree-tasks/jk235-stack-damage/README.md) 时，`git worktree remove` 报 `Filename too long`。独立测试游戏的 Godot 着色器缓存把路径拉长到 292 个字符。Windows 的 `LongPathsEnabled` 已为 `1`，Git for Windows 仍需开启自己的长路径支持。该次删除先注销了工作树，磁盘上留下了部分目录。[微软长路径说明](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation) · [Git for Windows 长路径说明](https://gitforwindows.org/git-cannot-create-a-file-or-directory-with-a-long-path.html)

## 清理已注册的工作树

关闭独立测试游戏，确认任务提交已进入目标分支、工作树干净，交付文件已另存。检查测试实例的 `game/data_sts2_windows_x86_64` 是否为指向游戏安装目录的 junction。若存在，只移除该链接，避免递归操作进入安装目录。

在仓库根目录开启仓库级设置，替换任务目录占位符后清理。

```powershell
git config --local core.longpaths true
git config --show-origin --get core.longpaths
git worktree remove "../worktrees/STS2_ShoujoKagekiJunna/<task-slug>"
```

仅本次开启时，运行 `git -c core.longpaths=true worktree remove "<工作树路径>"`。[Git 工作树文档](https://git-scm.com/docs/git-worktree)

## 清理已注销的遗留目录

如果删除中断，先运行 `git worktree list --porcelain` 确认注册状态。工作树已注销、目录仍存在时，在 PowerShell 7 中核对目录和保留内容，再用 .NET 文件接口处理剩余目录。

```powershell
$name = '<task-slug>'
$parent = (Resolve-Path -LiteralPath '../worktrees/STS2_ShoujoKagekiJunna').Path
$target = (Resolve-Path -LiteralPath (Join-Path $parent $name)).Path
if ((Split-Path -Parent $target) -ne $parent -or (Split-Path -Leaf $target) -ne $name) {
    throw "工作树路径不匹配：$target"
}
if (Test-Path -LiteralPath (Join-Path $target '.git')) {
    throw "目录仍是 Git 工作树，先核对注册状态。"
}
$links = Get-Item -LiteralPath $parent, $target -Force |
    Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }
if ($links -or
    (Get-ChildItem -LiteralPath $target -Force -Recurse -Attributes ReparsePoint)) {
    throw "目录中存在重解析点，先核对并移除测试实例创建的链接。"
}
[IO.Directory]::Delete("\\?\$target", $true)
Test-Path -LiteralPath $target
```

本次用扩展长度绝对路径删除成功，`Test-Path` 返回 `False`。确认任务提交仍在目标分支后，用 `git branch -d <任务分支>` 删除已合并的本地分支。[.NET 路径格式](https://learn.microsoft.com/dotnet/standard/io/file-path-formats) · [Directory.Delete 文档](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.delete)
