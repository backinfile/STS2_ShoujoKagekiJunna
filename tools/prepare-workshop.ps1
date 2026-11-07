#requires -Version 7.0
param(
    [Parameter(Mandatory)][ValidateRange(1, [ulong]::MaxValue)][ulong]$CoreWorkshopId,
    [Parameter(Mandatory)][string]$PreviewImage,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ModDirectory = (Join-Path $PSScriptRoot '../dist/ShoujoKagekiJunna'),
    [ValidatePattern('^[1-9][0-9]*$')][string]$WorkshopId,
    [string]$ChangeNote = ''
)
$ErrorActionPreference = 'Stop'
if ($CoreWorkshopId -eq 3737335127) { throw 'CoreWorkshopId must identify Core, not BaseLib.' }
$source = (Resolve-Path -LiteralPath $ModDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $source 'ShoujoKagekiJunna.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.id -ne 'ShoujoKagekiJunna' -or
    @($manifest.dependencies | Where-Object { $_.id -eq 'ShoujoKagekiAijoCore' -and $_.min_version }).Count -ne 1 -or
    @($manifest.dependencies | Where-Object { $_.id -eq 'BaseLib' }).Count -ne 1) {
    throw 'Junna manifest must declare BaseLib and the Core minimum version.'
}
foreach ($file in @('ShoujoKagekiJunna.dll', 'ShoujoKagekiJunna.pck', 'lib/ShoujoKagekiJunna.Stable.dll',
    'lib/ShoujoKagekiJunna.Beta.dll', 'core.requirements', 'core.package.lock')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $file) -PathType Leaf)) { throw "Build Junna first; missing $file" }
}
if (Get-ChildItem -LiteralPath $source -Recurse -Force | Where-Object { $_.Name -like '*ShoujoKagekiAijoCore*' }) {
    throw 'Junna Workshop content must use the independent Core dependency.'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Workshop output already exists. Choose a fresh OutputDirectory.' }
if ($output.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Workshop output must be outside the source mod directory.'
}
$preview = Get-Item -LiteralPath $PreviewImage
if ($preview.PSIsContainer -or $preview.Length -ge 1MB) { throw 'Workshop preview must be a PNG smaller than 1 MB.' }
$signature = [IO.File]::ReadAllBytes($preview.FullName)
if ($signature.Length -lt 8 -or [Convert]::ToHexString($signature[0..7]) -ne '89504E470D0A1A0A') { throw 'Workshop preview must be a PNG.' }
if ($WorkshopId) {
    if ([ulong]::Parse($WorkshopId) -in @($CoreWorkshopId, [ulong]3737335127)) { throw 'Junna WorkshopId must differ from its dependencies.' }
}
$config = [ordered]@{
    title='星见纯那'; description=$manifest.description; visibility='public'; changeNote=$ChangeNote
    tags=@('Characters', 'Cards', 'Relics'); dependencies=@([ulong]3737335127, $CoreWorkshopId); contentDescriptors=@()
}
$content = Join-Path $output 'content'
New-Item -ItemType Directory -Path $content -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $content 'ShoujoKagekiJunna') -Recurse
Copy-Item -LiteralPath $preview.FullName -Destination (Join-Path $output 'image.png')
$config | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'workshop.json') -Encoding UTF8
if ($WorkshopId) { Set-Content -LiteralPath (Join-Path $output 'mod_id.txt') -Value $WorkshopId -Encoding UTF8 }
Write-Host "Prepared Junna Workshop workspace: $output"
