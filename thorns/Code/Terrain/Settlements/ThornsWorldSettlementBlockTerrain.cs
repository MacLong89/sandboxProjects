using System.Collections.Generic;

namespace Sandbox;

/// <summary>Block-level terraced targets: local sampling, road hints, neighbor continuity.</summary>
public static class ThornsWorldSettlementBlockTerrain
{
	public const float MaxCityBlockDelta = 22f;
	public const float MaxTownBlockDelta = 28f;
	public const float RoadBlendWeight = 0.52f;
	public const float MacroHubBlendWeight = 0.18f;
	public const float DenseBlockSurfaceStrength = 0.36f;
	public const float SparseBlockSurfaceStrength = 0.34f;
	public const float BlockExteriorEdgeFalloff = 0.42f;
	public const float CorridorSurfaceDampMin = 0.38f;

	public static IReadOnlyList<ThornsWorldSettlementBlockTerrainDebug> LastDebug { get; private set; } =
		Array.Empty<ThornsWorldSettlementBlockTerrainDebug>();

	public static void ComputeAndRegister(
		ThornsWorldSettlementBlockPlan blockPlan,
		ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float worldWidth,
		float worldDepth,
		bool centerOnOrigin,
		bool collectDebug = false )
	{
		if ( blockPlan is null || !blockPlan.IsPopulated || spec is null || heights.IsEmpty )
			return;

		spec.SettlementBlockTerrain ??= new List<ThornsSettlementBlockTerrainNet>();
		spec.SettlementBlockTerrain.Clear();

		var debug = collectDebug ? new List<ThornsWorldSettlementBlockTerrainDebug>( 64 ) : null;

		if ( blockPlan.Areas is not null )
		{
			for ( var a = 0; a < blockPlan.Areas.Count; a++ )
				ComputeArea( blockPlan.Areas[a], spec, heights, rx, rz, worldWidth, worldDepth, centerOnOrigin, debug );
		}

		LastDebug = debug is not null
			? debug
			: Array.Empty<ThornsWorldSettlementBlockTerrainDebug>();
		ThornsSettlementTerrainInfluence.SyncHubTargetsFromBlocks( spec );
	}

	static void ComputeArea(
		ThornsWorldSettlementAreaBlockPlan area,
		ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		List<ThornsWorldSettlementBlockTerrainDebug> debug )
	{
		var blocks = CollectBlocks( area );
		if ( blocks.Count == 0 )
			return;

		var isCity = area.SettlementKind == ThornsWorldSettlementKind.MainCity;
		var maxDelta = isCity ? MaxCityBlockDelta : MaxTownBlockDelta;

		for ( var i = 0; i < blocks.Count; i++ )
		{
			var block = blocks[i];
			var raw = SampleBlockRawTarget(
				heights,
				rx,
				rz,
				spec,
				ww,
				wd,
				centerOnOrigin,
				block,
				area.Corridors );
			block.TargetSurfaceZ = raw;
		}

		ApplyTerraceConstraints( blocks, maxDelta, 4 );
		PropagateToLots( blocks );

		for ( var i = 0; i < blocks.Count; i++ )
		{
			var block = blocks[i];
			block.BuildingCount = Math.Max( 1, CountBlockLots( block ) );
			spec.SettlementBlockTerrain.Add( new ThornsSettlementBlockTerrainNet
			{
				CenterX = block.CenterLocal.x,
				CenterY = block.CenterLocal.y,
				HalfW = block.HalfW,
				HalfD = block.HalfD,
				YawRadians = block.YawRadians,
				TargetZ = block.TargetSurfaceZ,
				Kind = area.SettlementKind,
				BlockIndex = block.Index,
				BuildingCount = block.BuildingCount,
				SurfaceStrength = ComputeSurfaceStrength( block.BuildingCount )
			} );

			if ( debug is not null && debug.Count < 80 )
			{
				debug.Add( new ThornsWorldSettlementBlockTerrainDebug
				{
					CenterLocal = block.CenterLocal,
					TargetZ = block.TargetSurfaceZ,
					HalfW = block.HalfW,
					HalfD = block.HalfD,
					Kind = area.SettlementKind
				} );
			}
		}
	}

