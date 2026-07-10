namespace Terraingen.TerrainGen;

public static class HeightmapBlur
{
	public static float[] BoxBlur( HeightmapField field, int radius )
	{
		var output = new float[field.Heights.Length];

		for ( int y = 0; y < field.Height; y++ )
		{
			for ( int x = 0; x < field.Width; x++ )
			{
				var sum = 0f;
				var count = 0;

				for ( int oy = -radius; oy <= radius; oy++ )
				{
					for ( int ox = -radius; ox <= radius; ox++ )
					{
						sum += field.Get( x + ox, y + oy );
						count++;
					}
				}

				output[field.Index( x, y )] = sum / count;
			}
		}

		return output;
	}
}
