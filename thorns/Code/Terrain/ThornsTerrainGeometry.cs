#nullable disable

using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;

namespace Sandbox;

/// <summary>Host-authored parameters replicated to clients so every peer builds identical procedural geometry.</summary>
public sealed class ThornsTerrainNetSpec
{
	/// <summary>Deterministic world layout root (heightfield + static props/interior crates). Dynamic wildlife/supply ignores this for gameplay variance.</summary>
	public int Seed { get; set; } = 1337;

	public int HeightmapResolutionX { get; set; } = 640;

	public int HeightmapResolutionZ { get; set; } = 640;

	public float WorldWidth { get; set; } = 32768f;

	public float WorldDepth { get; set; } = 32768f;

	public float NoiseScale { get; set; } = 0.0000245f;

	public float HeightMultiplier { get; set; } = 820f;

	/// <summary>Fractal noise octaves (&gt;=1): more octaves → rolling hills + smaller surface detail.</summary>
	public int TerrainNoiseOctaves { get; set; } = 6;

	/// <summary>Octave amplitude falloff (&lt; 1): lower = softer small-scale ripples vs large hills.</summary>
	public float TerrainNoisePersistence { get; set; } = 0.4f;

	/// <summary>Frequency multiplier per octave (&gt; 1): lacunarity ≈ 2 gives classic layered hills.</summary>
	public float TerrainNoiseLacunarity { get; set; } = 2.12f;

	/// <summary>&gt; 1 stretches relief around mid height (low points lower, high points higher).</summary>
	public float TerrainHeightContrast { get; set; } = 1.34f;

	/// <summary>Network replica only — runtime uses <see cref="ThornsTerrainRepairPipeline"/>; terraingen forces this off.</summary>
	public int SmoothingPasses { get; set; }

	/// <summary>Network replica only — not applied during <see cref="FillHeightmap"/>.</summary>
	public bool EnableSmoothing { get; set; } = true;

	public string MaterialPath { get; set; } = "terrain_materials/thorns_grass.tmat";

	/// <summary>Chunk-local Z of the sea — used for the optional water sheet, swim plane, and scatter (land vs underwater).</summary>
	public float WaterLevelWorldZ { get; set; } = 88f;

	/// <summary>Sea-level sheet material (<c>materials/water.vmat</c>) — must exist under addon <c>Assets/materials</c>.</summary>
	public string WaterMaterialPath { get; set; } = "materials/water.vmat";

	/// <summary>Minimum UV tiles on the sea-level water sheet; actual repeat also scales with world size.</summary>
	public float WaterSurfaceUvRepeat { get; set; } = 144f;

	/// <summary>When true, a horizontal water mesh is added at <see cref="WaterLevelWorldZ"/> (visual only — no physics).</summary>
	public bool EnableSeaLevelWaterSheet { get; set; } = true;

	/// <summary>When true, terrain spans roughly [-WorldWidth/2,+WorldWidth/2] in X and [-WorldDepth/2,+WorldDepth/2] in Y (horizontal plane XY; Z is up).</summary>
	public bool CenterOnWorldOrigin { get; set; } = true;

	/// <summary>Blend height toward deep water along map edges (seed-independent shell — coastline shelf).</summary>
	public bool EnableCoastalEdgeFalloff { get; set; } = true;

	/// <summary>Only the outer <c>1 −</c> this fraction participates in the shoreline ramp toward <see cref="WaterLevelWorldZ"/>.</summary>
	public float CoastalInteriorLandFraction { get; set; } = 0.58f;

	/// <summary>Target terrain Z near the perimeter: <see cref="WaterLevelWorldZ"/> − this depth (fully transitioned edge).</summary>
	public float CoastalDepthBelowSeaLevelZ { get; set; } = 260f;

	/// <summary>
	/// Host-filled after procedural sites spawn; replicated so all peers carve/flatten the heightfield to foundation height near each building.
	/// </summary>
	public List<ThornsTerrainProcBuildingPad> ProcBuildingTerrainPads { get; set; }

	/// <summary>Replicated road centerlines for terrain flatten, dirt turf, and scatter clearance.</summary>
	public List<ThornsWorldRoadCorridor> RoadCorridors { get; set; }

	/// <summary>Multi-ring settlement influence (noise attenuation + height blend on <see cref="ThornsTerrainGeometry.FillHeightmap"/>).</summary>
	public List<ThornsSettlementTerrainInfluenceNet> SettlementTerrainInfluences { get; set; }