	public static float ComputeSurfaceStrength( int buildingCount )
	{
		var baseStrength = buildingCount >= 3
			? DenseBlockSurfaceStrength
			: buildingCount >= 2
				? DenseBlockSurfaceStrength * 0.92f
				: SparseBlockSurfaceStrength;
		return ThornsSettlementDensityRestraint.ComputeBlockSurfaceStrength( buildingCount, baseStrength );
	}

	static int CountBlockLots( ThornsWorldSettlementBlock block )
	{
		if ( block.Lots is null || block.Lots.Count == 0 )
			return 1;

		var count = 0;
		for ( var i = 0; i < block.Lots.Count; i++ )
		{
			var lot = block.Lots[i];
			if ( lot.AssignedType.HasValue || lot.PreferredSlotIndex >= 0 )
				count++;
		}

		return Math.Max( 1, count );
	}

	/// <summary>Shared terraced surface inside each block — run before per-building aprons.</summary>
	public static void ApplySurfacesToHeightmap( in ThornsTerrainNetSpec spec, Span<float> heights )
	{
		var blocks = spec.SettlementBlockTerrain;
		if ( blocks is null || blocks.Count == 0 || heights.IsEmpty )
			return;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		if ( heights.Length < rx * rz )
			return;

		var worldW = Math.Max( 64f, spec.WorldWidth );
		var worldD = Math.Max( 64f, spec.WorldDepth );
		var cellX = worldW / (rx - 1f );
		var cellY = worldD / (rz - 1f );
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;

		for ( var gy = 0; gy < rz; gy++ )
		{
			var wy = gy * cellY - halfD;
			var row = gy * rx;
			for ( var gx = 0; gx < rx; gx++ )
			{
				var wx = gx * cellX - halfW;
				var i = row + gx;
				var h = heights[i];
				if ( !TrySampleBlockSurfaceBlend( blocks, wx, wy, out var targetZ, out var weight ) )
					continue;

				weight *= SampleCorridorTraversalDamp( spec.RoadCorridors, wx, wy );
				if ( weight <= 0.01f )
					continue;

				heights[i] = h + (targetZ - h) * weight;
			}
		}
	}

	public static bool TrySampleBlockSurfaceBlend(
		List<ThornsSettlementBlockTerrainNet> blocks,
		float wx,
		float wy,
		out float targetZ,
		out float blendWeight )
	{
		targetZ = 0f;
		blendWeight = 0f;
		var bestW = 0f;

		for ( var b = 0; b < blocks.Count; b++ )
		{
			var block = blocks[b];
			if ( !TryBlockObbWeight( block, wx, wy, out var obbW, out var edgeT ) )
				continue;

			var strength = block.SurfaceStrength > 0.01f
				? block.SurfaceStrength
				: ComputeSurfaceStrength( block.BuildingCount );
			var w = obbW * strength * (1f - edgeT * BlockExteriorEdgeFalloff);
			if ( w <= bestW )
				continue;

			bestW = w;
			targetZ = block.TargetZ;
			blendWeight = w;
		}

		return blendWeight > 0.01f;
	}

	public static bool TryGetBlockByIndex(
		List<ThornsSettlementBlockTerrainNet> blocks,
		int blockIndex,
		out ThornsSettlementBlockTerrainNet block )
	{
		for ( var i = 0; i < blocks.Count; i++ )
		{
			if ( blocks[i].BlockIndex == blockIndex )
			{
				block = blocks[i];
				return true;
			}
		}

		block = null;
		return false;
	}

	static bool TryBlockObbWeight(
		ThornsSettlementBlockTerrainNet block,
		float wx,
		float wy,
		out float obbWeight,
		out float edgeT )
	{
		obbWeight = 0f;
		edgeT = 1f;
		var dx = wx - block.CenterX;
		var dy = wy - block.CenterY;
		var c = MathF.Cos( -block.YawRadians );
		var s = MathF.Sin( -block.YawRadians );
		var bx = dx * c - dy * s;
		var by = dx * s + dy * c;
		var nx = MathF.Abs( bx ) / MathF.Max( block.HalfW, 1f );
		var ny = MathF.Abs( by ) / MathF.Max( block.HalfD, 1f );
		var maxN = MathF.Max( nx, ny );
		if ( maxN > 1.12f )
			return false;

		var denseInterior = block.BuildingCount >= 2;
		if ( denseInterior )
		{
			edgeT = Math.Clamp( (maxN - 0.72f) / 0.28f, 0f, 1f );
			obbWeight = maxN <= 0.9f ? 1f : 1f - SmootherStep( (maxN - 0.9f) / 0.22f );
		}
		else
		{
			edgeT = Math.Clamp( (maxN - 0.55f) / 0.55f, 0f, 1f );
			obbWeight = 1f - SmootherStep( maxN * 0.72f );
		}

		return obbWeight > 0.02f;
	}

