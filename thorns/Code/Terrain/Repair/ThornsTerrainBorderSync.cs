namespace Sandbox;

/// <summary>Welds heightmap borders so chunk edges share consistent samples (single-chunk map edges included).</summary>
public static class ThornsTerrainBorderSync
{
	public sealed class ChunkBorderSample
	{
		public int EdgeIndex { get; init; }
		public int SampleIndex { get; init; }
		public float Height { get; init; }
	}

	/// <summary>Edge index: 0=west, 1=east, 2=south, 3=north.</summary>
	public static int SynchronizeMapEdges(
		int width,
		int height,
		Span<float> heights,
		int weldWidth,
		IReadOnlyList<ChunkBorderSample> neighborEdgeSamples = null )
	{
		if ( width < 3 || height < 3 )
			return 0;

		var w = Math.Clamp( weldWidth, 1, 4 );
		var adjusted = 0;
		adjusted += WeldVerticalEdge( width, height, heights, x: 0, inward: 1, w );
		adjusted += WeldVerticalEdge( width, height, heights, x: width - 1, inward: -1, w );
		adjusted += WeldHorizontalEdge( width, height, heights, y: 0, inward: 1, w );
		adjusted += WeldHorizontalEdge( width, height, heights, y: height - 1, inward: -1, w );

		if ( neighborEdgeSamples is { Count: > 0 } )
			adjusted += ApplyNeighborEdgeSamples( width, height, heights, neighborEdgeSamples );

		return adjusted;
	}

	static int WeldVerticalEdge( int width, int height, Span<float> heights, int x, int inward, int weldWidth )
	{
		var adjusted = 0;
		for ( var y = 0; y < height; y++ )
		{
			var edgeIdx = y * width + x;
			var innerIdx = y * width + Math.Clamp( x + inward, 0, width - 1 );
			var target = heights[innerIdx];
			for ( var d = 0; d < weldWidth; d++ )
			{
				var xi = Math.Clamp( x + inward * d, 0, width - 1 );
				var idx = y * width + xi;
				var t = 1f - d / (float)(weldWidth + 1);
				var nh = heights[idx] + (target - heights[idx]) * t * 0.65f;
				if ( MathF.Abs( nh - heights[idx] ) > 0.25f )
				{
					heights[idx] = nh;
					adjusted++;
				}
			}

			heights[edgeIdx] = target;
		}

		return adjusted;
	}

	static int WeldHorizontalEdge( int width, int height, Span<float> heights, int y, int inward, int weldWidth )
	{
		var adjusted = 0;
		for ( var x = 0; x < width; x++ )
		{
			var edgeIdx = y * width + x;
			var innerY = Math.Clamp( y + inward, 0, height - 1 );
			var innerIdx = innerY * width + x;
			var target = heights[innerIdx];
			for ( var d = 0; d < weldWidth; d++ )
			{
				var yi = Math.Clamp( y + inward * d, 0, height - 1 );
				var idx = yi * width + x;
				var t = 1f - d / (float)(weldWidth + 1);
				var nh = heights[idx] + (target - heights[idx]) * t * 0.65f;
				if ( MathF.Abs( nh - heights[idx] ) > 0.25f )
				{
					heights[idx] = nh;
					adjusted++;
				}
			}

			heights[edgeIdx] = target;
		}

		return adjusted;
	}

	static int ApplyNeighborEdgeSamples(
		int width,
		int height,
		Span<float> heights,
		IReadOnlyList<ChunkBorderSample> samples )
	{
		var adjusted = 0;
		for ( var i = 0; i < samples.Count; i++ )
		{
			var s = samples[i];
			var idx = MapBorderIndex( width, height, s.EdgeIndex, s.SampleIndex );
			if ( idx < 0 || idx >= heights.Length )
				continue;

			if ( MathF.Abs( heights[idx] - s.Height ) > 0.01f )
			{
				heights[idx] = s.Height;
				adjusted++;
			}
		}

		return adjusted;
	}

	static int MapBorderIndex( int width, int height, int edgeIndex, int sampleIndex )
	{
		sampleIndex = Math.Clamp( sampleIndex, 0, edgeIndex is 0 or 1 ? height - 1 : width - 1 );
		return edgeIndex switch
		{
			0 => sampleIndex * width,
			1 => sampleIndex * width + (width - 1),
			2 => sampleIndex,
			3 => (height - 1) * width + sampleIndex,
			_ => -1
		};
	}
}
