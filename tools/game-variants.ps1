$JunnaGameVariants = @(Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $PSScriptRoot '../data/game-variants.json') | ConvertFrom-Json)

function Get-JunnaRuntimeFiles {
    'ShoujoKagekiJunna.dll'
    'ShoujoKagekiJunna.pck'
    'ShoujoKagekiJunna.json'
    'core.requirements'
    'core.package.lock'
    foreach ($variant in $JunnaGameVariants) { "lib/ShoujoKagekiJunna.$($variant.name).dll" }
}

function Test-JunnaReference($Variant, [string]$GameDirectory) {
    if (!$GameDirectory) { throw "Set -MainGameDir or SHOUJO_MAIN_GAME to v$($Variant.reference)." }
    $assembly = Join-Path $GameDirectory 'data_sts2_windows_x86_64/sts2.dll'
    if (!(Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Missing $($Variant.name) reference: $assembly" }
    $product = [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly).ProductVersion
    if ($product -notmatch ('\+' + [regex]::Escape($Variant.commit))) {
        throw "$($Variant.name) requires the v$($Variant.reference) reference assembly ($($Variant.commit)); found $product."
    }
}
