# Disk-side publish checklist - run before uploading a package.
# Verifies every material/texture/sound/UI path the game expects under Assets/.
# Usage (from project root):
#   powershell -File tools/verify_ship_assets.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "Assets"
$script:fail = 0

function Need( [string]$rel ) {
	$path = Join-Path $assets $rel
	if ( -not (Test-Path -LiteralPath $path) ) {
		Write-Host "MISSING  $rel" -ForegroundColor Red
		$script:fail++
	}
	else {
		Write-Host "ok       $rel"
	}
}

Write-Host "=== Materials ==="
@(
	"materials/fo_grass.vmat",
	"materials/fo_stone.vmat",
	"materials/fo_wood.vmat",
	"materials/fo_roof.vmat",
	"materials/fo_brick.vmat",
	"materials/fo_metal.vmat",
	"materials/fo_plaster.vmat",
	"materials/fo_thatch.vmat",
	"materials/fo_crops.vmat",
	"materials/fo_awning.vmat",
	"materials/fo_slate.vmat"
) | ForEach-Object { Need $_ }

Write-Host "=== Textures (referenced by .vmat) ==="
@(
	"textures/grass_color.png",
	"textures/stone_color.png",
	"textures/wood_color.png",
	"textures/roof_color.png",
	"textures/brick_color.png",
	"textures/metal_color.png",
	"textures/plaster_color.png",
	"textures/thatch_color.png",
	"textures/crops_color.png",
	"textures/awning_color.png",
	"textures/slate_color.png"
) | ForEach-Object { Need $_ }

Write-Host "=== Sounds (.sound + mp3 they reference) ==="
Get-ChildItem (Join-Path $assets "sounds\*.sound") | ForEach-Object {
	$soundFile = $_
	Need ("sounds/" + $soundFile.Name)
	try {
		$j = Get-Content $soundFile.FullName -Raw | ConvertFrom-Json
		foreach ( $s in @($j.Sounds) ) {
			if ( [string]::IsNullOrWhiteSpace($s) ) { continue }
			Need $s
		}
	}
	catch {
		Write-Host ("BAD JSON " + $soundFile.Name + ": " + $_.Exception.Message) -ForegroundColor Red
		$script:fail++
	}
}

Write-Host "=== UI ==="
@(
	"ui/brand_emblem.png",
	"ui/build_gun_tower.png",
	"ui/build_cannon.png",
	"ui/build_long_range.png",
	"ui/build_wall.png",
	"ui/build_barracks.png",
	"ui/build_lab.png"
) | ForEach-Object { Need $_ }

Write-Host "=== Project package refs ==="
$sbproj = Get-Content (Join-Path $root "the_final_outpost.sbproj") -Raw | ConvertFrom-Json
if ( $sbproj.PackageReferences -notcontains "facepunch.sboxweapons" ) {
	Write-Host "MISSING  PackageReferences: facepunch.sboxweapons" -ForegroundColor Red
	$script:fail++
}
else {
	Write-Host "ok       PackageReferences includes facepunch.sboxweapons"
}

Write-Host ""
if ( $script:fail -gt 0 ) {
	Write-Host ("FAILED - " + $script:fail + " missing asset(s). Do not publish until fixed.") -ForegroundColor Red
	exit 1
}

Write-Host "PASS - all packaged content paths are on disk." -ForegroundColor Green
exit 0
