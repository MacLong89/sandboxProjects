namespace UnderPressure;

/// <summary>Story symbols carved or stenciled into the clean surface beneath the grime.</summary>
public enum SecretSymbol
{
	Star,
	Compass,
	Eye,
	Target,
	Interlock,
	Scratches,
	Biohazard,
	Crosshair,
}

/// <summary>One hidden mark revealed when the grime above is washed away.</summary>
public sealed class SurfaceSecret
{
	public SecretSymbol? Symbol { get; init; }
	public string Text { get; init; }

	public float X { get; init; } = 0.5f;
	public float Y { get; init; } = 0.5f;
	public float Scale { get; init; } = 1f;
	public Color? Color { get; init; }
	public bool Centered { get; init; } = true;

	/// <summary>Stable key for save tracking — e.g. <c>L03_they_know</c>.</summary>
	public string DiscoveryId { get; init; }

	/// <summary>Leo's inner monologue when this clue is uncovered.</summary>
	public string Monologue { get; init; }

	/// <summary>Fraction of the clue's bounding box that must be cleaned before the popup fires.</summary>
	public float RevealThreshold { get; init; } = 0.55f;
}

/// <summary>Bakes etched secrets into the surface layer beneath dirt and paint.</summary>
public static class SecretRaster
{
	public static void Apply( Color32[] pixels, int texW, int texH, IReadOnlyList<SurfaceSecret> secrets, Color baseColor )
	{
		if ( pixels is null )
			return;

		FillBase( pixels, baseColor );

		if ( secrets is null || secrets.Count == 0 )
			return;

		foreach ( var secret in secrets )
		{
			var ink = secret.Color ?? EtchColor( baseColor );

			if ( !string.IsNullOrWhiteSpace( secret.Text ) )
			{
				GraffitiRaster.StampTextLine( pixels, texW, texH, new GraffitiLine
				{
					Text = secret.Text,
					X = secret.X,
					Y = secret.Y,
					Scale = secret.Scale,
					Color = ink,
					Centered = secret.Centered,
				} );
			}

			if ( secret.Symbol is { } symbol )
				StampSymbol( pixels, texW, texH, symbol, secret.X, secret.Y, secret.Scale, ink );
		}
	}

	private static void FillBase( Color32[] pixels, Color baseColor )
	{
		var c = new Color32(
			(byte)Math.Clamp( (int)(baseColor.r * 255f), 0, 255 ),
			(byte)Math.Clamp( (int)(baseColor.g * 255f), 0, 255 ),
			(byte)Math.Clamp( (int)(baseColor.b * 255f), 0, 255 ),
			255 );

		for ( var i = 0; i < pixels.Length; i++ )
			pixels[i] = c;
	}

	private static Color EtchColor( Color baseColor ) =>
		new(
			baseColor.r * 0.42f,
			baseColor.g * 0.38f,
			baseColor.b * 0.34f,
			1f );

	private static void StampSymbol( Color32[] pixels, int texW, int texH, SecretSymbol symbol, float nx, float ny, float scale, Color color )
	{
		var size = (int)MathF.Round( 28f * Math.Max( 0.5f, scale ) );
		var cx = (int)MathF.Round( nx * texW );
		var cy = (int)MathF.Round( ny * texH );

		switch ( symbol )
		{
			case SecretSymbol.Star: DrawStar( pixels, texW, texH, cx, cy, size, color ); break;
			case SecretSymbol.Compass: DrawCompass( pixels, texW, texH, cx, cy, size, color ); break;
			case SecretSymbol.Eye: DrawEye( pixels, texW, texH, cx, cy, size, color ); break;
			case SecretSymbol.Target: DrawTarget( pixels, texW, texH, cx, cy, size, color ); break;
			case SecretSymbol.Interlock: DrawInterlock( pixels, texW, texH, cx, cy, size, color ); break;
			case SecretSymbol.Scratches: DrawScratches( pixels, texW, texH, cx, cy, size, color ); break;
			case SecretSymbol.Biohazard: DrawBiohazard( pixels, texW, texH, cx, cy, size, color ); break;
			case SecretSymbol.Crosshair: DrawCrosshair( pixels, texW, texH, cx, cy, size, color ); break;
		}
	}

