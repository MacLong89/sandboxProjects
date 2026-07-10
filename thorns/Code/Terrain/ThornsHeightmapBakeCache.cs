using System.Buffers;

namespace Sandbox;

/// <summary>
/// Shared baked heightmap cache — avoids recomputing <see cref="ThornsTerrainGeometry.FillHeightmap"/>
/// after world-gen phases or a prior client/host mesh bake for the same spec.
/// </summary>
public static class ThornsHeightmapBakeCache
{
	static float[] _heights;
	static int _rx;
	static int _rz;
	static int _cells;
	static long _specToken;
	static long _contentHash;

	public static long ComputeSpecToken( in ThornsTerrainNetSpec spec ) =>
		HashCode.Combine(
			HashCode.Combine(
				spec.Seed,
				spec.TerraingenWorldSeed,
				spec.UseTerraingenWorld,
				spec.HeightmapResolutionX,
				spec.HeightmapResolutionZ,
				spec.ProcBuildingTerrainPads?.Count ?? 0,
				spec.RoadCorridors?.Count ?? 0 ),
			spec.SettlementTerrainInfluences?.Count ?? 0,
			spec.SettlementBlockTerrain?.Count ?? 0 );

	/// <summary>Takes ownership of pooled heights — caller must not return them to <see cref="ArrayPool"/>.</summary>
	public static void Register( in ThornsTerrainNetSpec spec, float[] heights, int rx, int rz, int cells )
	{
		Clear();
		if ( heights is null || cells < 1 )
			return;

		_heights = heights;
		_rx = rx;
		_rz = rz;
		_cells = cells;
		_specToken = ComputeSpecToken( in spec );
	}

	public static void BindContentHash( long contentHash ) => _contentHash = contentHash;

	/// <summary>Stores a copy keyed by terrain replica content hash (client reconnect / rejoin).</summary>
	public static void RegisterMeshBakeCopy( long contentHash, in ThornsTerrainNetSpec spec, ReadOnlySpan<float> heights, int cells )
	{
		if ( contentHash == 0 || cells < 1 || heights.Length < cells )
			return;

		Clear();
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var copy = ArrayPool<float>.Shared.Rent( cells );
		heights.Slice( 0, cells ).CopyTo( copy );

		_heights = copy;
		_rx = rx;
		_rz = rz;
		_cells = cells;
		_specToken = ComputeSpecToken( in spec );
		_contentHash = contentHash;
	}

	public static bool TryCopy( in ThornsTerrainNetSpec spec, Span<float> dest ) =>
		TryCopyInternal( ComputeSpecToken( in spec ), 0, spec.HeightmapResolutionX, spec.HeightmapResolutionZ, dest );

	public static bool TryCopyByContentHash( long contentHash, in ThornsTerrainNetSpec spec, Span<float> dest ) =>
		contentHash != 0 && TryCopyInternal( 0, contentHash, spec.HeightmapResolutionX, spec.HeightmapResolutionZ, dest );

	static bool TryCopyInternal( long specToken, long contentHash, int rx, int rz, Span<float> dest )
	{
		if ( _heights is null || dest.Length < _cells || rx != _rx || rz != _rz )
			return false;

		var tokenMatch = specToken != 0 && specToken == _specToken;
		var hashMatch = contentHash != 0 && contentHash == _contentHash;
		if ( !tokenMatch && !hashMatch )
			return false;

		_heights.AsSpan( 0, _cells ).CopyTo( dest );
		return true;
	}

	/// <summary>Bilinear downsample from cached full-res bake (minimap overview).</summary>
	public static bool TryDownsample( in ThornsTerrainNetSpec spec, int targetRx, int targetRz, Span<float> dest )
	{
		if ( _heights is null || targetRx < 2 || targetRz < 2 )
			return false;

		if ( ComputeSpecToken( in spec ) != _specToken )
			return false;

		if ( spec.HeightmapResolutionX != _rx || spec.HeightmapResolutionZ != _rz )
			return false;

		var count = targetRx * targetRz;
		if ( dest.Length < count )
			return false;

		var src = _heights.AsSpan( 0, _cells );
		for ( var z = 0; z < targetRz; z++ )
		{
			var v = targetRz <= 1 ? 0f : z / (float)(targetRz - 1);
			var gy = v * (_rz - 1);
			for ( var x = 0; x < targetRx; x++ )
			{
				var u = targetRx <= 1 ? 0f : x / (float)(targetRx - 1);
				var gx = u * (_rx - 1);
				dest[z * targetRx + x] = SampleBilinear( src, _rx, _rz, gx, gy );
			}
		}

		return true;
	}

	static float SampleBilinear( ReadOnlySpan<float> heights, int rx, int rz, float gx, float gy )
	{
		gx = Math.Clamp( gx, 0f, rx - 1f );
		gy = Math.Clamp( gy, 0f, rz - 1f );
		var x0 = (int)MathF.Floor( gx );
		var y0 = (int)MathF.Floor( gy );
		var x1 = Math.Min( x0 + 1, rx - 1 );
		var y1 = Math.Min( y0 + 1, rz - 1 );
		var tx = gx - x0;
		var ty = gy - y0;

		var h00 = heights[y0 * rx + x0];
		var h10 = heights[y0 * rx + x1];
		var h01 = heights[y1 * rx + x0];
		var h11 = heights[y1 * rx + x1];
		var h0 = h00 * (1f - tx) + h10 * tx;
		var h1 = h01 * (1f - tx) + h11 * tx;
		return h0 * (1f - ty) + h1 * ty;
	}

	/// <summary>Rents pooled heights and fills from cache or <see cref="ThornsTerrainGeometry.FillHeightmap"/>.</summary>
	public static void RentFilled(
		in ThornsTerrainNetSpec spec,
		out float[] heights,
		out int cells,
		long contentHash = 0 )
	{
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		cells = rx * rz;
		heights = ArrayPool<float>.Shared.Rent( cells );
		var span = heights.AsSpan( 0, cells );
		if ( TryCopy( in spec, span )
		     || ( contentHash != 0 && TryCopyByContentHash( contentHash, in spec, span ) ) )
			return;

		ThornsTerrainGeometry.FillHeightmap( in spec, heights, contentHash );
	}

	public static void Clear()
	{
		if ( _heights is null )
			return;

		ArrayPool<float>.Shared.Return( _heights );
		_heights = null;
		_cells = 0;
		_specToken = 0;
		_contentHash = 0;
	}
}
