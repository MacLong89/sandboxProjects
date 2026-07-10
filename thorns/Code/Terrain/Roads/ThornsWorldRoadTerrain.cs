using System.Collections.Generic;

namespace Sandbox;

/// <summary>Terrain height + turf influence along <see cref="ThornsWorldRoadCorridor"/> centerlines.</summary>
public static class ThornsWorldRoadTerrain
{
	public readonly struct RoadInfluenceAtPoint
	{
		public float Weight { get; init; }
		public float TargetHeight { get; init; }
		public float DirtTurfWeight { get; init; }
	}

	public static List<ThornsWorldRoadCorridor> CollectAllCorridors(
		ThornsWorldSettlementBlockPlan blockPlan,
		ThornsWorldRoadNetwork roadNetwork )
	{
		var list = new List<ThornsWorldRoadCorridor>( 64 );
		if ( blockPlan is { IsPopulated: true } )
		{
			if ( blockPlan.InterSettlementCorridors is not null )
				list.AddRange( blockPlan.InterSettlementCorridors );

			if ( blockPlan.Areas is not null )
			{
				foreach ( var area in blockPlan.Areas )
				{
					if ( area?.Corridors is not null )
						list.AddRange( area.Corridors );
				}
			}
		}

		if ( list.Count == 0 && roadNetwork?.Segments is not null )
		{
			foreach ( var seg in roadNetwork.Segments )
			{
				list.Add( new ThornsWorldRoadCorridor
				{
					A = seg.FromLocal,
					B = seg.ToLocal,
					HalfWidth = seg.Kind == ThornsWorldTrailKind.DirtRoad ? 96f : 64f,
					Kind = ThornsWorldRoadCorridorKind.Trail
				} );
			}
		}

		return list;
	}

	public static bool PointInFoliageClearance(
		float lx,
		float ly,
		in ThornsTerrainNetSpec spec )
	{
		var corridors = spec.RoadCorridors;
		if ( corridors is null || corridors.Count == 0 )
			return false;

		var tuning = spec.RoadTuning ?? ThornsTerrainRoadTuningNet.EngineDefaults();
		return ThornsWorldSettlementRoadCorridors.PointInCorridor(
			new Vector2( lx, ly ),
			corridors,
			tuning.FoliageClearanceRadius );
	}

	public static bool PointInBoulderClearance(
		float lx,
		float ly,
		in ThornsTerrainNetSpec spec )
	{
		var corridors = spec.RoadCorridors;
		if ( corridors is null || corridors.Count == 0 )
			return false;

		var tuning = spec.RoadTuning ?? ThornsTerrainRoadTuningNet.EngineDefaults();
		return ThornsWorldSettlementRoadCorridors.PointInCorridor(
			new Vector2( lx, ly ),
			corridors,
			tuning.BoulderClearanceRadius );
	}

