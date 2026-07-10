namespace Terraingen.TerrainGen;

/// <summary>
/// Normalized height field (0 = low, 1 = high). All sculpt passes operate on this representation.
/// </summary>
public sealed class HeightmapField
{
	public int Width { get; }
	public int Height { get; }
	public float[] Heights { get; }

	public HeightmapField( int width, int height )
	{
		Width = width;
		Height = height;
		Heights = new float[width * height];
	}

	public HeightmapField( int width, int height, float[] heights )
	{
		Width = width;
		Height = height;
		Heights = heights;
	}

	public int Index( int x, int y ) => x + y * Width;

	public float Get( int x, int y )
	{
		x = Math.Clamp( x, 0, Width - 1 );
		y = Math.Clamp( y, 0, Height - 1 );
		return Heights[Index( x, y )];
	}

	public void Set( int x, int y, float value ) => Heights[Index( x, y )] = Math.Clamp( value, 0f, 1f );

	public HeightmapField Clone()
	{
		var copy = new HeightmapField( Width, Height );
		if ( Heights is null || copy.Heights is null )
			return copy;

		var n = Math.Min( Heights.Length, copy.Heights.Length );
		for ( var i = 0; i < n; i++ )
			copy.Heights[i] = Heights[i];
		return copy;
	}

	public float SampleBilinear( float u, float v )
	{
		u = Math.Clamp( u, 0f, 1f );
		v = Math.Clamp( v, 0f, 1f );

		var fx = u * (Width - 1);
		var fy = v * (Height - 1);

		var x0 = (int)Math.Floor( fx );
		var y0 = (int)Math.Floor( fy );
		var x1 = Math.Min( x0 + 1, Width - 1 );
		var y1 = Math.Min( y0 + 1, Height - 1 );

		var tx = fx - x0;
		var ty = fy - y0;

		var h00 = Get( x0, y0 );
		var h10 = Get( x1, y0 );
		var h01 = Get( x0, y1 );
		var h11 = Get( x1, y1 );

		var hx0 = h00 + (h10 - h00) * tx;
		var hx1 = h01 + (h11 - h01) * tx;
		return hx0 + (hx1 - hx0) * ty;
	}
}
