param([ValidateRange(0, 65535)][int]$Port = 0, [switch]$UseInstalledMod,
    [ValidatePattern('^[a-z0-9-]+$')][string]$Instance = 'dev-game',
    [switch]$Playable,
    [ValidateSet("forward_plus", "gl_compatibility")][string]$RenderingMethod = "forward_plus",
    [string]$CorePackage = $env:SHOUJO_CORE_PACKAGE,
    [string]$BaseLibPackage = $env:SHOUJO_BASELIB_PACKAGE,
    [string]$GameDir = 'D:/App/Stream/steamapps/common/Slay the Spire 2',
    [string]$McpPackage = $env:SHOUJO_TEST_MCP_PACKAGE)
$ErrorActionPreference = 'Stop'
$junnaRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$junnaArtifacts = Join-Path $junnaRoot "artifacts/$Instance"
$junnaGame = Join-Path $junnaArtifacts 'game'
$junnaUser = Join-Path $junnaArtifacts 'user'
$sourceGame = (Resolve-Path -LiteralPath $GameDir).Path
. (Join-Path $junnaRoot 'tools/core-package.ps1')
. (Join-Path $junnaRoot 'tools/game-variants.ps1')
$manifest = Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $junnaRoot 'ShoujoKagekiJunna.json') | ConvertFrom-Json
$minimumCoreVersion = ($manifest.dependencies | Where-Object { $_.id -eq 'ShoujoKagekiCore' }).min_version
$modSource = if ($UseInstalledMod) { Join-Path $junnaGame 'mods/ShoujoKagekiJunna' } else { Join-Path $junnaRoot 'dist/ShoujoKagekiJunna' }
foreach ($file in Get-JunnaRuntimeFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $modSource $file) -PathType Leaf)) { throw "Missing Junna runtime file: $file" }
}
$coreSource = if ($UseInstalledMod) { Join-Path $junnaGame 'mods/ShoujoKagekiCore' } else { $CorePackage }
$core = Get-CorePackage $coreSource $minimumCoreVersion
if (!$Playable) {
    if (!$McpPackage) { throw 'Set -McpPackage or SHOUJO_TEST_MCP_PACKAGE to the MCP test package directory.' }
    foreach ($name in @('KarenSTS2MCP.dll', 'KarenSTS2MCP.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $McpPackage $name) -PathType Leaf)) { throw "Missing MCP test file: $name" }
    }
} elseif (Test-Path -LiteralPath (Join-Path $junnaGame 'mods/KarenSTS2MCP.json')) {
    throw 'Use a separate -Instance for ordinary play; this instance is configured for MCP testing.'
}
if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $junnaGame 'SlayTheSpire2.exe') }) {
    throw "Close the $Instance game window before updating its mods."
}
$baseFiles = @('BaseLib.dll', 'BaseLib.json', 'BaseLib.pck', 'BaseLibCompatibilityHelper.dll', 'BaseLibCompatibilityHelper.json')
if (!$BaseLibPackage) {
    $steamApps = Split-Path -Parent (Split-Path -Parent $sourceGame)
    $baseCandidates = @(
        (Join-Path $sourceGame 'mods/BaseLib')
        (Join-Path $steamApps 'workshop/content/2868840/3737335127/BaseLib')
    )
    $BaseLibPackage = $baseCandidates | Where-Object {
        $candidate = $_
        @($baseFiles | Where-Object { !(Test-Path -LiteralPath (Join-Path $candidate $_) -PathType Leaf) }).Count -eq 0
    } | Select-Object -First 1
}
if (!$BaseLibPackage) {
    throw 'BaseLib 3.4.8 requires its full release. Set -BaseLibPackage or SHOUJO_BASELIB_PACKAGE to its extracted directory.'
}
foreach ($name in $baseFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $BaseLibPackage $name) -PathType Leaf)) {
        throw "Missing BaseLib release file: $name"
    }
}
if ($Port -eq 0) {
    $portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $portProbe.Start()
    $Port = $portProbe.LocalEndpoint.Port
    $portProbe.Stop()
}
$windowMode = 1
$noFocus = 'true'
$windowStyle = 'Hidden'
if ($Playable) {
    $windowMode = 0
    $noFocus = 'false'
    $windowStyle = 'Normal'
}
New-Item -ItemType Directory -Force $junnaGame, $junnaUser | Out-Null
$settingsPath = Join-Path $junnaUser 'AppData/Roaming/SlayTheSpire2/default/1/settings.save'
New-Item -ItemType Directory -Force (Split-Path $settingsPath -Parent) | Out-Null
$settings = @{ schema_version=8 }
if (Test-Path -LiteralPath $settingsPath) {
    $savedSettings = Get-Content -Raw -LiteralPath $settingsPath -Encoding utf8 | ConvertFrom-Json
    foreach ($property in $savedSettings.PSObject.Properties) { $settings[$property.Name] = $property.Value }
}
$settings.mod_settings = @{mods_enabled=$true;mod_list=@()}
$settings.fullscreen = $false
$settings.seen_ea_disclaimer = $true
$settings.skip_intro_logo = $true
$settings.language = 'zhs'
$settings.limit_fps_in_background = $false
$settings.volume_master = 0.5
$settings.window_size = @{X=1280;Y=720}
$settings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsPath -Encoding utf8
foreach ($file in Get-ChildItem -LiteralPath $sourceGame -File) {
    if ($file.Name -eq 'override.cfg') { continue }
    $targetFile = Join-Path $junnaGame $file.Name
    if (!(Test-Path -LiteralPath $targetFile)) { New-Item -ItemType HardLink -Path $targetFile -Target $file.FullName | Out-Null }
}
$dataLink = Join-Path $junnaGame 'data_sts2_windows_x86_64'
if (!(Test-Path -LiteralPath $dataLink)) {
    New-Item -ItemType Junction -Path $dataLink -Target (Join-Path $sourceGame 'data_sts2_windows_x86_64') | Out-Null
}
$mods = Join-Path $junnaGame 'mods'
New-Item -ItemType Directory -Force $mods, "$mods/BaseLib", "$mods/ShoujoKagekiJunna" | Out-Null
foreach ($name in $baseFiles) {
    Copy-Item -LiteralPath (Join-Path $BaseLibPackage $name) -Destination "$mods/BaseLib"
}
if (!$UseInstalledMod) {
    Copy-Item -Path "$modSource/*" -Destination "$mods/ShoujoKagekiJunna" -Recurse -Force
    New-Item -ItemType Directory -Force "$mods/ShoujoKagekiCore" | Out-Null
    Copy-Item -Path (Join-Path $core.Root '*') -Destination "$mods/ShoujoKagekiCore" -Recurse -Force
}
$legacyCore = Join-Path $mods 'ShoujoKagekiAijoCore'
if (Test-Path -LiteralPath $legacyCore -PathType Container) {
    foreach ($path in @($junnaArtifacts, $junnaGame, $mods, $legacyCore)) {
        if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "The legacy Core can only be moved in a local game instance: $path"
        }
    }
    $instanceRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $junnaArtifacts).Path)
    $legacyPath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $legacyCore).Path)
    $archivePath = [IO.Path]::GetFullPath((Join-Path $instanceRoot "ShoujoKagekiAijoCore-backup-$([Guid]::NewGuid().ToString('N'))"))
    $instancePrefix = $instanceRoot + [IO.Path]::DirectorySeparatorChar
    if (!$legacyPath.StartsWith($instancePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        !$archivePath.StartsWith($instancePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The legacy Core archive must remain in the $Instance instance."
    }
    Move-Item -LiteralPath $legacyPath -Destination $archivePath
    Write-Output "Moved the previous Core package outside mods: $archivePath"
}
if (!$Playable) {
    foreach ($name in @('KarenSTS2MCP.dll', 'KarenSTS2MCP.json')) {
        Copy-Item -LiteralPath (Join-Path $McpPackage $name) -Destination $mods -Force
    }
    @{port=$Port; fullscreen=$false; muted=$false; silent_test=$true; skip_tutorial=$true; skip_first_prompt=$true; window_width=1280; window_height=720} |
        ConvertTo-Json | Set-Content -LiteralPath "$mods/KarenSTS2MCP.conf" -Encoding utf8
}
@"
[display]
window/size/mode=$windowMode
window/size/no_focus=$noFocus
window/size/viewport_width=1280
window/size/viewport_height=720
[audio]
driver/enable_input=false
"@ | ForEach-Object { [System.IO.File]::WriteAllText("$junnaGame/override.cfg", $_, (New-Object System.Text.UTF8Encoding($false))) }
$oldAppData = $env:APPDATA
$oldLocalAppData = $env:LOCALAPPDATA
$oldFfmpeg = $env:STS2_MCP_FFMPEG
try {
    $env:APPDATA = Join-Path $junnaUser 'AppData/Roaming'
    $env:LOCALAPPDATA = Join-Path $junnaUser 'AppData/Local'
    if (!$Playable) { $env:STS2_MCP_FFMPEG = (Get-Command ffmpeg).Source }
    New-Item -ItemType Directory -Force $env:APPDATA, $env:LOCALAPPDATA | Out-Null
    $junnaProcess = Start-Process -FilePath "$junnaGame/SlayTheSpire2.exe" -WorkingDirectory $junnaGame -WindowStyle $windowStyle -PassThru `
        -ArgumentList '--force-steam=off','--rendering-method',$RenderingMethod,'--log-file',('"' + "$junnaArtifacts/game.log" + '"')
    $junnaProcess.Id | Set-Content -LiteralPath "$junnaArtifacts/process-id.txt"
    if ($Playable) {
        Write-Output "Junna play process=$($junnaProcess.Id)"
        return
    }
    Write-Output "Junna test process=$($junnaProcess.Id) MCP port=$Port"
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class JunnaWindowProbe {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint id);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
}
'@
    $samples = @()
    for ($i = 0; $i -lt 120; $i++) {
        [uint32]$foregroundPid=0
        [JunnaWindowProbe]::GetWindowThreadProcessId([JunnaWindowProbe]::GetForegroundWindow(),[ref]$foregroundPid) | Out-Null
        $junnaProcess.Refresh()
        $samples += @{elapsed_ms=$i*100;foreground=($foregroundPid -eq $junnaProcess.Id);has_window=($junnaProcess.MainWindowHandle -ne [IntPtr]::Zero);minimized=[JunnaWindowProbe]::IsIconic($junnaProcess.MainWindowHandle)}
        Start-Sleep -Milliseconds 100
    }
    $samples | ConvertTo-Json | Set-Content -LiteralPath "$junnaArtifacts/startup-window.json"
    Write-Output "Startup focus samples: $(@($samples | Where-Object foreground).Count) foreground / $($samples.Count) total"
} finally {
    $env:APPDATA = $oldAppData
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:STS2_MCP_FFMPEG = $oldFfmpeg
}