	public static void ApplyRoadInfluenceToHeightmap(
		in ThornsTerrainNetSpec spec,
		Span<float> heights )
	{
		var corridors = spec.RoadCorridors;
		if ( corridors is null || corridors.Count == 0 )
			return;

		var tuning = spec.RoadTuning ?? ThornsTerrainRoadTuningNet.EngineDefaults();
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var worldW = Math.Max( 64f, spec.WorldWidth );
		var worldD = Math.Max( 64f, spec.WorldDepth );
		var cellX = worldW / (rx - 1f);
		var cellY = worldD / (rz - 1f);
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
				var h0 = heights[i];
				if ( !TrySampleInfluenceAt(
					     heights,
					     spec,
					     new Vector2( wx, wy ),
					     corridors,
					     tuning,
					     out var inf ) )
					continue;

				if ( inf.Weight <= 0.0001f )
					continue;

				var toward = h0 + ( inf.TargetHeight - h0 ) * inf.Weight;
				heights[i] = MathF.Min( h0, toward );
			}
		}
	}

	public static void ApplyRoadTurfToQuads(
		ThornsTerrainNetSpec spec,
		float halfW,
		float halfD,
		float cellX,
		float cellY,
		int rx,
		int rz,
		Span<byte> turfKinds )
	{
		var corridors = spec.RoadCorridors;
		if ( corridors is null || corridors.Count == 0 )
			return;

		var tuning = spec.RoadTuning ?? ThornsTerrainRoadTuningNet.EngineDefaults();
		var qx = rx - 1;
		var qz = rz - 1;
		if ( turfKinds.Length < qx * qz )
			return;

		for ( var z = 0; z < qz; z++ )
		{
			var row = z * qx;
			var qcy = ( z + 0.5f ) * cellY - halfD;
			for ( var x = 0; x < qx; x++ )
			{
				var qcx = ( x + 0.5f ) * cellX - halfW;
				if ( !TrySampleInfluenceAt(
					     ReadOnlySpan<float>.Empty,
					     spec,
					     new Vector2( qcx, qcy ),
					     corridors,
					     tuning,
					     out var inf,
					     heightsOptional: false ) )
					continue;

				if ( inf.DirtTurfWeight >= tuning.DirtMaterialBlendFull )
					turfKinds[row + x] = 0;
				else if ( inf.DirtTurfWeight >= tuning.DirtMaterialBlendStart )
					turfKinds[row + x] = 0;
			}
		}
	}

	public static bool TrySampleInfluenceAt(
		ReadOnlySpan<float> heights,
		in ThornsTerrainNetSpec spec,
		Vector2 planar,
		IReadOnlyList<ThornsWorldRoadCorridor> corridors,
		ThornsTerrainRoadTuningNet tuning,
		out RoadInfluenceAtPoint influence,
		bool heightsOptional = true )
	{
		influence = default;
		if ( corridors is null || corridors.Count == 0 )
			return false;

		var wMax = 0f;
		var targetH = 0f;
		var dirtW = 0f;
		var hasHeight = heights.Length > 0 && heightsOptional;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var worldW = Math.Max( 64f, spec.WorldWidth );
		var worldD = Math.Max( 64f, spec.WorldDepth );

		for ( var c = 0; c < corridors.Count; c++ )
		{
			var corridor = corridors[c];
			var dist = ThornsWorldSettlementRoadCorridors.DistancePointToSegment( planar, corridor.A, corridor.B );
			GetKindTuning( corridor.Kind, tuning, out var flatten, out var falloff, out var widthMul );
			var halfW = Math.Max( 12f, corridor.HalfWidth * widthMul );
			var outer = halfW + Math.Max( 8f, falloff );
			if ( dist > outer )
				continue;

			float w;
			if ( dist <= halfW )
				w = flatten;
			else
			{
				var t = ( dist - halfW ) / MathF.Max( outer - halfW, 1f );
				w = flatten * ( 1f - SmootherStep( t ) );
			}

			if ( w <= wMax )
				continue;

			wMax = w;
			dirtW = MathF.Max( dirtW, ComputeDirtWeight( w, tuning ) );

			if ( hasHeight )
			{
				var closest = ClosestPointOnSegment( planar, corridor.A, corridor.B, out var segT );
				var sampled = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights,
					rx,
					rz,
					worldW,
					worldD,
					spec.CenterOnWorldOrigin,
					closest.x,
					closest.y );

				var hA = ThornsWorldSettlementBlockTerrain.SampleBlockTargetAt( in spec, corridor.A.x, corridor.A.y );
				var hB = ThornsWorldSettlementBlockTerrain.SampleBlockTargetAt( in spec, corridor.B.x, corridor.B.y );
				var blockAlong = sampled;
				if ( !float.IsNaN( hA ) && !float.IsNaN( hB ) )
					blockAlong = hA * (1f - segT) + hB * segT;
				else if ( !float.IsNaN( hA ) )
					blockAlong = hA;
				else if ( !float.IsNaN( hB ) )
					blockAlong = hB;

				targetH = float.IsNaN( blockAlong )
					? sampled
					: sampled * 0.42f + blockAlong * 0.58f;
			}
		}

		if ( wMax <= 0.0001f )
			return false;

		if ( !hasHeight )
			targetH = 0f;

		influence = new RoadInfluenceAtPoint
		{
			Weight = wMax,
			TargetHeight = targetH,
			DirtTurfWeight = dirtW
		};
		return true;
	}

	static float ComputeDirtWeight( float roadWeight, ThornsTerrainRoadTuningNet tuning )
	{
		if ( roadWeight <= tuning.DirtMaterialBlendStart )
			return 0f;
		if ( roadWeight >= tuning.DirtMaterialBlendFull )
			return 1f;
		var t = ( roadWeight - tuning.DirtMaterialBlendStart )
		        / MathF.Max( tuning.DirtMaterialBlendFull - tuning.DirtMaterialBlendStart, 0.001f );
		return SmootherStep( t );
	}

	static void GetKindTuning(
		ThornsWorldRoadCorridorKind kind,
		ThornsTerrainRoadTuningNet tuning,
		out float flatten,
		out float falloff,
		out float widthMul )
	{
		switch ( kind )
		{
			case ThornsWorldRoadCorridorKind.Radial:
			case ThornsWorldRoadCorridorKind.Ring:
				flatten = tuning.CityFlattenStrength;
				falloff = tuning.CityEdgeFalloff;
				widthMul = tuning.CityInfluenceWidthMul;
				break;
			case ThornsWorldRoadCorridorKind.MainStreet:
				flatten = tuning.TownFlattenStrength;
				falloff = tuning.TownEdgeFalloff;
				widthMul = tuning.TownInfluenceWidthMul;
				break;
			default:
				flatten = tuning.TrailFlattenStrength;
				falloff = tuning.TrailEdgeFalloff;
				widthMul = tuning.TrailInfluenceWidthMul;
				break;
		}
	}

	static Vector2 ClosestPointOnSegment( Vector2 p, Vector2 a, Vector2 b ) =>
		ClosestPointOnSegment( p, a, b, out _ );

	static Vector2 ClosestPointOnSegment( Vector2 p, Vector2 a, Vector2 b, out float t )
	{
		var ab = b - a;
		var lenSq = ab.LengthSquared;
		if ( lenSq < 0.0001f )
		{
			t = 0f;
			return a;
		}

		t = Math.Clamp( Vector2.Dot( p - a, ab ) / lenSq, 0f, 1f );
		return a + ab * t;
	}

	static float SmootherStep( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * t * (t * (t * 6f - 15f) + 10f);
	}
}
