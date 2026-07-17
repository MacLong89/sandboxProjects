namespace Sandbox;

/// <summary>
/// Builds cozy pixel textures at runtime so the game is playable before/without imported sheets.
/// Final art can replace these via Texture.Load paths in SpriteAtlas.
/// </summary>
public static class ProceduralSpriteFactory
{
	private static readonly Dictionary<string, Texture> Cache = new();

	public static Texture Get( string id, int w, int h, Action<Color32[], int, int> paint )
	{
		if ( Cache.TryGetValue( id, out var existing ) && existing.IsValid() )
			return existing;

		var pixels = new Color32[w * h];
		paint( pixels, w, h );
		var tex = Texture.Create( w, h )
			.WithName( $"proc_{id}" )
			.WithDynamicUsage()
			.WithData( pixels )
			.Finish();
		Cache[id] = tex;
		return tex;
	}

	public static Texture Solid( string id, int w, int h, Color color )
	{
		return Get( id, w, h, ( px, width, height ) =>
		{
			var c = (Color32)color;
			for ( var i = 0; i < px.Length; i++ ) px[i] = c;
		} );
	}

	public static Texture Fish( string id, Color body, Color fin )
	{
		return Get( id, 32, 16, ( px, w, h ) =>
		{
			Clear( px );
			FillEllipse( px, w, h, 14, 8, 10, 5, body );
			FillTriangle( px, w, h, 24, 8, 30, 3, 30, 13, fin );
			Set( px, w, h, 10, 6, Color.White );
			Set( px, w, h, 9, 6, Color.Black );
		} );
	}

	public static Texture Boat( string id, Color hull, Color cabin )
	{
		return Get( id, 64, 32, ( px, w, h ) =>
		{
			Clear( px );
			FillRect( px, w, h, 4, 16, 56, 10, hull );
			FillRect( px, w, h, 28, 6, 18, 12, cabin );
			FillRect( px, w, h, 32, 2, 4, 6, Color.Gray );
			FillRect( px, w, h, 8, 14, 6, 3, new Color( 0.9f, 0.2f, 0.2f ) );
		} );
	}

	public static Texture Player( string id )
	{
		return Get( id, 32, 48, ( px, w, h ) =>
		{
			Clear( px );
			// Cap
			FillRect( px, w, h, 8, 2, 16, 8, new Color( 0.15f, 0.4f, 0.65f ) );
			FillRect( px, w, h, 6, 6, 20, 3, new Color( 0.1f, 0.3f, 0.5f ) );
			// Head
			FillRect( px, w, h, 10, 10, 12, 10, new Color( 0.93f, 0.78f, 0.62f ) );
			Set( px, w, h, 13, 14, new Color( 0.15f, 0.12f, 0.1f ) );
			Set( px, w, h, 18, 14, new Color( 0.15f, 0.12f, 0.1f ) );
			// Coat
			FillRect( px, w, h, 8, 20, 16, 16, new Color( 0.22f, 0.38f, 0.55f ) );
			FillRect( px, w, h, 11, 22, 10, 8, new Color( 0.75f, 0.55f, 0.28f ) ); // lifevest-ish
			// Arms
			FillRect( px, w, h, 4, 22, 5, 10, new Color( 0.22f, 0.38f, 0.55f ) );
			FillRect( px, w, h, 23, 22, 5, 10, new Color( 0.22f, 0.38f, 0.55f ) );
			// Legs
			FillRect( px, w, h, 10, 36, 5, 10, new Color( 0.18f, 0.2f, 0.28f ) );
			FillRect( px, w, h, 17, 36, 5, 10, new Color( 0.18f, 0.2f, 0.28f ) );
			// Boots
			FillRect( px, w, h, 9, 44, 6, 4, new Color( 0.35f, 0.22f, 0.12f ) );
			FillRect( px, w, h, 17, 44, 6, 4, new Color( 0.35f, 0.22f, 0.12f ) );
		} );
	}

