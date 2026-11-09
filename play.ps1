param(
    [string]$CorePackage = $env:SHOUJO_CORE_PACKAGE,
    [string]$Godot,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$playExecutable = Join-Path $PSScriptRoot 'artifacts/play-game/game/SlayTheSpire2.exe'
$runningGame = Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $playExecutable }
if ($runningGame) {
    Write-Output "Junna is already running (PID $($runningGame.Id)). Close that window before rebuilding."
    return
}

if ([string]::IsNullOrWhiteSpace($CorePackage)) {
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ShoujoKagekiJunna.json') -Raw -Encoding utf8 | ConvertFrom-Json
    $coreVersion = ($manifest.dependencies | Where-Object { $_.id -eq 'ShoujoKagekiAijoCore' }).min_version
    $CorePackage = Join-Path $PSScriptRoot "../STS2_ShoujoKagekiAijoCore/dist/ShoujoKagekiAijoCore-$coreVersion"
}
if (!$SkipBuild) {
    $buildArguments = @{ CorePackage = $CorePackage }
    if ($Godot) { $buildArguments.Godot = $Godot }
    & (Join-Path $PSScriptRoot 'build.ps1') @buildArguments
}

& (Join-Path $PSScriptRoot 'tools/dev/start-test-game.ps1') -Instance play-game -Port 0 -Playable -CorePackage $CorePackage