	/// <summary>Terraced block target elevations for localized settlement adaptation.</summary>
	public List<ThornsSettlementBlockTerrainNet> SettlementBlockTerrain { get; set; }

	public ThornsTerrainRoadTuningNet RoadTuning { get; set; }

	/// <summary>When true, use terraingen heightmap + Sandbox.Terrain instead of procedural FBM noise.</summary>
	public bool UseTerraingenWorld { get; set; }

	/// <summary>Deterministic crop/sculpt seed for <see cref="UseTerraingenWorld"/> (defaults to <see cref="Seed"/>).</summary>
	public int TerraingenWorldSeed { get; set; }

	/// <summary>When true with <see cref="UseTerraingenWorld"/>, wood uses foliage2 harvest nodes + terraingen clutter (not decor fluff / non-harvest decorative trees).</summary>
	public bool UseTerraingenFoliage { get; set; }

	/// <summary>Replicated master for grass/mushroom scatter — clients do not read <see cref="ThornsTerrainSystem"/> inspector values.</summary>
	public bool DecorGenerateFoliageFluff { get; set; } = true;

	/// <summary>When true with <see cref="DecorGenerateFoliageFluff"/>, scene gets <see cref="ThornsFoliageDistanceCullSystem"/> for decor proxies.</summary>
	public bool DecorEnableFoliageDistanceCulling { get; set; } = true;

	/// <summary>Scatter placement inset (same semantics as <see cref="ThornsTerrainSystem.ScatterEdgeInsetFraction"/>).</summary>
	public float DecorEdgeInsetFraction { get; set; } = 0.06f;

	public ThornsTerrainDecorGrassNet DecorGrass { get; set; }

	public ThornsTerrainDecorMushroomNet DecorMushroom { get; set; }

	public static string Serialize( ThornsTerrainNetSpec s ) =>
		JsonSerializer.Serialize( s );

	public static ThornsTerrainNetSpec Deserialize( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) )
		{
			var empty = new ThornsTerrainNetSpec();
			ThornsTerrainDecorScatter.EnsureDecorNetDefaults( empty );
			return empty;
		}

		try
		{
			var parsed = JsonSerializer.Deserialize<ThornsTerrainNetSpec>( json );
			var s = parsed is null ? new ThornsTerrainNetSpec() : parsed;
			if ( s.WorldWidth < 32f )
				s.WorldWidth = 32768f;
			if ( s.WorldDepth < 32f )
				s.WorldDepth = 32768f;
			FixLegacyTerrainNoiseFields( s );
			ThornsTerrainDecorScatter.EnsureDecorNetDefaults( s );
			return s;
		}
		catch
		{
			var s = new ThornsTerrainNetSpec();
			ThornsTerrainDecorScatter.EnsureDecorNetDefaults( s );
			return s;
		}
	}

	/// <summary>Older saves omitted fractal knobs — restores sane authoring defaults instead of deserialization zeros.</summary>
	internal static void FixLegacyTerrainNoiseFields( ThornsTerrainNetSpec s )
	{
		if ( s.HeightMultiplier < 10f )
			s.HeightMultiplier = 820f;
		s.TerrainNoiseOctaves = Math.Clamp( s.TerrainNoiseOctaves, 1, 8 );
		if ( s.TerrainNoisePersistence <= 0.01f || s.TerrainNoisePersistence > 0.99f )
			s.TerrainNoisePersistence = 0.4f;
		if ( s.TerrainNoiseLacunarity < 1.05f )
			s.TerrainNoiseLacunarity = 2.12f;
		if ( s.TerrainHeightContrast < 1f || s.TerrainHeightContrast > 2.25f )
			s.TerrainHeightContrast = 1.34f;

		s.CoastalInteriorLandFraction = Math.Clamp( s.CoastalInteriorLandFraction, 0.1f, 0.93f );
		if ( s.CoastalDepthBelowSeaLevelZ < 24f )
			s.CoastalDepthBelowSeaLevelZ = 260f;
		if ( s.CoastalDepthBelowSeaLevelZ > 3800f )
			s.CoastalDepthBelowSeaLevelZ = 3800f;

		s.UseTerraingenWorld = true;
		if ( !s.UseTerraingenFoliage )
			s.UseTerraingenFoliage = true;
	}
}