	public static Texture Panel( string id, int w, int h )
	{
		return Get( id, w, h, ( px, width, height ) =>
		{
			var bg = new Color( 0.12f, 0.16f, 0.18f, 0.92f );
			var border = new Color( 0.75f, 0.55f, 0.25f );
			FillRect( px, width, height, 0, 0, width, height, bg );
			for ( var x = 0; x < width; x++ )
			{
				Set( px, width, height, x, 0, border );
				Set( px, width, height, x, height - 1, border );
			}
			for ( var y = 0; y < height; y++ )
			{
				Set( px, width, height, 0, y, border );
				Set( px, width, height, width - 1, y, border );
			}
		} );
	}

	public static Texture Background( string id, Color skyTop, Color skyBot, Color water, Color sand )
	{
		return Get( id, 320, 180, ( px, w, h ) =>
		{
			for ( var y = 0; y < h; y++ )
			for ( var x = 0; x < w; x++ )
			{
				Color c;
				if ( y < h * 0.42f )
				{
					var t = y / (h * 0.42f);
					c = Color.Lerp( skyTop, skyBot, t );
				}
				else if ( y < h * 0.88f )
				{
					var t = (y - h * 0.42f) / (h * 0.46f);
					c = Color.Lerp( water, water * 0.55f, t );
					if ( (x + y * 3) % 37 == 0 ) c *= 1.08f;
				}
				else
				{
					c = sand;
					if ( (x * 13 + y * 7) % 29 == 0 ) c *= 0.85f;
				}
				px[y * w + x] = c;
			}
		} );
	}

	public static void Clear( Color32[] px )
	{
		for ( var i = 0; i < px.Length; i++ )
			px[i] = new Color32( 0, 0, 0, 0 );
	}

	public static void Set( Color32[] px, int w, int h, int x, int y, Color c )
	{
		if ( (uint)x >= (uint)w || (uint)y >= (uint)h ) return;
		px[y * w + x] = c;
	}

	public static void FillRect( Color32[] px, int w, int h, int x, int y, int rw, int rh, Color c )
	{
		for ( var yy = y; yy < y + rh; yy++ )
		for ( var xx = x; xx < x + rw; xx++ )
			Set( px, w, h, xx, yy, c );
	}

	public static void FillEllipse( Color32[] px, int w, int h, int cx, int cy, int rx, int ry, Color c )
	{
		for ( var y = -ry; y <= ry; y++ )
		for ( var x = -rx; x <= rx; x++ )
		{
			if ( (x * x) * (ry * ry) + (y * y) * (rx * rx) <= rx * rx * ry * ry )
				Set( px, w, h, cx + x, cy + y, c );
		}
	}

	public static void FillTriangle( Color32[] px, int w, int h, int x0, int y0, int x1, int y1, int x2, int y2, Color c )
	{
		var minX = Math.Min( x0, Math.Min( x1, x2 ) );
		var maxX = Math.Max( x0, Math.Max( x1, x2 ) );
		var minY = Math.Min( y0, Math.Min( y1, y2 ) );
		var maxY = Math.Max( y0, Math.Max( y1, y2 ) );
		for ( var y = minY; y <= maxY; y++ )
		for ( var x = minX; x <= maxX; x++ )
		{
			if ( PointInTri( x, y, x0, y0, x1, y1, x2, y2 ) )
				Set( px, w, h, x, y, c );
		}
	}

	private static bool PointInTri( int px, int py, int x0, int y0, int x1, int y1, int x2, int y2 )
	{
		var d1 = Sign( px, py, x0, y0, x1, y1 );
		var d2 = Sign( px, py, x1, y1, x2, y2 );
		var d3 = Sign( px, py, x2, y2, x0, y0 );
		var hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
		var hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
		return !(hasNeg && hasPos);
	}

	private static int Sign( int px, int py, int x1, int y1, int x2, int y2 )
		=> (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
}
