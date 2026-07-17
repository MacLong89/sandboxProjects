namespace Offshore;

/// <summary>Creates simple runtime textures for dynamic scene layers.</summary>
public static class ProceduralSprites
{
	private static Texture _white;
	private static Texture _softDisc;
	private static Texture _stars;

	public static Texture White()
	{
		if ( _white is not null && _white.IsValid() )
			return _white;

		var px = new byte[] { 255, 255, 255, 255 };
		_white = Texture.Create( 1, 1, ImageFormat.RGBA8888 )
			.WithName( "offshore_white" )
			.WithData( px )
			.Finish();
		return _white ?? Texture.White;
	}

	public static Texture SoftDisc()
	{
		if ( _softDisc is not null && _softDisc.IsValid() )
			return _softDisc;

		const int size = 128;
		var px = new byte[size * size * 4];
		var cx = (size - 1) * 0.5f;
		var cy = (size - 1) * 0.5f;
		var radius = size * 0.42f;

		for ( var y = 0; y < size; y++ )
		{
			for ( var x = 0; x < size; x++ )
			{
				var dx = x - cx;
				var dy = y - cy;
				var d = MathF.Sqrt( dx * dx + dy * dy );
				var t = Math.Clamp( 1f - (d - radius * 0.55f) / (radius * 0.55f), 0f, 1f );
				t = t * t * (3f - 2f * t);
				var i = (y * size + x) * 4;
				px[i] = 255;
				px[i + 1] = 255;
				px[i + 2] = 255;
				px[i + 3] = (byte)(t * 255f);
			}
		}

		_softDisc = Texture.Create( size, size, ImageFormat.RGBA8888 )
			.WithName( "offshore_soft_disc" )
			.WithData( px )
			.Finish();
		return _softDisc ?? Texture.White;
	}

	public static Texture Starfield()
	{
		if ( _stars is not null && _stars.IsValid() )
			return _stars;

		// Higher res so 1px stars read ~25% the old size when stretched across the sky.
		const int w = 2048;
		const int h = 1024;
		var px = new byte[w * h * 4];
		var rng = new Random( 42 );

		for ( var n = 0; n < 900; n++ )
		{
			var x = rng.Next( 0, w );
			var y = rng.Next( 0, h );
			var bright = (byte)rng.Next( 170, 255 );
			// Almost all single-pixel; rare tiny 2px for a few brighter ones.
			var r = rng.Next( 0, 40) == 0 ? 1 : 0;
			for ( var oy = -r; oy <= r; oy++ )
			{
				for ( var ox = -r; ox <= r; ox++ )
				{
					if ( ox * ox + oy * oy > r * r )
						continue;
					var sx = x + ox;
					var sy = y + oy;
					if ( (uint)sx >= w || (uint)sy >= h )
						continue;
					var i = (sy * w + sx) * 4;
					var a = (byte)(bright * (ox == 0 && oy == 0 ? 1f : 0.4f));
					px[i] = bright;
					px[i + 1] = bright;
					px[i + 2] = (byte)Math.Min( 255, bright + 18 );
					px[i + 3] = a;
				}
			}
		}

		_stars = Texture.Create( w, h, ImageFormat.RGBA8888 )
			.WithName( "offshore_starfield_v2" )
			.WithData( px )
			.Finish();
		return _stars ?? Texture.White;
	}
}
