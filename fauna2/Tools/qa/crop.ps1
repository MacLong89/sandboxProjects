param(
    [string]$InFile,
    [string]$OutFile,
    [int]$CropX, [int]$CropY, [int]$CropW, [int]$CropH,
    [int]$Scale = 2
)
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile($InFile)
$outW = $CropW * $Scale
$outH = $CropH * $Scale
$rect = New-Object System.Drawing.Rectangle -ArgumentList $CropX, $CropY, $CropW, $CropH
$bmp = New-Object System.Drawing.Bitmap -ArgumentList $outW, $outH
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$dst = New-Object System.Drawing.Rectangle -ArgumentList 0, 0, $outW, $outH
$g.DrawImage($src, $dst, $rect, [System.Drawing.GraphicsUnit]::Pixel)
$bmp.Save($OutFile)
$g.Dispose(); $src.Dispose(); $bmp.Dispose()
Write-Output "Saved $OutFile"