/// <summary>Chunk-local OBB patch: blends heightmap samples toward <see cref="TargetZ"/> inside the footprint and out to <see cref="Apron"/>.</summary>
public sealed class ThornsTerrainProcBuildingPad
{
	public float CenterX { get; set; }

	public float CenterY { get; set; }

	public float HalfW { get; set; }

	public float HalfD { get; set; }

	public float YawRadians { get; set; }

	/// <summary>Chunk-space Z matching walkable surface / foundation tie-in.</summary>
	public float TargetZ { get; set; }

	/// <summary>Horizontal meters beyond footprint to blend back to natural terrain.</summary>
	public float Apron { get; set; } = 640f;

	/// <summary>Strong wall-adjacent support band (meters outside foundation OBB).</summary>
	public float WallApron { get; set; } = 64f;

	/// <summary>Terrain lowered slightly under interior floor (meters).</summary>
	public float FoundationEmbed { get; set; } = 10f;

	/// <summary>Building footprint half-extents (without wall skirt).</summary>
	public float FoundationHalfW { get; set; }

	public float FoundationHalfD { get; set; }

	/// <summary>Unit vector — outward from primary door in world X/Y.</summary>
	public float DoorOutwardX { get; set; }

	public float DoorOutwardY { get; set; }

	/// <summary>Parent block index for density-aware apron suppression (-1 = none).</summary>
	public int BlockIndex { get; set; } = -1;

	/// <summary>Density scaler for apron influence (0–1, set from block building count).</summary>
	public float ApronStrengthMul { get; set; } = 1f;

	/// <summary>Max blend weight at pad center (macro settlements use ~0.38–0.58).</summary>
	public float PeakBlend { get; set; } = 1f;

	/// <summary>Macro only: inner fraction of <see cref="HalfW"/> held at <see cref="PeakBlend"/> before rim feather.</summary>
	public float MacroFlatCoreRadiusFraction { get; set; }

	public ThornsSettlementTerrainPadKind Kind { get; set; } = ThornsSettlementTerrainPadKind.LocalBuilding;

	/// <summary>When false, pad is ignored during heightmap sculpt (buildings still snap via raycast).</summary>
	public bool SculptHeightmap { get; set; } = true;
}

/// <summary>Heightmap sampling and sculpt helpers for terraingen terrain and world-gen scatter.</summary>
public static class ThornsTerrainGeometry
{
	internal static void GetExtents( in ThornsTerrainNetSpec spec, out float worldW, out float worldD )
	{
		worldW = Math.Max( 64f, spec.WorldWidth );
		worldD = Math.Max( 64f, spec.WorldDepth );
	}

	/// <summary>Layered 2D noise in 0..1 — used by height, turf, and foliage channels (each with its own <see cref="ThornsPerlinNoise2D"/> permutation).</summary>
	public static float SampleFractalNoise01(
		ThornsPerlinNoise2D noise,
		float scaledNx,
		float scaledNy,
		int octaves,
		float persistence,
		float lacunarity )
	{
		octaves = Math.Clamp( octaves, 1, 8 );
		persistence = Math.Clamp( persistence, 0.01f, 0.99f );
		lacunarity = Math.Max( lacunarity, 1.01f );

		var sum = 0f;
		var norm = 0f;
		var amp = 1f;
		var freq = 1f;
		for ( var i = 0; i < octaves; i++ )
		{
			var n = noise.Sample( scaledNx * freq, scaledNy * freq );
			var t = (n + 1f) * 0.5f;
			sum += t * amp;
			norm += amp;
			amp *= persistence;
			freq *= lacunarity;
		}

		return norm > 0.00001f ? sum / norm : 0.5f;
	}

