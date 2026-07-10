namespace Terraingen.TerrainGen;

/// <summary>
/// Loads grayscale heightmaps (black = low, white = high) from game content.
/// </summary>
public static class HeightmapLoader
{
	public static HeightmapField LoadFromContent( string contentPath )
	{
		var texture = Texture.Load( contentPath );
		if ( !texture.IsValid() )
			throw new InvalidOperationException( $"Could not load heightmap: {contentPath}" );

		var width = texture.Width;
		var height = texture.Height;
		var field = new HeightmapField( width, height );

		var pixels = texture.GetPixels( 0 );
		for ( int i = 0; i < pixels.Length; i++ )
		{
			var p = pixels[i];
			field.Heights[i] = p.r * 0.299f + p.g * 0.587f + p.b * 0.114f;
		}

		NormalizeField( field );
		return field;
	}

	static void NormalizeField( HeightmapField field )
	{
		var min = float.MaxValue;
		var max = float.MinValue;

		foreach ( var h in field.Heights )
		{
			min = Math.Min( min, h );
			max = Math.Max( max, h );
		}

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
