using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>Horizontal map extents for minimap / POI replica (prefers full procedural terrain size).</summary>
public static class ThornsPoiMapBounds
{
	public static bool TryGetTerrainPlayableBounds(
		Scene scene,
		out float minX,
		out float maxX,
		out float minY,
		out float maxY )
	{
		minX = maxX = minY = maxY = 0f;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var chunk in scene.GetAllComponents<ThornsTerrainChunk>() )
		{
			if ( !chunk.IsValid() || !chunk.TryGetResolvedNetSpec( out var spec ) )
				continue;

			ComputeInsetBounds(
				spec.WorldWidth,
				spec.WorldDepth,
				spec.CenterOnWorldOrigin,
				spec.DecorEdgeInsetFraction,
				out minX,
				out maxX,
				out minY,
				out maxY );
			return true;
		}

		foreach ( var terrain in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !terrain.IsValid() )
				continue;

			var cfg = terrain.TerraingenConfig ?? new ThornsTerrainConfig();
			var worldSize = ThornsTerraingenTerrainRuntime.ComputeTerrainWorldSize( cfg );
			ComputeInsetBounds(
				worldSize,
				worldSize,
				terrain.CenterTerrainOnWorldOrigin,
				terrain.ScatterEdgeInsetFraction,
				out minX,
				out maxX,
				out minY,
				out maxY );
			return true;
		}

		return false;
	}

	public static void ComputeInsetBounds(
		float worldWidth,
		float worldDepth,
		bool centerOnOrigin,
		float edgeInsetFraction,
		out float minX,
		out float maxX,
		out float minY,
		out float maxY )
	{
		var ww = Math.Max( 64f, worldWidth );
		var wd = Math.Max( 64f, worldDepth );
		var inset = Math.Clamp( edgeInsetFraction, 0f, 0.45f );

		if ( centerOnOrigin )
		{
			var hw = ww * 0.5f;
			var hd = wd * 0.5f;
			minX = -hw + ww * inset;
			maxX = hw - ww * inset;
			minY = -hd + wd * inset;
			maxY = hd - wd * inset;
			return;
		}

		minX = ww * inset;
		maxX = ww * ( 1f - inset );
		minY = wd * inset;
		maxY = wd * ( 1f - inset );
	}
}
