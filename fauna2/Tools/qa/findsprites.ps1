param([string]$InFile)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap($InFile)
# Viewport bounds (full-res screen): x 390-1530, y 160-735
$minX = 400; $maxX = 1525; $minY = 165; $maxY = 730
$playerHits = New-Object System.Collections.ArrayList
$pengHits = New-Object System.Collections.ArrayList
function In-Hud([int]$x, [int]$y) {
    if ($x -ge 390 -and $x -le 640 -and $y -ge 220 -and $y -le 320) { return $true }   # warning banner
    if ($x -ge 1320 -and $x -le 1540 -and $y -ge 150 -and $y -le 340) { return $true } # goal card
    if ($x -ge 770 -and $x -le 1150 -and $y -ge 650 -and $y -le 745) { return $true }  # hotbar
    return $false
}
for ($y = $minY; $y -lt $maxY; $y += 2) {
    for ($x = $minX; $x -lt $maxX; $x += 2) {
        if (In-Hud $x $y) { continue }
        $c = $bmp.GetPixel($x, $y)
        # Player: saturated gold/amber
        if ($c.R -gt 150 -and $c.R -lt 235 -and $c.G -gt 110 -and $c.G -lt 190 -and $c.B -lt 90 -and ($c.R - $c.B) -gt 90) {
            [void]$playerHits.Add(@($x, $y))
        }
        # Penguin: near-black pixel (dark head) — rare on snow/rock terrain
        elseif ($c.R -lt 55 -and $c.G -lt 55 -and $c.B -lt 60) {
            # confirm a near-white pixel just below (belly)
            $c2 = $bmp.GetPixel($x, [Math]::Min($y + 4, 1079))
            if ($c2.R -gt 190 -and $c2.G -gt 190 -and $c2.B -gt 190) {
                [void]$pengHits.Add(@($x, $y))
            }
        }
    }
}
function Cluster($hits) {
    $clusters = New-Object System.Collections.ArrayList
    foreach ($h in $hits) {
        $found = $false
        foreach ($cl in $clusters) {
            if ([Math]::Abs($cl.X - $h[0]) -lt 25 -and [Math]::Abs($cl.Y - $h[1]) -lt 25) {
                $cl.X = ($cl.X * $cl.N + $h[0]) / ($cl.N + 1)
                $cl.Y = ($cl.Y * $cl.N + $h[1]) / ($cl.N + 1)
                $cl.N += 1
                $found = $true
                break
            }
        }
        if (-not $found) {
            [void]$clusters.Add([PSCustomObject]@{ X = [double]$h[0]; Y = [double]$h[1]; N = 1 })
        }
    }
    return $clusters
}
Write-Output "PLAYER clusters:"
Cluster $playerHits | Sort-Object N -Descending | Select-Object -First 5 | ForEach-Object { Write-Output ("  ({0:F0},{1:F0}) n={2}" -f $_.X, $_.Y, $_.N) }
Write-Output "PENGUIN candidates:"
Cluster $pengHits | Sort-Object N -Descending | Select-Object -First 10 | ForEach-Object { Write-Output ("  ({0:F0},{1:F0}) n={2}" -f $_.X, $_.Y, $_.N) }
$bmp.Dispose()
