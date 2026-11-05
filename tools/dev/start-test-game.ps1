param([int]$Port = 15727, [switch]$UseInstalledMod,
    [ValidatePattern('^[a-z0-9-]+$')][string]$Instance = 'dev-game')
$ErrorActionPreference = 'Stop'
$junnaRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$junnaArtifacts = Join-Path $junnaRoot "artifacts/$Instance"
$junnaGame = Join-Path $junnaArtifacts 'game'
$junnaUser = Join-Path $junnaArtifacts 'user'
$sourceGame = 'D:/App/Stream/steamapps/common/Slay the Spire 2'
New-Item -ItemType Directory -Force $junnaGame, $junnaUser | Out-Null
$settingsPath = Join-Path $junnaUser 'AppData/Roaming/SlayTheSpire2/default/1/settings.save'
New-Item -ItemType Directory -Force (Split-Path $settingsPath -Parent) | Out-Null
$settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json -AsHashtable } else { @{ schema_version=8 } }
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
$basePackage = 'C:/Users/17575/.nuget/packages/alchyr.sts2.baselib/3.4.7'
Copy-Item -LiteralPath "$basePackage/lib/net9.0/BaseLib.dll", "$basePackage/Content/BaseLib.json", "$basePackage/Content/BaseLib.pck" -Destination "$mods/BaseLib"
if (!$UseInstalledMod) {
    Copy-Item -Path "$junnaRoot/dist/ShoujoKagekiJunna/*" -Destination "$mods/ShoujoKagekiJunna" -Recurse -Force
}
Copy-Item -LiteralPath "$junnaRoot/.tools/implementation/mcp/KarenSTS2MCP.dll" -Destination $mods
Copy-Item -LiteralPath 'D:/Github/STS2_Mcp/mod_manifest.json' -Destination "$mods/KarenSTS2MCP.json"
@{port=$Port; fullscreen=$false; muted=$false; silent_test=$true; skip_tutorial=$true; skip_first_prompt=$true; window_width=1280; window_height=720} |
    ConvertTo-Json | Set-Content -LiteralPath "$mods/KarenSTS2MCP.conf" -Encoding utf8
@'
[display]
window/size/mode=1
window/size/no_focus=true
window/size/viewport_width=1280
window/size/viewport_height=720
[audio]
driver/enable_input=false
'@ | Set-Content -LiteralPath "$junnaGame/override.cfg" -Encoding utf8
$oldAppData = $env:APPDATA
$oldLocalAppData = $env:LOCALAPPDATA
$oldFfmpeg = $env:STS2_MCP_FFMPEG
try {
    $env:APPDATA = Join-Path $junnaUser 'AppData/Roaming'
    $env:LOCALAPPDATA = Join-Path $junnaUser 'AppData/Local'
    $env:STS2_MCP_FFMPEG = (Get-Command ffmpeg).Source
    New-Item -ItemType Directory -Force $env:APPDATA, $env:LOCALAPPDATA | Out-Null
    $junnaProcess = Start-Process -FilePath "$junnaGame/SlayTheSpire2.exe" -WorkingDirectory $junnaGame -WindowStyle Hidden -PassThru `
        -ArgumentList '--force-steam=off','--rendering-method','gl_compatibility','--log-file',"$junnaArtifacts/game.log"
    $junnaProcess.Id | Set-Content -LiteralPath "$junnaArtifacts/process-id.txt"
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
