param([Parameter(Mandatory=$true)][string]$Base,[double]$Degrees=25,[ValidatePattern('^[a-z0-9_]+$')][string]$Key='arrow_rain_potion')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$baseRelative=$Base.Replace('\','/')
$sourceFile=[IO.Path]::GetFullPath((Join-Path $Base ("cleaned-native/"+$Key+"_upright.png")))
$outputFile=[IO.Path]::GetFullPath((Join-Path $Base ("cleaned-native/"+$Key+".png")))
$image=[System.Drawing.Image]::FromFile($sourceFile)
$rotated=$null
$graphics=$null
try {
  $radians=$Degrees*[Math]::PI/180
  $width=[int][Math]::Ceiling([Math]::Abs($image.Width*[Math]::Cos($radians))+[Math]::Abs($image.Height*[Math]::Sin($radians)))+8
  $height=[int][Math]::Ceiling([Math]::Abs($image.Height*[Math]::Cos($radians))+[Math]::Abs($image.Width*[Math]::Sin($radians)))+8
  $rotated=[System.Drawing.Bitmap]::new($width,$height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics=[System.Drawing.Graphics]::FromImage($rotated)
  $graphics.PageUnit=[System.Drawing.GraphicsUnit]::Pixel
  $graphics.Clear([System.Drawing.Color]::Transparent)
  $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
  $graphics.CompositingQuality=[System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $graphics.TranslateTransform([single]($width/2),[single]($height/2))
  $graphics.RotateTransform([single]$Degrees)
  $graphics.TranslateTransform([single](-$image.Width/2),[single](-$image.Height/2))
  $matrix=$graphics.Transform
  $elements=@($matrix.Elements)
  $matrix.Dispose()
  $graphics.DrawImage($image,0,0,$image.Width,$image.Height)
  $rotated.Save($outputFile,[System.Drawing.Imaging.ImageFormat]::Png)
  $record=[ordered]@{
    tool='System.Drawing GDI+'
    operation='rigid whole-image rotation'
    degreesClockwise=$Degrees
    source=$baseRelative+'/cleaned-native/'+$Key+'_upright.png'
    source_sha256=(Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash.ToLowerInvariant()
    output=$baseRelative+'/cleaned-native/'+$Key+'.png'
    output_sha256=(Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceSize=@($image.Width,$image.Height)
    outputSize=@($width,$height)
    affineMatrix=$elements
    transparentBackground=$true
    regenerated=$false
    cleanupAppliedBeforeRotation=$true
  }
  $record|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $Base 'rotation.json') -Encoding utf8NoBOM
  [pscustomobject]@{rotated=$true;degreesClockwise=$Degrees;sourceSize=$record.sourceSize;outputSize=$record.outputSize}|ConvertTo-Json -Compress
} finally {
  if($graphics){$graphics.Dispose()}
  if($rotated){$rotated.Dispose()}
  $image.Dispose()
}
