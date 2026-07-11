Add-Type -AssemblyName System.Drawing
$assets = Join-Path $PSScriptRoot "..\Assets" | Resolve-Path

function Ensure-Dir($p) {
    $d = Split-Path $p -Parent
    if (!(Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

function Lerp([double]$a, [double]$b, [double]$t) { $a + ($b - $a) * $t }

function Hash01([int]$x, [int]$y, [int]$seed) {
    [Math]::Abs([HashCode]::Combine($seed, $x, $y)) % 10000 / 10000.0
}

function SmoothNoise([double]$x, [double]$y, [int]$seed) {
    $x0 = [Math]::Floor($x); $y0 = [Math]::Floor($y)
    $tx = $x - $x0; $ty = $y - $y0
    $a = Hash01 $x0 $y0 $seed
    $b = Hash01 ($x0 + 1) $y0 $seed
    $c = Hash01 $x0 ($y0 + 1) $seed
    $d = Hash01 ($x0 + 1) ($y0 + 1) $seed
    $u = $tx * $tx * (3 - 2 * $tx)
    $v = $ty * $ty * (3 - 2 * $ty)
    Lerp (Lerp $a $b $u) (Lerp $c $d $u) $v
}

function Fractal([double]$x, [double]$y, [int]$seed) {
    $v = 0; $amp = 0.55; $freq = 2.5
    for ($o = 0; $o -lt 5; $o++) {
        $v += $amp * (SmoothNoise ($x * $freq) ($y * $freq) ($seed + $o * 97))
        $amp *= 0.5; $freq *= 2
    }
    [Math]::Clamp($v, 0, 1)
}

$menu = Join-Path $assets "ui\menu\menu_background.png"
if (!(Test-Path $menu)) {
    Ensure-Dir $menu
    $w = 1920; $h = 1080
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    for ($y = 0; $y -lt $h; $y++) {
        $t = $y / ($h - 1)
        for ($x = 0; $x -lt $w; $x++) {
            $nx = $x / ($w - 1) - 0.5; $ny = $y / ($h - 1) - 0.5
            $vig = 1 - 0.35 * ($nx * $nx * 1.4 + $ny * $ny)
            if ($vig -lt 0.55) { $vig = 0.55 }
            if ($t -lt 0.55) {
                $u = $t / 0.55
                $R = [int]((Lerp 0.04 0.18 $u) * $vig * 255)
                $G = [int]((Lerp 0.07 0.14 $u) * $vig * 255)
                $B = [int]((Lerp 0.13 0.10 $u) * $vig * 255)
            }
            else {
                $u = ($t - 0.55) / 0.45
                $R = [int]((Lerp 0.18 0.10 $u) * $vig * 255)
                $G = [int]((Lerp 0.14 0.13 $u) * $vig * 255)
                $B = [int]((Lerp 0.10 0.08 $u) * $vig * 255)
            }
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($R, $G, $B))
        }
    }
    $bmp.Save($menu, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "created menu_background"
}

$tab = Join-Path $assets "ui\menu\chrome\menu_backdrop.png"
if (!(Test-Path $tab)) {
    Ensure-Dir $tab
    $bmp = New-Object System.Drawing.Bitmap 1024, 1024
    for ($y = 0; $y -lt 1024; $y++) {
        for ($x = 0; $x -lt 1024; $x++) {
            $n = Hash01 $x $y 17
            $s = 0.92 + $n * 0.08
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int](217 * $s), [int](199 * $s), [int](168 * $s)))
        }
    }
    $bmp.Save($tab, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "created menu_backdrop"
}

$map = Join-Path $assets "map\co_height.png"
if (!(Test-Path $map)) {
    Ensure-Dir $map
    $w = 2048; $h = 2048
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $nx = $x / ($w - 1); $ny = $y / ($h - 1)
            $hv = [int]((Fractal $nx $ny 42069) * 255)
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($hv, $hv, $hv))
        }
    }
    $bmp.Save($map, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "created co_height"
}

$icons = @(
    "deer", "wolf", "panther", "moose", "health", "stamina", "thirst", "hunger",
    "inventory", "journal", "tames", "map", "guild", "settings", "home", "special",
    "survival", "combat", "building", "notification", "you", "lineage", "exploration",
    "city", "town", "events", "quest", "dominion", "discoveries", "achievements",
    "craft_armor", "craft_tools", "craft_forge", "craft_build", "craft_medical", "craft_ammo",
    "damage", "stay", "scavenger", "apple", "m4", "filter_all", "filter_building",
    "filter_weapons", "filter_tools", "filter_apparel", "filter_consumables",
    "craft_building", "craft_weapons", "craft_food"
)
$iconDir = Join-Path $assets "ui\iconsv8"
if (!(Test-Path $iconDir)) { New-Item -ItemType Directory -Path $iconDir -Force | Out-Null }
foreach ($stem in $icons) {
    $p = Join-Path $iconDir "$stem.png"
    if (Test-Path $p) { continue }
    $hash = [Math]::Abs($stem.GetHashCode())
    $r = [byte](((($hash -band 0xFF) / 255.0) * 140 + 40))
    $g = [byte]((((($hash -shr 8) -band 0xFF) / 255.0) * 140 + 40))
    $b = [byte]((((($hash -shr 16) -band 0xFF) / 255.0) * 140 + 40))
    $bmp = New-Object System.Drawing.Bitmap 64, 64
    for ($y = 0; $y -lt 64; $y++) {
        for ($x = 0; $x -lt 64; $x++) {
            if ($x -lt 4 -or $y -lt 4 -or $x -gt 59 -or $y -gt 59) {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int]($r / 2), [int]($g / 2), [int]($b / 2)))
            }
            else {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($r, $g, $b))
            }
        }
    }
    $bmp.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$count = (Get-ChildItem -Path $assets -Recurse -Include *.png | Measure-Object).Count
Write-Host "PNG count under Assets: $count"
