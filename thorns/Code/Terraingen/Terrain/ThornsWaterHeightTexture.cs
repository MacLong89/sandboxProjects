namespace Terraingen.TerrainGen;

/// <summary>
/// Bakes the sculpted heightfield to a GPU texture for shoreline depth in the water shader.
/// </summary>
public static class ThornsWaterHeightTexture
{
	public static Texture Create( HeightmapField field, int maxResolution = 1024 )
	{
		if ( field is null )
			return null;

		try
		{
			var target = Math.Min( maxResolution, NextPowerOfTwo( Math.Max( field.Width, field.Height ) ) );
			target = Math.Clamp( target, 64, 2048 );

			var bitmap = new Bitmap( target, target );
			var denom = Math.Max( target - 1, 1 );
			for ( var y = 0; y < target; y++ )
			{
				var v = y / (float)denom;
				for ( var x = 0; x < target; x++ )
				{
					var u = x / (float)denom;
					var height = field.SampleBilinear( u, v );
					var gray = (byte)Math.Clamp( (int)(height * 255f), 0, 255 );
					bitmap.SetPixel( x, y, new Color( gray / 255f, gray / 255f, gray / 255f ) );
				}
			}

			return bitmap.ToTexture();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Thorns Water] Height texture bake failed: {e.Message}" );
			return null;
		}
	}

	static int NextPowerOfTwo( int value )
	{
		var power = 1;
		while ( power < value )
			power <<= 1;
		return power;
	}
}
