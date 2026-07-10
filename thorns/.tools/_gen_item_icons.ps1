# One-off placeholders for ids that have no PNG yet. Never overwrites an existing file — delete a PNG manually if you want this to recreate it.
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root 'Assets\textures\ui\item_icons'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
foreach ( $entry in @(
		@{ id = 'm4'; abbr = 'M4' }, @{ id = 'mp5'; abbr = 'MP5' }, @{ id = 'shotgun'; abbr = 'SG' }, @{ id = 'sniper'; abbr = 'SR' }, @{ id = 'm9_bayonet'; abbr = 'M9' },
		@{ id = 'apple'; abbr = 'AP' }, @{ id = 'ammo_basic'; abbr = 'AM' }, @{ id = 'clean_water'; abbr = 'H2' },
		@{ id = 'rifle'; abbr = 'RF' }, @{ id = 'c4'; abbr = 'C4' }
	) ) {
	$id = $entry.id
	$abbr = $entry.abbr
	$outPath = Join-Path $dir "$id.png"
	if ( Test-Path -LiteralPath $outPath ) { continue }
	$bmp = New-Object Drawing.Bitmap 56, 56
	$g = [Drawing.Graphics]::FromImage( $bmp )
	$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
	$brush = New-Object Drawing.SolidBrush ( [Drawing.Color]::FromArgb( 255, 52, 56, 62 ) )
	$g.FillRectangle( $brush, 0, 0, 56, 56 )
	$accent = switch ( $id ) {
		'm4' { [Drawing.Color]::FromArgb( 255, 110, 180, 230 ) }
		'mp5' { [Drawing.Color]::FromArgb( 255, 120, 200, 150 ) }
		'shotgun' { [Drawing.Color]::FromArgb( 255, 220, 180, 110 ) }
		'sniper' { [Drawing.Color]::FromArgb( 255, 200, 130, 200 ) }
		'apple' { [Drawing.Color]::FromArgb( 255, 230, 130, 130 ) }
		'ammo_basic' { [Drawing.Color]::FromArgb( 255, 220, 210, 140 ) }
		'clean_water' { [Drawing.Color]::FromArgb( 255, 120, 185, 235 ) }
		'rifle' { [Drawing.Color]::FromArgb( 255, 160, 200, 120 ) }
		'c4' { [Drawing.Color]::FromArgb( 255, 235, 90, 70 ) }
		default { [Drawing.Color]::FromArgb( 255, 200, 200, 200 ) }
	}
	$brush2 = New-Object Drawing.SolidBrush ( $accent )
	$font = New-Object Drawing.Font( 'Segoe UI', 13, [Drawing.FontStyle]::Bold )
	$sf = New-Object Drawing.StringFormat
	$sf.Alignment = [Drawing.StringAlignment]::Center
	$sf.LineAlignment = [Drawing.StringAlignment]::Center
	$rect = New-Object Drawing.RectangleF( 4, 4, 48, 48 )
	$g.DrawString( $abbr, $font, $brush2, $rect, $sf )
	$g.Dispose()
	$bmp.Save( $outPath, [Drawing.Imaging.ImageFormat]::Png )
	$bmp.Dispose()
}
Write-Host "Done: $dir (only created missing PNGs; existing files left unchanged)"
