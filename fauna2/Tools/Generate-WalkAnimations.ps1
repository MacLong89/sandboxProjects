# Generates walk-cycle PNG copies from existing Fauna2 sprites.
# Original PNGs are never modified - output goes under Assets/models/animations/.

param(
	[Parameter( Mandatory = $false )]
	[string]$ProjectRoot = ""
)

if ( [string]::IsNullOrWhiteSpace( $ProjectRoot ) ) {
	if ( $PSScriptRoot ) {
		$ProjectRoot = Split-Path -Parent $PSScriptRoot
	}
	else {
		$ProjectRoot = "C:\_s&box\fauna2"
	}
}

Add-Type -AssemblyName System.Drawing

$modelsRoot = Join-Path $ProjectRoot "Assets\models"
$animationsRoot = Join-Path $modelsRoot "animations"
$frameCount = 4

function Get-ContentBounds( $bitmap ) {
	$minX = $bitmap.Width
	$minY = $bitmap.Height
	$maxX = 0
	$maxY = 0
	$found = $false

	for ( $y = 0; $y -lt $bitmap.Height; $y++ ) {
		for ( $x = 0; $x -lt $bitmap.Width; $x++ ) {
			if ( $bitmap.GetPixel( $x, $y ).A -lt 16 ) { continue }
			$found = $true
			if ( $x -lt $minX ) { $minX = $x }
			if ( $y -lt $minY ) { $minY = $y }
			if ( $x -gt $maxX ) { $maxX = $x }
			if ( $y -gt $maxY ) { $maxY = $y }
		}
	}

	if ( -not $found ) { return $null }
	return [PSCustomObject]@{
		MinX = $minX
		MinY = $minY
		MaxX = $maxX
		MaxY = $maxY
		Width = $maxX - $minX + 1
		Height = $maxY - $minY + 1
	}
}

function Test-CheckerboardPixel( $color ) {
	if ( $color.A -lt 16 ) { return $false }
	$spread = [Math]::Max( $color.R, [Math]::Max( $color.G, $color.B ) ) - [Math]::Min( $color.R, [Math]::Min( $color.G, $color.B ) )
	return $color.R -ge 230 -and $color.G -ge 230 -and $color.B -ge 230 -and $spread -le 4
}

function Remove-CheckerboardBackground( $path ) {
	if ( -not ( Test-Path -LiteralPath $path ) ) { return $false }

	$bytes = [System.IO.File]::ReadAllBytes( $path )
	$stream = New-Object System.IO.MemoryStream( , $bytes )
	$source = [System.Drawing.Image]::FromStream( $stream )
	$output = $null
	try {
		$changed = $false
		$output = New-Object System.Drawing.Bitmap $source.Width, $source.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
		for ( $y = 0; $y -lt $source.Height; $y++ ) {
			for ( $x = 0; $x -lt $source.Width; $x++ ) {
				$pixel = $source.GetPixel( $x, $y )
				if ( Test-CheckerboardPixel $pixel ) {
					$output.SetPixel( $x, $y, [System.Drawing.Color]::FromArgb( 0, 0, 0, 0 ) )
					$changed = $true
				}
				else {
					$output.SetPixel( $x, $y, $pixel )
				}
			}
		}

		if ( -not $changed ) { return $false }

		$tempPath = "$path.tmp.png"
		$output.Save( $tempPath, [System.Drawing.Imaging.ImageFormat]::Png )
		Move-Item -LiteralPath $tempPath -Destination $path -Force
		Write-Host "Stripped checkerboard background from '$path'"
		return $true
	}
	finally {
		$source.Dispose()
		$stream.Dispose()
		if ( $null -ne $output ) { $output.Dispose() }
	}
}

function Copy-Png( $sourcePath, $destPath ) {
	$destDir = Split-Path -Parent $destPath
	if ( -not ( Test-Path -LiteralPath $destDir ) ) {
		New-Item -ItemType Directory -Path $destDir -Force | Out-Null
	}
	Copy-Item -LiteralPath $sourcePath -Destination $destPath -Force
}

