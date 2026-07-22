param([string]$InFile)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap($InFile)
$minX = 400; $maxX = 1520; $minY = 170; $maxY = 725
function In-Hud([int]$x, [int]$y) {
    if ($x -ge 390 -and $x -le 650 -and $y -ge 215 -and $y -le 325) { return $true }
    if ($x -ge 1310 -and $x -le 1540 -and $y -ge 145 -and $y -le 345) { return $true }
    if ($x -ge 760 -and $x -le 1160 -and $y -ge 645 -and $y -le 745) { return $true }
    return $false
}
$hits = New-Object System.Collections.ArrayList
for ($y = $minY; $y -lt $maxY; $y++) {
    for ($x = $minX; $x -lt $maxX; $x++) {
        if (In-Hud $x $y) { continue }
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -lt 70 -and $c.G -lt 70 -and $c.B -lt 75) {
            $white = $false
            for ($dy = 2; $dy -le 8; $dy += 2) {
                $c2 = $bmp.GetPixel($x, [Math]::Min($y + $dy, 1079))
                if ($c2.R -gt 195 -and $c2.G -gt 195 -and $c2.B -gt 195) { $white = $true; break }
            }
            if ($white) { [void]$hits.Add(@($x, $y)) }
        }
    }
}
$clusters = New-Object System.Collections.ArrayList
foreach ($h in $hits) {
    $found = $false
    foreach ($cl in $clusters) {
        if ([Math]::Abs($cl.X - $h[0]) -lt 20 -and [Math]::Abs($cl.Y - $h[1]) -lt 20) {
            $cl.X = ($cl.X * $cl.N + $h[0]) / ($cl.N + 1)
            $cl.Y = ($cl.Y * $cl.N + $h[1]) / ($cl.N + 1)
            $cl.N += 1
            $found = $true
            break
        }
    }
    if (-not $found) { [void]$clusters.Add([PSCustomObject]@{ X = [double]$h[0]; Y = [double]$h[1]; N = 1 }) }
}
Write-Output "PENGUIN candidates (screen px, player approx 975,430):"
$clusters | Sort-Object N -Descending | Select-Object -First 8 | ForEach-Object {
    $dx = $_.X - 975; $dy = $_.Y - 430
    Write-Output ("  ({0:F0},{1:F0}) n={2} dx={3:F0} dy={4:F0}" -f $_.X, $_.Y, $_.N, $dx, $dy)
}
$bmp.Dispose()
