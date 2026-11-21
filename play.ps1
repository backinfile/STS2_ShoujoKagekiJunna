param(
    [string]$CorePackage = $env:SHOUJO_CORE_PACKAGE,
    [string]$Godot,
    [string]$MainGameDir = $env:SHOUJO_MAIN_GAME,
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
    $repository = $PSScriptRoot
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $gitDirectory = git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir 2>$null
        if ($LASTEXITCODE -eq 0) { $repository = Split-Path -Parent $gitDirectory }
    }
    $coreRoot = if ($env:SHOUJO_CORE_ROOT) { $env:SHOUJO_CORE_ROOT }
        else { Join-Path (Split-Path -Parent $repository) 'STS2_ShoujoKagekiCore' }
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ShoujoKagekiJunna.json') -Raw -Encoding utf8 | ConvertFrom-Json
    $coreVersion = ($manifest.dependencies | Where-Object id -eq 'ShoujoKagekiCore').min_version
    $CorePackage = Join-Path $coreRoot "dist/ShoujoKagekiCore-$coreVersion"
    if (!(Test-Path -LiteralPath $CorePackage -PathType Container)) {
        $CorePackage = Join-Path $coreRoot 'dist/ShoujoKagekiCore'
    }
}
if (!$SkipBuild) {
    $buildArguments = @{ CorePackage = $CorePackage; MainGameDir = $MainGameDir }
    if ($Godot) { $buildArguments.Godot = $Godot }
    & (Join-Path $PSScriptRoot 'build.ps1') @buildArguments
}

& (Join-Path $PSScriptRoot 'tools/dev/start-test-game.ps1') -Instance play-game -Port 0 -Playable -CorePackage $CorePackage