function New-WalkFrame( $sourcePath, $destPath, [int]$frameIndex ) {
	$source = [System.Drawing.Bitmap]::FromFile( $sourcePath )
	try {
		$bounds = Get-ContentBounds $source
		if ( $null -eq $bounds ) {
			Copy-Png $sourcePath $destPath
			return
		}

		$canvas = New-Object System.Drawing.Bitmap $source.Width, $source.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
		$graphics = [System.Drawing.Graphics]::FromImage( $canvas )
		try {
			$graphics.Clear( [System.Drawing.Color]::FromArgb( 0, 0, 0, 0 ) )
			$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
			$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

			$profiles = @(
				@{ ScaleY = 1.00; OffsetY = 0.00 },
				@{ ScaleY = 0.92; OffsetY = 0.035 },
				@{ ScaleY = 1.08; OffsetY = -0.045 },
				@{ ScaleY = 0.95; OffsetY = 0.020 }
			)
			$profile = $profiles[ $frameIndex % $profiles.Count ]

			$anchorX = ( $bounds.MinX + $bounds.MaxX ) / 2.0
			$anchorY = [double]$bounds.MaxY
			$scaleY = [double]$profile.ScaleY
			$offsetY = [double]$profile.OffsetY * $bounds.Height

			$destRect = [System.Drawing.RectangleF]::new(
				[double]$bounds.MinX,
				[double]( $anchorY - $bounds.Height * $scaleY + $offsetY ),
				[double]$bounds.Width,
				[double]( $bounds.Height * $scaleY )
			)
			$srcRect = [System.Drawing.Rectangle]::new(
				$bounds.MinX,
				$bounds.MinY,
				$bounds.Width,
				$bounds.Height
			)

			$graphics.DrawImage( $source, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel )
		}
		finally {
			$graphics.Dispose()
		}

		$destDir = Split-Path -Parent $destPath
		if ( -not ( Test-Path -LiteralPath $destDir ) ) {
			New-Item -ItemType Directory -Path $destDir -Force | Out-Null
		}
		$canvas.Save( $destPath, [System.Drawing.Imaging.ImageFormat]::Png )
	}
	finally {
		$source.Dispose()
	}
}

function Export-WalkSet( $sourcePath, $outputDir, [string]$label ) {
	if ( -not ( Test-Path -LiteralPath $sourcePath ) ) {
		Write-Host "Skip $label - missing source '$sourcePath'"
		return
	}

	Copy-Png $sourcePath ( Join-Path $outputDir "idle.png" )
	for ( $i = 0; $i -lt $frameCount; $i++ ) {
		$dest = Join-Path $outputDir ( "walk_{0}.png" -f $i )
		New-WalkFrame $sourcePath $dest $i
	}
	Write-Host "Generated walk set for $label -> $outputDir"
}

if ( -not ( Test-Path -LiteralPath $modelsRoot ) ) {
	Write-Error "Models folder not found: $modelsRoot"
	exit 1
}

Export-WalkSet ( Join-Path $modelsRoot "player_sprite.png" ) ( Join-Path $animationsRoot "player\down" ) "player/down"
Export-WalkSet ( Join-Path $modelsRoot "guest_sprite.png" ) ( Join-Path $animationsRoot "guest" ) "guest"
foreach ( $guestVariant in @( "guest_boy_1", "guest_boy_2", "guest_girl_1" ) ) {
	$sourcePath = Join-Path $modelsRoot "$guestVariant.png"
	Remove-CheckerboardBackground $sourcePath | Out-Null
	Export-WalkSet $sourcePath ( Join-Path $animationsRoot $guestVariant ) $guestVariant
}

$playerDown = Join-Path $animationsRoot "player\down"
$playerFacings = @{
	left  = { param( $src, $dest ) Copy-Png $src $dest }
	right = { param( $src, $dest ) Copy-Png $src $dest }
	up    = { param( $src, $dest ) Copy-Png $src $dest }
}

foreach ( $facing in @( "left", "right", "up" ) ) {
	$targetDir = Join-Path $animationsRoot "player\$facing"
	if ( -not ( Test-Path -LiteralPath ( Join-Path $playerDown "idle.png" ) ) ) { continue }

	Copy-Png ( Join-Path $playerDown "idle.png" ) ( Join-Path $targetDir "idle.png" )
	for ( $i = 0; $i -lt $frameCount; $i++ ) {
		Copy-Png ( Join-Path $playerDown ( "walk_{0}.png" -f $i ) ) ( Join-Path $targetDir ( "walk_{0}.png" -f $i ) )
	}
	Write-Host "Copied player/down walk set to player/$facing (replace with directional art later)"
}

$animalsDir = Join-Path $modelsRoot "animals"
if ( Test-Path -LiteralPath $animalsDir ) {
	Get-ChildItem -LiteralPath $animalsDir -Filter "*.png" -File | ForEach-Object {
		$stem = [System.IO.Path]::GetFileNameWithoutExtension( $_.Name )
		Export-WalkSet $_.FullName ( Join-Path $animationsRoot "animals\$stem" ) "animal/$stem"
	}
}

Write-Host "Done. Original PNGs under Assets/models were not modified."
