$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repository = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$cropDirectory = Join-Path $PSScriptRoot 'research/crops'
$manifest = Get-Content -LiteralPath (Join-Path $cropDirectory 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$ffmpeg = (Get-Command ffmpeg.exe -CommandType Application).Source
$checks = @()
foreach ($item in $manifest.crops) {
    $rect = $item.rectangle
    $source = Join-Path $repository $item.source
    $target = Join-Path $cropDirectory $item.file
    $filter = 'crop={0}:{1}:{2}:{3}:exact=1' -f $rect[2], $rect[3], $rect[0], $rect[1]
    & $ffmpeg -hide_banner -loglevel error -y -i $source -vf $filter -frames:v 1 $target
    if ($LASTEXITCODE) { throw "Source crop failed: $($item.file)" }
    $image = [System.Drawing.Image]::FromFile($target)
    try {
        if ($image.Width -ne $rect[2] -or $image.Height -ne $rect[3]) { throw "Crop dimensions differ: $($item.file)" }
        $checks += [ordered]@{ file=$item.file; dimensions=@($image.Width,$image.Height); sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() }
    } finally { $image.Dispose() }
}
[ordered]@{cropCount=$checks.Count; expectedDimensionsVerified=$true; files=$checks} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $cropDirectory 'validation.json') -Encoding UTF8
Write-Output ('Validated {0} source crop dimensions' -f $checks.Count)