	/// <summary>Approximate texel bounds for a stamped secret or graffiti line (for discovery tracking).</summary>
	public static void EstimateBounds( int texW, int texH, float nx, float ny, float scale, string text, SecretSymbol? symbol, bool centered,
		out int minX, out int minY, out int maxX, out int maxY )
	{
		var ix0 = int.MaxValue;
		var iy0 = int.MaxValue;
		var ix1 = int.MinValue;
		var iy1 = int.MinValue;

		if ( !string.IsNullOrWhiteSpace( text ) )
		{
			var s = Math.Max( 1, (int)MathF.Round( scale ) );
			var charStep = 6 * s;
			var textW = text.Length * charStep;
			var startX = centered
				? (int)MathF.Round( nx * texW - textW * 0.5f )
				: (int)MathF.Round( nx * texW );
			var baselineY = (int)MathF.Round( ny * texH );
			ix0 = Math.Min( ix0, startX );
			ix1 = Math.Max( ix1, startX + textW );
			iy0 = Math.Min( iy0, baselineY - 7 * s );
			iy1 = Math.Max( iy1, baselineY + s );
		}

		if ( symbol is not null )
		{
			var size = (int)MathF.Round( 28f * Math.Max( 0.5f, scale ) );
			var cx = (int)MathF.Round( nx * texW );
			var cy = (int)MathF.Round( ny * texH );
			ix0 = Math.Min( ix0, cx - size );
			ix1 = Math.Max( ix1, cx + size );
			iy0 = Math.Min( iy0, cy - size );
			iy1 = Math.Max( iy1, cy + size );
		}

		if ( ix0 == int.MaxValue )
		{
			ix0 = ix1 = (int)MathF.Round( nx * texW );
			iy0 = iy1 = (int)MathF.Round( ny * texH );
		}

		var pad = Math.Max( 2, (int)(4f * Math.Max( 1f, scale ) ) );
		minX = Math.Clamp( ix0 - pad, 0, texW - 1 );
		maxX = Math.Clamp( ix1 + pad, 0, texW - 1 );
		minY = Math.Clamp( iy0 - pad, 0, texH - 1 );
		maxY = Math.Clamp( iy1 + pad, 0, texH - 1 );
	}

	private static void DrawStar( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		for ( var i = 0; i < 5; i++ )
		{
			var a = i * MathF.PI * 2f / 5f - MathF.PI * 0.5f;
			var x2 = cx + (int)(MathF.Cos( a ) * size);
			var y2 = cy + (int)(MathF.Sin( a ) * size);
			DrawLine( px, w, h, cx, cy, x2, y2, Math.Max( 2, size / 8 ), c );
		}

		FillCircle( px, w, h, cx, cy, size / 5, c );
	}

	private static void DrawCompass( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		DrawCircle( px, w, h, cx, cy, size, Math.Max( 2, size / 10 ), c );
		DrawLine( px, w, h, cx, cy - size, cx, cy + size, Math.Max( 2, size / 10 ), c );
		DrawLine( px, w, h, cx - size, cy, cx + size, cy, Math.Max( 2, size / 10 ), c );
		FillBlock( px, w, h, cx - size / 4, cy - size - size / 3, size / 2, size / 3, c );
	}

	private static void DrawEye( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		DrawEllipse( px, w, h, cx, cy, size * 1.4f, size * 0.75f, Math.Max( 2, size / 9 ), c );
		FillCircle( px, w, h, cx, cy, size / 2, c );
		FillCircle( px, w, h, cx, cy, size / 5, new Color( 0.95f, 0.95f, 0.92f ) );
	}

	private static void DrawTarget( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		DrawCircle( px, w, h, cx, cy, size, Math.Max( 2, size / 10 ), c );
		DrawCircle( px, w, h, cx, cy, size * 2 / 3, Math.Max( 2, size / 10 ), c );
		DrawCircle( px, w, h, cx, cy, size / 3, Math.Max( 2, size / 10 ), c );
		FillCircle( px, w, h, cx, cy, size / 8, c );
	}

	private static void DrawInterlock( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		var r = size / 2;
		DrawCircle( px, w, h, cx - r / 2, cy, r, Math.Max( 2, size / 12 ), c );
		DrawCircle( px, w, h, cx + r / 2, cy, r, Math.Max( 2, size / 12 ), c );
		DrawCircle( px, w, h, cx, (int)(cy - r * 0.55f), r, Math.Max( 2, size / 12 ), c );
	}

