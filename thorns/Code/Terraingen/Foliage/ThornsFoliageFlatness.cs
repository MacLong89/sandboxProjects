namespace Terraingen.Foliage;

/// <summary>
/// Rejects tree placements on slopes / uneven footing (avoids floating trunk bases).
/// </summary>
public static class ThornsFoliageFlatness
{
	static readonly float[] FootprintAngles = { 0f, MathF.PI * 0.5f, MathF.PI, MathF.PI * 1.5f };

	public static bool IsTreeFootprintFlat(
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		float worldX,
		float worldY,
		ThornsFoliageConfig config,
		out float maxSlope )
	{
		maxSlope = 0f;

		if ( !sampler.IsAboveSeaLevel( worldX, worldY ) )
			return false;

		if ( !config.RequireTreeFootprintFlatness )
			return true;

		var center = sampler.Sample( worldX, worldY );
		if ( center.Slope > sampler.MaxTreeSlope )
			return false;

		maxSlope = center.Slope;
		var minSlope = center.Slope;

		if ( !TrySampleSurfaceZ( terrain, worldX, worldY, out var centerZ ) )
			return false;

		var minZ = centerZ;
		var maxZ = centerZ;
		var radius = config.TreeFootprintSampleRadiusInches;

		foreach ( var angle in FootprintAngles )
		{
			var px = worldX + MathF.Cos( angle ) * radius;
			var py = worldY + MathF.Sin( angle ) * radius;

			var sample = sampler.Sample( px, py );
			if ( sample.Slope > sampler.MaxTreeSlope )
				return false;

			maxSlope = Math.Max( maxSlope, sample.Slope );
			minSlope = Math.Min( minSlope, sample.Slope );

			if ( !TrySampleSurfaceZ( terrain, px, py, out var z ) )
				return false;

			minZ = Math.Min( minZ, z );
			maxZ = Math.Max( maxZ, z );
		}

		if ( maxSlope - minSlope > config.MaxTreeFootprintSlopeDelta )
			return false;

		if ( maxZ - minZ > config.MaxTreeFootprintHeightDeltaInches )
			return false;

		return true;
	}

	static bool TrySampleSurfaceZ( Terrain terrain, float worldX, float worldY, out float worldZ )
	{
		worldZ = 0f;
		if ( !terrain.IsValid() )
			return false;

		var maxHeight = terrain.TerrainHeight;
		var rayStart = new Vector3( worldX, worldY, maxHeight * 2.5f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( !terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
			return false;

		worldZ = terrain.GameObject.WorldTransform.PointToWorld( localHit ).z;
		return true;
	}
}
