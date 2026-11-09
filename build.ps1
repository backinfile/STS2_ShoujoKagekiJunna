param(
    [string]$CorePackage = $env:SHOUJO_CORE_PACKAGE,
    [string]$GameDir = 'D:/App/Stream/steamapps/common/Slay the Spire 2',
    [string]$Godot = 'D:/Godot/megadot-4.5.1-m.12-windows-x86_64-llvm-editor-csharp/MegaDot_v4.5.1-stable_mono_win64_console.exe'
)
$ErrorActionPreference = 'Stop'
& py -3 (Join-Path $PSScriptRoot 'tools/assets/generate-card-rules.py') --check
if ($LASTEXITCODE) { throw 'Card runtime metadata validation failed' }
. (Join-Path $PSScriptRoot 'tools/core-package.ps1')
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ShoujoKagekiJunna.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$minimumCoreVersion = ($manifest.dependencies | Where-Object { $_.id -eq 'ShoujoKagekiAijoCore' }).min_version
$core = Get-CorePackage $CorePackage $minimumCoreVersion
$previousCoreApi = $env:CoreApiPath
$env:CoreApiPath = $core.ApiPath
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force dist/ShoujoKagekiJunna/lib | Out-Null
    foreach ($variant in @('Beta', 'Stable')) {
        $arguments = @('build', 'ShoujoKagekiJunna.csproj', '-c', 'Release', '--nologo',
            "-p:CoreApiPath=$($core.ApiPath)", "-p:Sts2GameDir=$GameDir", "-p:Sts2Api=$($variant.ToLowerInvariant())",
            "-p:AssemblyName=ShoujoKagekiJunna.$variant")
        if ($variant -eq 'Stable') { $arguments += '-p:Sts2ReferenceMode=package' }
        & dotnet @arguments
        if ($LASTEXITCODE) { throw "$variant C# build failed" }
        Copy-Item -LiteralPath ".godot/mono/temp/bin/Release/ShoujoKagekiJunna.$variant.dll" -Destination dist/ShoujoKagekiJunna/lib
    }
    dotnet build loader/JunnaModLoader.csproj -c Release --nologo "-p:Sts2GameDir=$GameDir"
    if ($LASTEXITCODE) { throw 'Mod loader build failed' }
    $import = Start-Process -FilePath $Godot -ArgumentList '--headless', '--editor', '--path', ('"' + $PSScriptRoot + '"'), '--import', '--quit' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput dist/import.log -RedirectStandardError dist/import-errors.log
    if ($import.ExitCode) { throw 'Resource import failed; see dist/import.log and dist/import-errors.log' }
    & $Godot --headless --path $PSScriptRoot --script tools/pack.gd -- 'res://dist/ShoujoKagekiJunna/ShoujoKagekiJunna.pck'
    if ($LASTEXITCODE) { throw 'Resource packing failed' }
    Copy-Item -LiteralPath loader/bin/Release/net9.0/ShoujoKagekiJunna.dll -Destination dist/ShoujoKagekiJunna
    Copy-Item -LiteralPath ShoujoKagekiJunna.json -Destination dist/ShoujoKagekiJunna
    Write-CoreRoleMetadata $core (Join-Path $PSScriptRoot 'dist/ShoujoKagekiJunna') (Join-Path $PSScriptRoot 'dist') 'ShoujoKagekiJunna'
} finally {
    $env:CoreApiPath = $previousCoreApi
    Pop-Location
}
