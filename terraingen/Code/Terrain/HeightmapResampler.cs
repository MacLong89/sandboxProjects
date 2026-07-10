namespace Terraingen.TerrainGen;

public static class HeightmapResampler
{
	public static HeightmapField Resize( HeightmapField source, int targetWidth, int targetHeight )
	{
		var result = new HeightmapField( targetWidth, targetHeight );

		for ( int y = 0; y < targetHeight; y++ )
		{
			var v = y / (float)(targetHeight - 1);
			for ( int x = 0; x < targetWidth; x++ )
			{
				var u = x / (float)(targetWidth - 1);
				result.Set( x, y, source.SampleBilinear( u, v ) );
			}
		}

		return result;
	}

	public static HeightmapField ExtractRegion( HeightmapField source, int originX, int originY, int cropWidth, int cropHeight, bool flipY = false )
	{
		var crop = new HeightmapField( cropWidth, cropHeight );

		for ( int y = 0; y < cropHeight; y++ )
		{
			var sy = flipY ? originY + (cropHeight - 1 - y) : originY + y;

			for ( int x = 0; x < cropWidth; x++ )
			{
				var sx = originX + x;
				crop.Set( x, y, source.Get( sx, sy ) );
			}
		}

		Normalize( crop );
		return crop;
	}

	static void Normalize( HeightmapField field )
	{
		var min = field.Heights.Min();
		var max = field.Heights.Max();
		var range = max - min;
		if ( range < 0.0001f )
		{
			Array.Fill( field.Heights, 0.5f );
			return;
		}

		for ( int i = 0; i < field.Heights.Length; i++ )
			field.Heights[i] = (field.Heights[i] - min) / range;
	}
}
