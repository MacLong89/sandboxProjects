using System;
using System.Buffers;
using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// Host spawn/respawn placement on dry shoreline: just above sea level, adjacent to water or the map coast shell.
/// </summary>
public static class ThornsPlayerCoastalSpawn
{
	const float MinElevationAboveWaterZ = 10f;
	const float MaxShoreElevationAboveWaterZ = 520f;
	const float NeighborWetHeightMarginZ = 28f;
	const float MapEdgeCoastProximityThreshold = 0.55f;
	const int MaxAttempts = 120;

	static readonly (float Dx, float Dy)[] ShoreProbeDirections =
	{
		(1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f),
		(0.707f, 0.707f), (-0.707f, 0.707f), (0.707f, -0.707f), (-0.707f, -0.707f)
	};

	/// <summary>
	/// Random dry point on procedural terrain near a coast: low elevation above <see cref="ThornsTerrainNetSpec.WaterLevelWorldZ"/>,
	/// with water or shoreline nearby (or map perimeter when coastal edge falloff is enabled).
	/// </summary>
	public static bool TryFindCoastalTerrainSpawnTransform( Scene scene, float pawnFeetLift, out Transform transform )
	{
		transform = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		ThornsTerrainChunk chunk = null;
		foreach ( var c in scene.GetAllComponents<ThornsTerrainChunk>() )
		{
			if ( c is null || !c.IsValid() || !c.HasReplicatedTerrainSpec() )
				continue;
			if ( !c.TryGetResolvedNetSpec( out _ ) )
				continue;
			chunk = c;
			break;
		}

		if ( chunk is null || !chunk.IsValid() || !chunk.TryGetResolvedNetSpec( out var spec ) )
			return false;

		var chunkGo = chunk.GameObject;
		if ( !chunkGo.IsValid() )
			return false;

		var insetFrac = 0.06f;
		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( ts.IsValid() )
			{
				insetFrac = Math.Clamp( ts.ScatterEdgeInsetFraction, 0f, 0.45f );
				break;
			}
		}

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var cells = rx * rz;
		var heights = ArrayPool<float>.Shared.Rent( cells );
		try
		{
			ThornsTerrainGeometry.FillHeightmap( spec, heights );
			var ww = Math.Max( 64f, spec.WorldWidth );
			var wd = Math.Max( 64f, spec.WorldDepth );
			var hw = ww * 0.5f;
			var hd = wd * 0.5f;
			var inset = insetFrac;
			var minX = -hw + ww * inset;
			var maxX = hw - ww * inset;
			var minY = -hd + wd * inset;
			var maxY = hd - wd * inset;
			if ( maxX <= minX || maxY <= minY )
				return false;

			var pads = spec.ProcBuildingTerrainPads;
			var waterZ = spec.WaterLevelWorldZ;
			var shoreProbeRadius = Math.Clamp( Math.Min( ww, wd ) * 0.004f, 128f, 768f );
			var rnd = Random.Shared;
			var upper = ResolveTerrainPeakWorldZRough( scene );
			var startLift = Math.Clamp( upper + 920f, 2400f, 18000f );
			var segment = Math.Clamp( startLift + upper + 2000f, 6000f, 65536f );
			var heightSpan = heights.AsSpan( 0, cells );

			for ( var attempt = 0; attempt < MaxAttempts; attempt++ )
			{
				SampleCoastalBiasedLocalXY( rnd, minX, maxX, minY, maxY, hw, hd, out var lx, out var ly );
				if ( ThornsTerrainDecorScatter.ChunkPointOverlapsAnyProcBuildingFootprintFromPads( lx, ly, pads ) )
					continue;

				var hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heightSpan,
					rx,
					rz,
					ww,
					wd,
					spec.CenterOnWorldOrigin,
					lx,
					ly );
				if ( !IsLowShoreElevation( hz, waterZ ) )
					continue;

				if ( !IsNearCoastline( heightSpan, spec, rx, rz, ww, wd, hw, hd, lx, ly, hz, shoreProbeRadius ) )
					continue;

				var flatLocal = new Vector3( lx, ly, hz );
				var approxWorld = chunkGo.WorldPosition + chunkGo.WorldRotation * flatLocal;
				if ( !ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
					     scene,
					     approxWorld,
					     startLift,
					     segment,
					     out var snapped ) )
					continue;

				if ( !ThornsTerrainSystem.IsWorldTerrainSurfaceDryAccessible( scene, snapped.z ) )
					continue;