	private static void DrawScratches( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		var thick = Math.Max( 2, size / 10 );
		for ( var i = -2; i <= 2; i++ )
		{
			var ox = i * size / 5;
			DrawLine( px, w, h, cx + ox - size / 2, cy - size / 2, cx + ox + size / 3, cy + size / 2, thick, c );
			DrawLine( px, w, h, cx + ox + size / 3, cy - size / 2, cx + ox - size / 2, cy + size / 2, thick, c );
		}
	}

	private static void DrawBiohazard( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		for ( var i = 0; i < 3; i++ )
		{
			var a = i * MathF.PI * 2f / 3f;
			var lx = cx + (int)(MathF.Cos( a ) * size * 0.55f );
			var ly = cy + (int)(MathF.Sin( a ) * size * 0.55f );
			DrawCircle( px, w, h, lx, ly, size / 2, Math.Max( 2, size / 12 ), c );
		}

		DrawCircle( px, w, h, cx, cy, size / 4, Math.Max( 2, size / 12 ), c );
	}

	private static void DrawCrosshair( Color32[] px, int w, int h, int cx, int cy, int size, Color c )
	{
		var thick = Math.Max( 2, size / 10 );
		DrawCircle( px, w, h, cx, cy, size, thick, c );
		DrawLine( px, w, h, cx - size, cy, cx + size, cy, thick, c );
		DrawLine( px, w, h, cx, cy - size, cx, cy + size, thick, c );
	}

	private static void DrawLine( Color32[] px, int w, int h, int x0, int y0, int x1, int y1, int thick, Color c )
	{
		var steps = Math.Max( Math.Abs( x1 - x0 ), Math.Abs( y1 - y0 ) );
		for ( var i = 0; i <= steps; i++ )
		{
			var t = steps == 0 ? 0f : i / (float)steps;
			var x = (int)MathF.Round( x0 + (x1 - x0) * t );
			var y = (int)MathF.Round( y0 + (y1 - y0) * t );
			FillBlock( px, w, h, x - thick / 2, y - thick / 2, thick, thick, c );
		}
	}

	private static void DrawCircle( Color32[] px, int w, int h, int cx, int cy, int radius, int thick, Color c )
	{
		for ( var a = 0; a < 360; a += 2 )
		{
			var rad = a * MathF.PI / 180f;
			var x = cx + (int)(MathF.Cos( rad ) * radius );
			var y = cy + (int)(MathF.Sin( rad ) * radius );
			FillBlock( px, w, h, x - thick / 2, y - thick / 2, thick, thick, c );
		}
	}

	private static void DrawEllipse( Color32[] px, int w, int h, int cx, int cy, float rx, float ry, int thick, Color c )
	{
		for ( var a = 0; a < 360; a += 2 )
		{
			var rad = a * MathF.PI / 180f;
			var x = cx + (int)(MathF.Cos( rad ) * rx );
			var y = cy + (int)(MathF.Sin( rad ) * ry );
			FillBlock( px, w, h, x - thick / 2, y - thick / 2, thick, thick, c );
		}
	}

	private static void FillCircle( Color32[] px, int w, int h, int cx, int cy, int radius, Color c )
	{
		for ( var y = cy - radius; y <= cy + radius; y++ )
		for ( var x = cx - radius; x <= cx + radius; x++ )
		{
			if ( (x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius )
				continue;
			SetPixel( px, w, h, x, y, c );
		}
	}

	private static void FillBlock( Color32[] px, int w, int h, int x, int y, int bw, int bh, Color color )
	{
		var c = new Color32(
			(byte)Math.Clamp( (int)(color.r * 255f), 0, 255 ),
			(byte)Math.Clamp( (int)(color.g * 255f), 0, 255 ),
			(byte)Math.Clamp( (int)(color.b * 255f), 0, 255 ),
			255 );

		for ( var py = y; py < y + bh; py++ )
		for ( var pxX = x; pxX < x + bw; pxX++ )
			SetPixel( px, w, h, pxX, py, c );
	}

	private static void SetPixel( Color32[] px, int w, int h, int x, int y, Color32 c )
	{
		y = h - 1 - y;
		if ( x < 0 || y < 0 || x >= w || y >= h )
			return;
		px[y * w + x] = c;
	}
}
