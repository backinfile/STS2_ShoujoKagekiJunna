param(
    [string]$GameDir = 'D:/App/Stream/steamapps/common/Slay the Spire 2',
    [string]$Godot = 'D:/Godot/megadot-4.5.1-m.12-windows-x86_64-llvm-editor-csharp/MegaDot_v4.5.1-stable_mono_win64_console.exe'
)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build ShoujoKagekiJunna.csproj -c Release --nologo "-p:Sts2GameDir=$GameDir"
    if ($LASTEXITCODE) { throw 'C# build failed' }
    New-Item -ItemType Directory -Force dist/ShoujoKagekiJunna | Out-Null
    & $Godot --headless --editor --path $PSScriptRoot --import --quit *> dist/import.log
    if ($LASTEXITCODE) { throw 'Resource import failed; see dist/import.log' }
    & $Godot --headless --path $PSScriptRoot --script tools/pack.gd -- 'res://dist/ShoujoKagekiJunna/ShoujoKagekiJunna.pck'
    if ($LASTEXITCODE) { throw 'Resource packing failed' }
    Copy-Item -LiteralPath .godot/mono/temp/bin/Release/ShoujoKagekiJunna.dll -Destination dist/ShoujoKagekiJunna
    Copy-Item -LiteralPath ShoujoKagekiJunna.json -Destination dist/ShoujoKagekiJunna
} finally { Pop-Location }