				var aboveWater = snapped.z - waterZ;
				if ( aboveWater < MinElevationAboveWaterZ || aboveWater > MaxShoreElevationAboveWaterZ )
					continue;

				transform = new Transform( snapped + Vector3.Up * pawnFeetLift, Rotation.Identity, 1f );
				return true;
			}
		}
		finally
		{
			ArrayPool<float>.Shared.Return( heights );
		}

		return false;
	}

	static void SampleCoastalBiasedLocalXY(
		Random rnd,
		float minX,
		float maxX,
		float minY,
		float maxY,
		float hw,
		float hd,
		out float lx,
		out float ly )
	{
		var cx = (minX + maxX) * 0.5f;
		var cy = (minY + maxY) * 0.5f;
		const float coastBand = 0.38f;
		var radial = 0.35f + (float)rnd.NextDouble() * 0.65f;
		var angle = (float)(rnd.NextDouble() * Math.PI * 2.0 );
		lx = cx + MathF.Cos( angle ) * hw * (1f - coastBand * radial);
		ly = cy + MathF.Sin( angle ) * hd * (1f - coastBand * radial);
		lx = Math.Clamp( lx, minX, maxX );
		ly = Math.Clamp( ly, minY, maxY );
	}

	static bool IsLowShoreElevation( float localHeightZ, float waterLevelWorldZ )
	{
		var above = localHeightZ - waterLevelWorldZ;
		return above >= MinElevationAboveWaterZ && above <= MaxShoreElevationAboveWaterZ;
	}

	static bool IsNearCoastline(
		ReadOnlySpan<float> heights,
		ThornsTerrainNetSpec spec,
		int rx,
		int rz,
		float ww,
		float wd,
		float hw,
		float hd,
		float lx,
		float ly,
		float centerHz,
		float shoreProbeRadius )
	{
		if ( spec.EnableCoastalEdgeFalloff && ComputeMapEdgeProximity01( lx, ly, hw, hd ) >= MapEdgeCoastProximityThreshold )
			return true;

		var waterZ = spec.WaterLevelWorldZ;
		var wetThreshold = waterZ + NeighborWetHeightMarginZ;
		foreach ( var (dx, dy) in ShoreProbeDirections )
		{
			var nh = ThornsTerrainGeometry.SampleHeightLocalZUp(
				heights,
				rx,
				rz,
				ww,
				wd,
				spec.CenterOnWorldOrigin,
				lx + dx * shoreProbeRadius,
				ly + dy * shoreProbeRadius );
			if ( nh <= wetThreshold )
				return true;
		}

		// Steep drop toward water within probe distance (cliffed shoreline).
		if ( centerHz - wetThreshold > 48f )
		{
			var minNeighbor = centerHz;
			foreach ( var (dx, dy) in ShoreProbeDirections )
			{
				var nh = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights,
					rx,
					rz,
					ww,
					wd,
					spec.CenterOnWorldOrigin,
					lx + dx * shoreProbeRadius,
					ly + dy * shoreProbeRadius );
				minNeighbor = MathF.Min( minNeighbor, nh );
			}

			if ( centerHz - minNeighbor >= 72f )
				return true;
		}

		return false;
	}

	static float ResolveTerrainPeakWorldZRough( Scene scene )
	{
		if ( scene is not null && scene.IsValid() )
		{
			var terraPeak = ThornsTerraingenTerrainQueries.ResolvePeakWorldZRough( scene );
			if ( terraPeak > 2000f )
				return terraPeak;
		}

		var peak = 1600f;
		if ( scene is null || !scene.IsValid() )
			return peak;
		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() )
				continue;
			var cfg = ts.TerraingenConfig ?? new ThornsTerrainConfig();
			var hm = MathF.Abs( cfg.MaxTerrainHeightInches );
			peak = MathF.Max( peak, hm + ts.WaterLevelWorldZ );
		}

		return peak + 380f;
	}

	/// <summary>0 at map center, 1 on the perimeter (axis-aligned play bounds).</summary>
	static float ComputeMapEdgeProximity01( float localX, float localY, float halfWidth, float halfDepth )
	{
		if ( halfWidth < 1f || halfDepth < 1f )
			return 0f;

		var nx = 1f - Math.Abs( localX ) / halfWidth;
		var ny = 1f - Math.Abs( localY ) / halfDepth;
		var interior = Math.Clamp( Math.Min( nx, ny ), 0f, 1f );
		return 1f - interior;
	}
}