	/// <summary>
	/// Bilinear height on the heightfield grid (local space: XY horizontal plane, +Z up).
	/// </summary>
	public static float SampleHeightLocalZUp(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float worldW,
		float worldD,
		bool centerOnWorldOrigin,
		float localX,
		float localY )
	{
		var cellX = worldW / (rx - 1f);
		var cellY = worldD / (rz - 1f);
		var halfW = centerOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = centerOnWorldOrigin ? worldD * 0.5f : 0f;

		var gx = (localX + halfW) / cellX;
		var gy = (localY + halfD) / cellY;
		gx = Math.Clamp( gx, 0f, rx - 1f );
		gy = Math.Clamp( gy, 0f, rz - 1f );

		var x0 = (int)Math.Floor( gx );
		var y0 = (int)Math.Floor( gy );
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

	/// <summary>
	/// Vertical ray cast downward to find <see cref="ThornsTerrainChunk"/> collision — snaps scatter props to the real mesh.
	/// Penetrates occluders (trees, rocks, crates) until the terrain body is hit.
	/// </summary>
	public static bool TrySnapWorldPositionToTerrainGround(
		Scene scene,
		Vector3 approximateWorldPosition,
		float startLiftZ,
		float segmentLength,
		out Vector3 snappedWorldPosition )
	{
		snappedWorldPosition = approximateWorldPosition;
		if ( scene is null || !scene.IsValid() )
			return false;

		if ( ThornsTerraingenTerrainQueries.TrySampleGroundWorld(
			     scene,
			     approximateWorldPosition.x,
			     approximateWorldPosition.y,
			     0f,
			     out snappedWorldPosition ) )
			return true;

		var rayOrigin = approximateWorldPosition;
		rayOrigin.z += startLiftZ;
		var seg = Math.Clamp( segmentLength, 256f, 65536f );

		for ( var pass = 0; pass < 24; pass++ )
		{
			var tr = ThornsTraceUtility.RunRay( scene, new Ray( rayOrigin, Vector3.Down ), seg, ThornsTraceProfile.TerrainChunkSnapDown, null );

			if ( !tr.Hit || !tr.GameObject.IsValid() )
				return false;

			if ( tr.GameObject.Components.GetInAncestorsOrSelf<ThornsTerrainChunk>( true ).IsValid()
			     || ( tr.GameObject.Tags.Has( "thorns_terrain" )
			          && tr.GameObject.Components.GetInAncestorsOrSelf<Terrain>( true ).IsValid() )
			     || tr.GameObject.Tags.Has( ThornsCollisionTags.FurnitureGalleryFloor ) )
			{
				snappedWorldPosition = tr.HitPosition;
				return true;
			}

			rayOrigin = tr.HitPosition + Vector3.Down * 2f;
		}

		return false;
	}

	/// <summary>Fills <paramref name="heightsOut"/> in row-major order [z * rx + x]; length must be at least rx*rz.</summary>
	public static void FillHeightmap( in ThornsTerrainNetSpec spec, float[] heightsOut ) =>
		FillHeightmap( in spec, heightsOut, 0 );

	public static void FillHeightmap( in ThornsTerrainNetSpec spec, float[] heightsOut, long contentHash )
	{
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var cellCount = rx * rz;
		var heightSpan = heightsOut.AsSpan( 0, cellCount );
		if ( heightsOut.Length >= cellCount
		     && ( ThornsHeightmapBakeCache.TryCopy( in spec, heightSpan )
		          || ( contentHash != 0 && ThornsHeightmapBakeCache.TryCopyByContentHash( contentHash, in spec, heightSpan ) ) ) )
			return;

		var worldSeed = spec.TerraingenWorldSeed != 0 ? spec.TerraingenWorldSeed : spec.Seed;
		ThornsTerraingenTerrainRuntime.TryBindConfigsFromScene( Game.ActiveScene );
		var field = ThornsTerraingenTerrainRuntime.GetOrGenerateField( worldSeed );
		ThornsTerraingenTerrainRuntime.FillHeightmapBase( spec, heightsOut, field );

		ThornsSettlementTerrainInfluence.ApplyToHeightmap( in spec, heightSpan, reconcile: true );

		ThornsWorldRoadTerrain.ApplyRoadInfluenceToHeightmap( in spec, heightSpan );
		ThornsSettlementTerrainReconciliation.SoftenRoadExitBanks( in spec, heightSpan );
		ThornsWorldSettlementBlockTerrain.ApplySurfacesToHeightmap( in spec, heightSpan );
		ThornsTerrainPadSculptDiagnostics.BeginBake( spec.ProcBuildingTerrainPads );
		ApplyProcBuildingTerrainPads( in spec, heightSpan );
		ThornsTerrainHeightRepair.RepairMeshBreakingDiscontinuities( in spec, heightSpan );
		ThornsTerrainPadSculptDiagnostics.LogIfRelevant();

		if ( contentHash != 0 )
			ThornsHeightmapBakeCache.RegisterMeshBakeCopy( contentHash, in spec, heightSpan, cellCount );
	}

	/// <summary>Footprints this steep get building snap only — heightmap sculpt creates vertical cliff slabs.</summary>
	public static bool ShouldSculptHeightmapAtFootprint( in FootprintHeightSampleStats stats ) =>
		stats.CornerDelta <= 14f
		&& stats.CliffSeverity <= 18f
		&& stats.MaxStepSlope <= 16f;

	/// <summary>
	/// Target terrain/floor contact for a per-building flat scrape — mean height on slopes, lowest corner on already-flat sites.
	/// </summary>
	public static float ResolveOrganicBuildingFlatPlotSurfaceZ(
		in FootprintHeightSampleStats stats,
		float centerBaseZ,
		float footprintMinSurfaceZ,
		float floorThickness )
	{
		var centerSurface = centerBaseZ - floorThickness * 0.5f;
		if ( ShouldSculptHeightmapAtFootprint( in stats ) )
			return MathF.Min( centerSurface, footprintMinSurfaceZ );

		return stats.MeanHeight;
	}

	/// <summary>Lowest walkable height under a rotated footprint (dense OBB sample, not center-only).</summary>
	public static float SampleObbMinSurfaceHeight(
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		float centerX,
		float centerY,
		float halfW,
		float halfD,
		float yawRadians )
	{
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		GetExtents( spec, out var worldW, out var worldD );
		var centerOnOrigin = spec.CenterOnWorldOrigin;
		var cellX = worldW / (rx - 1f );
		var cellY = worldD / (rz - 1f );
		var step = MathF.Max( 8f, MathF.Min( cellX, cellY ) * 0.075f );
		var minH = float.MaxValue;
		var cyaw = MathF.Cos( yawRadians );
		var syaw = MathF.Sin( yawRadians );

		for ( var by = -halfD; by <= halfD + 0.01f; by += step )
		for ( var bx = -halfW; bx <= halfW + 0.01f; bx += step )
			minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, bx, by, minH );

		// OBB corners + edge midpoints (catches peaks between grid steps).
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, -halfW, -halfD, minH );
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, halfW, -halfD, minH );
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, -halfW, halfD, minH );
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, halfW, halfD, minH );
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, 0f, -halfD, minH );
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, 0f, halfD, minH );
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, -halfW, 0f, minH );
		minH = ConsiderObbSamplePoint( heights, rx, rz, worldW, worldD, centerOnOrigin, centerX, centerY, cyaw, syaw, halfW, 0f, minH );

		return minH < float.MaxValue * 0.5f ? minH : 0f;
	}

	public readonly struct FootprintHeightSampleStats
	{
		public float CenterHeight { get; init; }
		public float MinHeight { get; init; }
		public float MaxHeight { get; init; }
		public float CornerDelta { get; init; }
		public float MeanHeight { get; init; }
		public float Variance { get; init; }
		public float MaxStepSlope { get; init; }
		public float CliffSeverity { get; init; }
	}

	/// <summary>Dense bilinear footprint sampling for placement validation (finer than heightmap cell size).</summary>
	public static FootprintHeightSampleStats SampleFootprintHeightMetrics(
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		float worldWidth,
		float worldDepth,
		float centerX,
		float centerY,
		float halfW,
		float halfD,
		float yawRadians )
	{
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var centerOnOrigin = spec.CenterOnWorldOrigin;
		var cell = MathF.Min( worldWidth / (rx - 1f), worldDepth / (rz - 1f) );
		var step = MathF.Max( 12f, cell * 0.22f );
		var cyaw = MathF.Cos( yawRadians );
		var syaw = MathF.Sin( yawRadians );

		var sum = 0f;
		var count = 0;
		var minH = float.MaxValue;
		var maxH = float.MinValue;
		var maxStep = 0f;
		var cliffSum = 0f;
		var cliffN = 0;
		float? lastH = null;

		for ( var by = -halfD; by <= halfD + 0.01f; by += step )
		for ( var bx = -halfW; bx <= halfW + 0.01f; bx += step )
		{
			var wx = centerX + bx * cyaw - by * syaw;
			var wy = centerY + bx * syaw + by * cyaw;
			var h = SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin, wx, wy );
			if ( float.IsNaN( h ) || float.IsInfinity( h ) )
				continue;

			sum += h;
			count++;
			minH = MathF.Min( minH, h );
			maxH = MathF.Max( maxH, h );
			if ( lastH.HasValue )
				maxStep = MathF.Max( maxStep, MathF.Abs( h - lastH.Value ) );

			lastH = h;
		}

		for ( var ci = 0; ci < 4; ci++ )
		{
			var bx = ( ci & 1 ) == 0 ? -halfW : halfW;
			var by = ( ci & 2 ) == 0 ? -halfD : halfD;
			var wx = centerX + bx * cyaw - by * syaw;
			var wy = centerY + bx * syaw + by * cyaw;
			var h = SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin, wx, wy );
			if ( float.IsNaN( h ) || float.IsInfinity( h ) )
				continue;

			sum += h;
			count++;
			minH = MathF.Min( minH, h );
			maxH = MathF.Max( maxH, h );
			if ( lastH.HasValue )
				maxStep = MathF.Max( maxStep, MathF.Abs( h - lastH.Value ) );

			lastH = h;
		}

		var centerH = SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin, centerX, centerY );
		if ( count < 2 )
		{
			return new FootprintHeightSampleStats
			{
				CenterHeight = centerH,
				MinHeight = centerH,
				MaxHeight = centerH
			};
		}

		var mean = sum / count;
		var variance = 0f;
		for ( var by = -halfD; by <= halfD + 0.01f; by += step )
		for ( var bx = -halfW; bx <= halfW + 0.01f; bx += step )
		{
			var wx = centerX + bx * cyaw - by * syaw;
			var wy = centerY + bx * syaw + by * cyaw;
			var h = SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin, wx, wy );
			if ( float.IsNaN( h ) )
				continue;

			var d = h - mean;
			variance += d * d;

			var ring = SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin, wx + step, wy );
			if ( !float.IsNaN( ring ) )
			{
				cliffSum += MathF.Abs( ring - h );
				cliffN++;
			}
		}

		variance /= MathF.Max( 1, count - 1 );
		var corners = new[]
		{
			SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin,
				centerX + (-halfW * cyaw - -halfD * syaw), centerY + (-halfW * syaw + -halfD * cyaw) ),
			SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin,
				centerX + (halfW * cyaw - -halfD * syaw), centerY + (halfW * syaw + -halfD * cyaw) ),
			SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin,
				centerX + (-halfW * cyaw - halfD * syaw), centerY + (-halfW * syaw + halfD * cyaw) ),
			SampleHeightLocalZUp( heights, rx, rz, worldWidth, worldDepth, centerOnOrigin,
				centerX + (halfW * cyaw - halfD * syaw), centerY + (halfW * syaw + halfD * cyaw) )
		};
		var cMin = MathF.Min( MathF.Min( corners[0], corners[1] ), MathF.Min( corners[2], corners[3] ) );
		var cMax = MathF.Max( MathF.Max( corners[0], corners[1] ), MathF.Max( corners[2], corners[3] ) );

		return new FootprintHeightSampleStats
		{
			CenterHeight = centerH,
			MinHeight = minH,
			MaxHeight = maxH,
			CornerDelta = cMax - cMin,
			MeanHeight = mean,
			Variance = variance,
			MaxStepSlope = maxStep,
			CliffSeverity = cliffN > 0 ? cliffSum / cliffN : 0f
		};
	}

	static float ConsiderObbSamplePoint(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float worldW,
		float worldD,
		bool centerOnOrigin,
		float centerX,
		float centerY,
		float cyaw,
		float syaw,
		float bx,
		float by,
		float minH )
	{
		var wx = centerX + bx * cyaw - by * syaw;
		var wy = centerY + bx * syaw + by * cyaw;
		var h = SampleHeightLocalZUp( heights, rx, rz, worldW, worldD, centerOnOrigin, wx, wy );
		if ( float.IsNaN( h ) || float.IsInfinity( h ) )
			return minH;

		return MathF.Min( minH, h );
	}

	static bool IsMacroSettlementPad( in ThornsTerrainProcBuildingPad pad ) =>
		pad.Kind == ThornsSettlementTerrainPadKind.MacroSettlement
		|| ( pad.YawRadians == 0f && pad.HalfW == pad.HalfD && pad.HalfW >= 400f && pad.PeakBlend < 0.99f
		     && pad.Kind != ThornsSettlementTerrainPadKind.HubPlateau );

	static bool IsHubPlateauPad( in ThornsTerrainProcBuildingPad pad ) =>
		pad.Kind == ThornsSettlementTerrainPadKind.HubPlateau;

	/// <summary>1 at hub center, 0 at hub radius — rim stays natural terrain before apron.</summary>
	static float HubPlateauRadialWeight( float planar, float radius, float peak )
	{
		if ( radius <= 1f )
			return peak;

		var t = Math.Clamp( planar / radius, 0f, 1f );
		return peak * (1f - SmootherStep( t ));
	}

	static float SmootherStep( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * t * (t * (t * 6f - 15f) + 10f);
	}

	/// <summary>Very gentle macro apron falloff (double smoothstep — reduces cliff-like hub rims).</summary>
	static float MacroSettlementFalloff( float t01 )
	{
		t01 = Math.Clamp( t01, 0f, 1f );
		return SmootherStep( SmootherStep( 1f - t01 ) );
	}

	static bool TryPickLocalPadTarget(
		List<ThornsTerrainProcBuildingPad> pads,
		float wx,
		float wy,
		float hNatural,
		out float targetZ )
	{
		targetZ = 0f;
		var bestArea = float.MaxValue;
		var found = false;

		for ( var p = 0; p < pads.Count; p++ )
		{
			var pad = pads[p];
			if ( !ThornsBuildingFoundationTerrain.IsLocalFoundationPad( in pad ) )
				continue;

			var fw = pad.FoundationHalfW > 1f ? pad.FoundationHalfW : pad.HalfW * 0.82f;
			var fd = pad.FoundationHalfD > 1f ? pad.FoundationHalfD : pad.HalfD * 0.82f;
			var dx = wx - pad.CenterX;
			var dy = wy - pad.CenterY;
			var c = MathF.Cos( -pad.YawRadians );
			var s = MathF.Sin( -pad.YawRadians );
			var bx = dx * c - dy * s;
			var by = dx * s + dy * c;
			if ( MathF.Abs( bx ) > fw || MathF.Abs( by ) > fd )
				continue;

			var area = fw * fd;
			if ( area >= bestArea )
				continue;

			bestArea = area;
			if ( ThornsBuildingFoundationTerrain.TryEvaluate(
				     in pad,
				     wx,
				     wy,
				     hNatural,
				     out var supportZ,
				     out _,
				     out _,
				     out _ ) )
				targetZ = supportZ;
			else
				targetZ = pad.TargetZ - pad.FoundationEmbed;

			found = true;
		}

		return found;
	}

	/// <summary>
	/// Clears macro settlement bowls and per-building feathers from a spec clone used only for minimap terrain shading.
	/// Gameplay heightmaps still use full pads; POI blips mark actual buildings.
	/// </summary>
	public static void StripProcBuildingPadsForMinimapOverview( ThornsTerrainNetSpec spec )
	{
		if ( spec?.ProcBuildingTerrainPads is not { Count: > 0 } pads )
			return;

		pads.Clear();
	}

	/// <summary>
	/// After <see cref="FillHeightmap"/>: lower/raise height samples toward each pad's <see cref="ThornsTerrainProcBuildingPad.TargetZ"/> so collision matches procedural foundations.
	/// </summary>
	public static void ApplyProcBuildingTerrainPads( in ThornsTerrainNetSpec spec, Span<float> heights )
	{
		var pads = spec.ProcBuildingTerrainPads;
		if ( pads is null || pads.Count == 0 )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var cells = rx * rz;
		if ( heights.Length < cells )
			return;

		var naturalHeights = ArrayPool<float>.Shared.Rent( cells );
		heights.Slice( 0, cells ).CopyTo( naturalHeights );

		try
		{
			ApplyProcBuildingTerrainPadsCore( in spec, heights, rx, rz );
			ThornsTerrainPadSculptDiagnostics.AddCellsModified(
				CountPadModifiedCells( naturalHeights.AsSpan( 0, cells ), heights.Slice( 0, cells ) ) );
			ThornsBuildingFoundationTerrain.SoftenHeightmapRimsAfterPads(
				in spec,
				heights,
				naturalHeights.AsSpan( 0, cells ) );
		}
		finally
		{
			ArrayPool<float>.Shared.Return( naturalHeights );
		}
	}

	static int CountPadModifiedCells( ReadOnlySpan<float> before, ReadOnlySpan<float> after )
	{
		var n = 0;
		var len = Math.Min( before.Length, after.Length );
		for ( var i = 0; i < len; i++ )
		{
			if ( MathF.Abs( before[i] - after[i] ) > 0.5f )
				n++;
		}

		return n;
	}

	static void ApplyProcBuildingTerrainPadsCore( in ThornsTerrainNetSpec spec, Span<float> heights, int rx, int rz )
	{
		var pads = spec.ProcBuildingTerrainPads;
		if ( pads is null || pads.Count == 0 )
			return;

		GetExtents( spec, out var worldW, out var worldD );
		var cellX = worldW / (rx - 1f );
		var cellY = worldD / (rz - 1f );
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;

		var padReachSq = new float[pads.Count];
		for ( var pi = 0; pi < pads.Count; pi++ )
		{
			var pad = pads[pi];
			if ( ThornsBuildingFoundationTerrain.IsLocalFoundationPad( in pad ) )
			{
				padReachSq[pi] = 0f;
				continue;
			}

			var apron = Math.Max( 16f, pad.Apron );
			var reach = pad.HalfW + apron + MathF.Max( cellX, cellY );
			padReachSq[pi] = reach * reach;
		}

		for ( var gy = 0; gy < rz; gy++ )
		{
			var wy = gy * cellY - halfD;
			var row = gy * rx;
			for ( var gx = 0; gx < rx; gx++ )
			{
				var wx = gx * cellX - halfW;
				var i = row + gx;
				var h0 = heights[i];
				var wMax = 0f;
				var tz = h0;
				var winningApron = 640f;
				var winningIsMacro = false;
				var winningIsHub = false;

				if ( ThornsBuildingFoundationTerrain.TryApplyCell( pads, in spec, wx, wy, h0, out var foundationH ) )
				{
					heights[i] = foundationH;
					continue;
				}

				for ( var p = 0; p < pads.Count; p++ )
				{
					var pad = pads[p];
					if ( !pad.SculptHeightmap )
						continue;

					var apron = Math.Max( 16f, pad.Apron );
					var dx = wx - pad.CenterX;
					var dy = wy - pad.CenterY;
					if ( padReachSq[p] > 0.01f && ( dx * dx + dy * dy ) > padReachSq[p] )
						continue;

					float w;

					if ( ThornsBuildingFoundationTerrain.IsLocalFoundationPad( in pad ) )
						continue;

					if ( IsHubPlateauPad( in pad ) )
					{
						var radius = pad.HalfW;
						var planar = MathF.Sqrt( dx * dx + dy * dy );
						var peak = Math.Clamp( pad.PeakBlend, 0.05f, 1f );
						if ( planar >= radius + apron )
							continue;

						if ( planar <= radius )
							w = HubPlateauRadialWeight( planar, radius, peak );
						else
						{
							var t = (planar - radius) / MathF.Max( apron, 1f );
							w = peak * (1f - MacroSettlementFalloff( t )) * 0.12f;
						}
					}
					else if ( IsMacroSettlementPad( in pad ) )
					{
						var radius = pad.HalfW;
						var planar = MathF.Sqrt( dx * dx + dy * dy );
						var peak = Math.Clamp( pad.PeakBlend, 0.05f, 1f );
						if ( planar <= radius )
						{
							var t = planar / MathF.Max( radius, 1f );
							w = peak * (1f - SmootherStep( t ) * 0.35f);
						}
						else if ( planar >= radius + apron )
							continue;
						else
						{
							var t = (planar - radius) / MathF.Max( apron, 1f );
							w = peak * MacroSettlementFalloff( t );
						}
					}
					else
						continue;

					if ( w > wMax + 1e-5f || ( w >= wMax - 1e-5f && apron < winningApron ) )
					{
						wMax = w;
						tz = pad.TargetZ;
						winningApron = apron;
						winningIsMacro = IsMacroSettlementPad( in pad );
						winningIsHub = IsHubPlateauPad( in pad );
					}
				}

				if ( wMax <= 0f )
					continue;

				if ( winningIsMacro )
				{
					var blendedMacro = h0 + (tz - h0) * wMax;
					heights[i] = tz <= h0 ? MathF.Min( h0, blendedMacro ) : blendedMacro;
					continue;
				}

				if ( winningIsHub )
				{
					heights[i] = h0 + (tz - h0) * wMax;
					continue;
				}

				var blended = h0 * (1f - wMax) + tz * wMax;
				heights[i] = MathF.Min( h0, blended );
			}
		}
	}
}