	static float SampleCorridorTraversalDamp(
		IReadOnlyList<ThornsWorldRoadCorridor> corridors,
		float wx,
		float wy )
	{
		if ( corridors is null || corridors.Count == 0 )
			return 1f;

		var p = new Vector2( wx, wy );
		var damp = 1f;
		for ( var c = 0; c < corridors.Count; c++ )
		{
			var corridor = corridors[c];
			var dist = ThornsWorldSettlementRoadCorridors.DistancePointToSegment( p, corridor.A, corridor.B );
			var inner = corridor.HalfWidth * 0.92f;
			if ( dist <= inner )
				return CorridorSurfaceDampMin;

			var blend = inner + MathF.Max( 24f, corridor.HalfWidth * 0.35f );
			if ( dist < blend )
			{
				var t = (dist - inner) / MathF.Max( blend - inner, 1f );
				damp = MathF.Min( damp, CorridorSurfaceDampMin + (1f - CorridorSurfaceDampMin) * SmootherStep( t ) );
			}
		}

		return damp;
	}

	static float SmootherStep( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	static List<ThornsWorldSettlementBlock> CollectBlocks( ThornsWorldSettlementAreaBlockPlan area )
	{
		var list = new List<ThornsWorldSettlementBlock>( 24 );
		if ( area?.Districts is null )
			return list;

		for ( var d = 0; d < area.Districts.Count; d++ )
		{
			var district = area.Districts[d];
			if ( district?.Blocks is null )
				continue;

			for ( var b = 0; b < district.Blocks.Count; b++ )
				list.Add( district.Blocks[b] );
		}

		return list;
	}

	static float SampleBlockRawTarget(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		in ThornsTerrainNetSpec spec,
		float ww,
		float wd,
		bool centerOnOrigin,
		ThornsWorldSettlementBlock block,
		IReadOnlyList<ThornsWorldRoadCorridor> corridors )
	{
		var cx = block.CenterLocal.x;
		var cy = block.CenterLocal.y;
		var terrainZ = ThornsTerrainGeometry.SampleObbMinSurfaceHeight(
			heights,
			in spec,
			cx,
			cy,
			block.HalfW * 0.55f,
			block.HalfD * 0.55f,
			block.YawRadians );

		var roadZ = SampleRoadHeightNear( heights, rx, rz, spec, ww, wd, centerOnOrigin, cx, cy, corridors );
		var z = terrainZ;
		if ( !float.IsNaN( roadZ ) )
			z = terrainZ * (1f - RoadBlendWeight) + roadZ * RoadBlendWeight;

		return z;
	}

	static float SampleRoadHeightNear(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		in ThornsTerrainNetSpec spec,
		float ww,
		float wd,
		bool centerOnOrigin,
		float lx,
		float ly,
		IReadOnlyList<ThornsWorldRoadCorridor> corridors )
	{
		if ( corridors is null || corridors.Count == 0 )
			return float.NaN;

		var best = float.MaxValue;
		var sum = 0f;
		var count = 0;
		for ( var c = 0; c < corridors.Count; c++ )
		{
			var corridor = corridors[c];
			var dist = ThornsWorldSettlementRoadCorridors.DistancePointToSegment(
				new Vector2( lx, ly ),
				corridor.A,
				corridor.B );
			if ( dist > corridor.HalfWidth * 2.5f )
				continue;

			var closest = ClosestPointOnSegment( new Vector2( lx, ly ), corridor.A, corridor.B );
			var h = ThornsTerrainGeometry.SampleHeightLocalZUp(
				heights,
				rx,
				rz,
				ww,
				wd,
				centerOnOrigin,
				closest.x,
				closest.y );
			if ( float.IsNaN( h ) )
				continue;

			sum += h;
			count++;
			best = MathF.Min( best, dist );
		}

		return count > 0 ? sum / count : float.NaN;
	}

	static Vector2 ClosestPointOnSegment( Vector2 p, Vector2 a, Vector2 b )
	{
		var ab = b - a;
		var len2 = ab.LengthSquared;
		if ( len2 < 1e-6f )
			return a;

		var t = Math.Clamp( Vector2.Dot( p - a, ab ) / len2, 0f, 1f );
		return a + ab * t;
	}

	static void ApplyTerraceConstraints( List<ThornsWorldSettlementBlock> blocks, float maxDelta, int passes )
	{
		for ( var pass = 0; pass < passes; pass++ )
		{
			for ( var i = 0; i < blocks.Count; i++ )
			{
				var a = blocks[i];
				var avg = a.TargetSurfaceZ;
				var n = 1;
				for ( var j = 0; j < blocks.Count; j++ )
				{
					if ( i == j )
						continue;

					var b = blocks[j];
					var dx = a.CenterLocal.x - b.CenterLocal.x;
					var dy = a.CenterLocal.y - b.CenterLocal.y;
					var edge = MathF.Max( a.HalfW + b.HalfW, a.HalfD + b.HalfD ) * 1.15f;
					if ( dx * dx + dy * dy > edge * edge )
						continue;

					avg += b.TargetSurfaceZ;
					n++;
				}

				avg /= n;
				var d = avg - a.TargetSurfaceZ;
				if ( d > maxDelta )
					a.TargetSurfaceZ += maxDelta;
				else if ( d < -maxDelta )
					a.TargetSurfaceZ -= maxDelta;
				else
					a.TargetSurfaceZ = a.TargetSurfaceZ + (avg - a.TargetSurfaceZ) * 0.35f;
			}
		}
	}

	static void PropagateToLots( List<ThornsWorldSettlementBlock> blocks )
	{
		for ( var i = 0; i < blocks.Count; i++ )
		{
			var block = blocks[i];
			if ( block.Lots is null )
				continue;

			for ( var l = 0; l < block.Lots.Count; l++ )
				block.Lots[l].TargetSurfaceZ = block.TargetSurfaceZ;
		}
	}

	public static bool TryFindBlock(
		ThornsWorldSettlementAreaBlockPlan area,
		int blockIndex,
		out ThornsWorldSettlementBlock block )
	{
		block = null;
		if ( area?.Districts is null )
			return false;

		for ( var d = 0; d < area.Districts.Count; d++ )
		{
			var district = area.Districts[d];
			if ( district?.Blocks is null )
				continue;

			for ( var b = 0; b < district.Blocks.Count; b++ )
			{
				if ( district.Blocks[b].Index == blockIndex )
				{
					block = district.Blocks[b];
					return true;
				}
			}
		}

		return false;
	}

	public static float ResolvePlacementSurfaceZ(
		ThornsWorldSettlementBlockPlan blockPlan,
		ThornsWorldSettlementKind kind,
		int townIndex,
		float lx,
		float ly,
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		ThornsWorldSettlementLot lot = null )
	{
		if ( blockPlan is { IsPopulated: true } )
		{
			ThornsWorldSettlementAreaBlockPlan area = kind switch
			{
				ThornsWorldSettlementKind.MainCity => blockPlan.MainCity,
				ThornsWorldSettlementKind.Town => blockPlan.Town( townIndex ),
				_ => null
			};

			if ( lot is not null && area is not null
			     && TryFindBlock( area, lot.BlockIndex, out var block ) )
				return block.TargetSurfaceZ;

			if ( area is not null && TrySampleNearestBlockTarget( area, lx, ly, out var nearZ ) )
				return nearZ;
		}

		return ThornsTerrainGeometry.SampleObbMinSurfaceHeight(
			heights,
			in spec,
			lx,
			ly,
			48f,
			48f,
			0f );
	}

	static bool TrySampleNearestBlockTarget(
		ThornsWorldSettlementAreaBlockPlan area,
		float lx,
		float ly,
		out float targetZ )
	{
		targetZ = 0f;
		var blocks = CollectBlocks( area );
		var bestD2 = float.MaxValue;
		var found = false;
		for ( var i = 0; i < blocks.Count; i++ )
		{
			var block = blocks[i];
			var dx = lx - block.CenterLocal.x;
			var dy = ly - block.CenterLocal.y;
			var d2 = dx * dx + dy * dy;
			if ( d2 >= bestD2 )
				continue;

			bestD2 = d2;
			targetZ = block.TargetSurfaceZ;
			found = true;
		}

		return found;
	}

	public static void RefineFromPlacements(
		ThornsTerrainNetSpec spec,
		ThornsWorldSettlementAreaBlockPlan area,
		IReadOnlyList<ThornsWorldGenProcBuildingFootprint> footprints )
	{
		if ( area is null || footprints is null || spec?.SettlementBlockTerrain is null )
			return;

		var blocks = CollectBlocks( area );
		for ( var i = 0; i < blocks.Count; i++ )
		{
			var block = blocks[i];
			var samples = new List<float>( 8 );
			for ( var f = 0; f < footprints.Count; f++ )
			{
				var fp = footprints[f];
				if ( float.IsNaN( fp.FloorSurfaceZ ) )
					continue;

				var dx = fp.CenterX - block.CenterLocal.x;
				var dy = fp.CenterY - block.CenterLocal.y;
				if ( dx * dx + dy * dy > (block.HalfW * block.HalfW + block.HalfD * block.HalfD) )
					continue;

				samples.Add( fp.FloorSurfaceZ );
			}

			if ( samples.Count == 0 )
				continue;

			samples.Sort();
			block.TargetSurfaceZ = samples[samples.Count / 2];
			for ( var l = 0; l < block.Lots.Count; l++ )
				block.Lots[l].TargetSurfaceZ = block.TargetSurfaceZ;
		}

		SyncSpecFromBlocks( spec, area );
	}

	static void SyncSpecFromBlocks( ThornsTerrainNetSpec spec, ThornsWorldSettlementAreaBlockPlan area )
	{
		var blocks = CollectBlocks( area );
		for ( var i = 0; i < blocks.Count; i++ )
		{
			var block = blocks[i];
			for ( var n = 0; n < spec.SettlementBlockTerrain.Count; n++ )
			{
				var net = spec.SettlementBlockTerrain[n];
				if ( MathF.Abs( net.CenterX - block.CenterLocal.x ) > 4f
				     || MathF.Abs( net.CenterY - block.CenterLocal.y ) > 4f )
					continue;

				net.TargetZ = block.TargetSurfaceZ;
				net.BuildingCount = block.BuildingCount;
				net.SurfaceStrength = ComputeSurfaceStrength( block.BuildingCount );
				break;
			}
		}
	}

	public static float SampleBlockTargetAt(
		in ThornsTerrainNetSpec spec,
		float wx,
		float wy )
	{
		var blocks = spec.SettlementBlockTerrain;
		if ( blocks is null || blocks.Count == 0 )
			return float.NaN;

		var bestW = 0f;
		var tz = 0f;
		for ( var i = 0; i < blocks.Count; i++ )
		{
			var b = blocks[i];
			var dx = wx - b.CenterX;
			var dy = wy - b.CenterY;
			var c = MathF.Cos( -b.YawRadians );
			var s = MathF.Sin( -b.YawRadians );
			var bx = dx * c - dy * s;
			var by = dx * s + dy * c;
			var ox = MathF.Max( MathF.Abs( bx ) - b.HalfW, 0f );
			var oy = MathF.Max( MathF.Abs( by ) - b.HalfD, 0f );
			var dist = MathF.Sqrt( ox * ox + oy * oy );
			var falloff = MathF.Max( b.HalfW, b.HalfD ) * 1.35f;
			if ( dist > falloff )
				continue;

			var w = 1f - SmootherStep( Math.Clamp( dist / MathF.Max( falloff, 1f ), 0f, 1f ) );
			if ( w <= bestW )
				continue;

			bestW = w;
			tz = b.TargetZ;
		}

		return bestW > 0.02f ? tz : float.NaN;
	}
}

public readonly struct ThornsWorldSettlementBlockTerrainDebug
{
	public Vector2 CenterLocal { get; init; }
	public float TargetZ { get; init; }
	public float HalfW { get; init; }
	public float HalfD { get; init; }
	public ThornsWorldSettlementKind Kind { get; init; }
}
