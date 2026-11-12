$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskDirectory = $PSScriptRoot
$cropDirectory = Join-Path $taskDirectory 'research/crops'
$font = [System.Drawing.Font]::new('Microsoft YaHei', 19, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(30, 40, 42))

function New-ReferenceBoard([string]$Name, [object[]]$Items, [int]$Columns, [int]$CellWidth, [int]$CellHeight) {
    $rows = [int][Math]::Ceiling($Items.Count / $Columns)
    $board = [System.Drawing.Bitmap]::new($CellWidth * $Columns, $CellHeight * $rows)
    $graphics = [System.Drawing.Graphics]::FromImage($board)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(238, 236, 229))
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        for ($index = 0; $index -lt $Items.Count; $index++) {
            $item = $Items[$index]
            $left = ($index % $Columns) * $CellWidth
            $top = [int][Math]::Floor($index / $Columns) * $CellHeight
            $graphics.DrawString($item[1], $font, $textBrush, $left + 18, $top + 12)
            $image = [System.Drawing.Image]::FromFile((Join-Path $cropDirectory ($item[0] + '.png')))
            try {
                $scale = [Math]::Min(($CellWidth - 36) / $image.Width, ($CellHeight - 66) / $image.Height)
                $width = [int][Math]::Round($image.Width * $scale)
                $height = [int][Math]::Round($image.Height * $scale)
                $destination = [System.Drawing.Rectangle]::new($left + [int](($CellWidth - $width) / 2), $top + 52 + [int](($CellHeight - 66 - $height) / 2), $width, $height)
                $graphics.DrawImage($image, $destination, 0, 0, $image.Width, $image.Height, [System.Drawing.GraphicsUnit]::Pixel)
            } finally { $image.Dispose() }
        }
        $board.Save((Join-Path $cropDirectory $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $board.Dispose() }
}

try {
    New-ReferenceBoard 'setting-comparison.png' @(
        @('bow-setting', '弓端宝石 · TV设定集预览32'),
        @('junna-short-blade-gem', '纯那短刃 · 剧场版设定集119页'),
        @('nana-original-topaz', '奈奈原刀柄 · TV设定集预览33'),
        @('junna-short-blade-bottom', '纯那短刃柄底 · 剧场版设定集119页')
    ) 2 560 560
    New-ReferenceBoard 'bow-color-references.png' @(
        @('bow-movie-color', '弓端彩图 · 剧场版设定集预览115'),
        @('bow-movie-setting', '弓端细节 · 剧场版设定集预览115'),
        @('bow-tv-closeup', 'TV第2话17:07 · 原画面顶部截断'),
        @('bow-tv-whole', 'TV第2话17:12')
    ) 2 560 460
    New-ReferenceBoard 'short-blade-context.png' @(
        @('junna-short-blade-handle', '纯那短刃刀柄 · 设定集119页'),
        @('movie-broken-gem', '破碎宝石 · 狩猎片段05:17'),
        @('movie-short-blade-light', '柄端光效 · 狩猎片段05:29')
    ) 3 440 390
} finally { $font.Dispose(); $textBrush.Dispose() }
Write-Output 'Created three source-reference boards from unmodified crops'
