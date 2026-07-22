param([string]$InFile, [int]$CX, [int]$CY, [int]$R = 20)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap($InFile)
for ($y = $CY - $R; $y -le $CY + $R; $y += 2) {
    $row = ""
    for ($x = $CX - $R; $x -le $CX + $R; $x += 2) {
        $c = $bmp.GetPixel($x, $y)
        $row += ("{0:X2}{1:X2}{2:X2} " -f $c.R, $c.G, $c.B)
    }
    Write-Output ("y={0}: {1}" -f $y, $row)
}
$bmp.Dispose()
