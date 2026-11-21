param(
    [string]$CorePackage = $env:SHOUJO_CORE_PACKAGE,
    [string]$GameDir = $env:SHOUJO_BETA_GAME,
    [string]$Godot = 'D:/Godot/megadot-4.5.1-m.12-windows-x86_64-llvm-editor-csharp/MegaDot_v4.5.1-stable_mono_win64_console.exe',
    [string]$MainGameDir = $env:SHOUJO_MAIN_GAME
)
$ErrorActionPreference = 'Stop'
$steamGameDir = 'D:/App/Stream/steamapps/common/Slay the Spire 2'
if (!$GameDir) { $GameDir = $steamGameDir }
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $powerShell7 = Get-Command pwsh.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
    if (!$powerShell7) {
        $powerShell7 = @(
            "$env:ProgramFiles/PowerShell/7/pwsh.exe",
            (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (!$powerShell7) { throw 'PowerShell 7 is required. Install it or add pwsh.exe to PATH.' }
    $powerShell7Arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-GameDir', $GameDir, '-Godot', $Godot, '-MainGameDir', $MainGameDir)
    if ($CorePackage) { $powerShell7Arguments += @('-CorePackage', $CorePackage) }
    & $powerShell7 @powerShell7Arguments
    if ($LASTEXITCODE) { throw "Build failed under PowerShell 7 (exit code $LASTEXITCODE)." }
    return
}
& py -3 (Join-Path $PSScriptRoot 'tools/assets/generate-card-rules.py') --check
if ($LASTEXITCODE) { throw 'Card runtime metadata validation failed' }
. (Join-Path $PSScriptRoot 'tools/core-package.ps1')
. (Join-Path $PSScriptRoot 'tools/game-variants.ps1')
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ShoujoKagekiJunna.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$minimumCoreVersion = ($manifest.dependencies | Where-Object { $_.id -eq 'ShoujoKagekiCore' }).min_version
$core = Get-CorePackage $CorePackage $minimumCoreVersion
$games = @{ Beta = $GameDir; Stable = $GameDir; Main = $MainGameDir }
foreach ($variant in $JunnaGameVariants) {
    if ($variant.name -ne 'Stable') { Test-JunnaReference $variant $games[$variant.name] }
}
$previousCoreApi = $env:CoreApiPath
$env:CoreApiPath = $core.ApiPath
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force dist/ShoujoKagekiJunna/lib | Out-Null
    foreach ($entry in $JunnaGameVariants) {
        $variant = $entry.name
        $arguments = @('build', 'ShoujoKagekiJunna.csproj', '-c', 'Release', '--nologo',
            "-p:CoreApiPath=$($core.ApiPath)", "-p:Sts2GameDir=$($games[$variant])", "-p:Sts2Api=$($variant.ToLowerInvariant())",
            "-p:AssemblyName=ShoujoKagekiJunna.$variant")
        if ($variant -eq 'Stable') { $arguments += '-p:Sts2ReferenceMode=package' }
        & dotnet @arguments
        if ($LASTEXITCODE) { throw "$variant C# build failed" }
        Copy-Item -LiteralPath ".godot/mono/temp/bin/Release/ShoujoKagekiJunna.$variant.dll" -Destination dist/ShoujoKagekiJunna/lib
    }
    dotnet build loader/JunnaModLoader.csproj -c Release --nologo "-p:Sts2GameDir=$GameDir" "-p:CoreLoaderSource=$($core.Root)/sdk/CharacterLoader.cs"
    if ($LASTEXITCODE) { throw 'Mod loader build failed' }
    $import = Start-Process -FilePath $Godot -ArgumentList '--headless', '--editor', '--path', ('"' + $PSScriptRoot + '"'), '--import', '--quit' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput dist/import.log -RedirectStandardError dist/import-errors.log
    if ($import.ExitCode) { throw 'Resource import failed; see dist/import.log and dist/import-errors.log' }
    & $Godot --headless --path $PSScriptRoot --script tools/pack.gd -- 'res://dist/ShoujoKagekiJunna/ShoujoKagekiJunna.pck'
    if ($LASTEXITCODE) { throw 'Resource packing failed' }
    Copy-Item -LiteralPath loader/bin/Release/net9.0/ShoujoKagekiJunna.dll -Destination dist/ShoujoKagekiJunna
    Copy-Item -LiteralPath ShoujoKagekiJunna.json -Destination dist/ShoujoKagekiJunna
    Write-CoreRoleMetadata $core (Join-Path $PSScriptRoot 'dist/ShoujoKagekiJunna') (Join-Path $PSScriptRoot 'dist') 'ShoujoKagekiJunna'
    foreach ($file in Get-JunnaRuntimeFiles) {
        if (!(Test-Path -LiteralPath (Join-Path 'dist/ShoujoKagekiJunna' $file) -PathType Leaf)) { throw "Incomplete Junna package: $file" }
    }
} finally {
    $env:CoreApiPath = $previousCoreApi
    Pop-Location
}
