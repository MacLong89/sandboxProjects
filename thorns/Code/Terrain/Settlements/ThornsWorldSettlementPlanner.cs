using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>Macro settlement layout: 12-building city, three 5-building towns, three isolated POIs, dirt-road connectivity.</summary>
public static class ThornsWorldSettlementPlanner
{
	public static ThornsWorldSettlementPlan LastPlan { get; private set; }

	public static ThornsWorldSettlementPlan Plan(
		int seed,
		float minX,
		float maxX,
		float minY,
		float maxY,
		ThornsWorldSettlementConfig config = null,
		ReadOnlySpan<float> heights = default,
		int heightRx = 0,
		int heightRz = 0,
		float worldWidth = 0f,
		float worldDepth = 0f,
		bool centerOnOrigin = true,
		ThornsPerlinNoise2D foliageNoise = null,
		in ThornsTerrainNetSpec spec = default )
	{
		config ??= new ThornsWorldSettlementConfig();
		var rnd = new Random( unchecked( seed ^ (int)0x5a921039 ) );
		var hasHeights = heights.Length > 0 && heightRx > 1 && heightRz > 1;

		var cx = (minX + maxX) * 0.5f;
		var cy = (minY + maxY) * 0.5f;
		var halfW = (maxX - minX) * 0.5f;
		var halfD = (maxY - minY) * 0.5f;
		var mapRadius = MathF.Min( halfW, halfD );

		var cityCenter = PickCityCenter( rnd, cx, cy, mapRadius, config, hasHeights, heights, heightRx, heightRz, worldWidth, worldDepth, centerOnOrigin );

		var cityRadius = mapRadius * config.MainCityRadiusFraction;
		var townRadius = mapRadius * config.TownRadiusFraction;
		var minCityTownSep = mapRadius * config.MinCityTownSeparationFraction;
		var minTownSep = mapRadius * config.MinTownTownSeparationFraction;

		var townCenters = PickTownCenters(
			rnd,
			cityCenter,
			cx,
			cy,
			mapRadius,
			minX,
			maxX,
			minY,
			maxY,
			config.TownOrbitFractionMin,
			config.TownOrbitFractionMax,
			minCityTownSep,
			minTownSep,
			hasHeights,
			heights,
			heightRx,
			heightRz,
			worldWidth,
			worldDepth,
			centerOnOrigin,
			foliageNoise,
			spec );

		var towns = new List<ThornsWorldSettlementZone>( 3 );
		for ( var i = 0; i < townCenters.Count; i++ )
		{
			towns.Add( new ThornsWorldSettlementZone
			{
				Kind = ThornsWorldSettlementKind.Town,
				Label = ThornsWorldSettlementComposition.TownLabel( i ),
				CenterLocal = townCenters[i],
				Radius = townRadius,
				PrimaryDistrict = ThornsProcBuildingDistrict.Mixed,
				SpacingMultiplier = 1.22f,
				BuildingSlots = ThornsWorldSettlementComposition.TownSlots( i )
			} );
		}

		var isolatedSites = ThornsWorldSettlementComposition.PickIsolatedSites( seed );
		var trails = BuildTrails( cityCenter, townCenters, rnd );

		var plan = new ThornsWorldSettlementPlan
		{
			Seed = seed,
			MainCity = new ThornsWorldSettlementZone
			{
				Kind = ThornsWorldSettlementKind.MainCity,
				Label = "Main City",
				CenterLocal = cityCenter,
				Radius = cityRadius,
				PrimaryDistrict = ThornsProcBuildingDistrict.Commercial,
				SpacingMultiplier = 0.68f,
				BuildingSlots = ThornsWorldSettlementComposition.MainCitySlots
			},
			Towns = towns,
			IsolatedSites = isolatedSites,
			Trails = trails,
			IsolatedMinDistanceFromSettlements = mapRadius * config.IsolatedSettlementClearanceFraction
		};

		LastPlan = plan;
		return plan;
	}

	static Vector2 PickCityCenter(
		Random rnd,
		float cx,
		float cy,
		float mapRadius,
		ThornsWorldSettlementConfig config,
		bool hasHeights,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin )
	{
		var jitter = mapRadius * config.CityCenterJitterFraction;
		Vector2? best = null;
		var bestEval = default( ThornsWorldSettlementSiteEvaluation );
		var bestScore = float.MinValue;

		if ( ThornsWorldSettlementSiteAnalysis.CollectDebug )
			ThornsWorldSettlementSiteAnalysis.ClearDebug();

		TryPickCityCandidate(
			rnd,
			cx,
			cy,
			jitter,
			hasHeights,
			heights,
			rx,
			rz,
			ww,
			wd,
			centerOnOrigin,
			128,
			ref best,
			ref bestScore,
			ref bestEval );

		if ( !best.HasValue && hasHeights )
		{
			TryPickCityCandidate(
				rnd,
				cx,
				cy,
				jitter * 2.4f,
				hasHeights,
				heights,
				rx,
				rz,
				ww,
				wd,
				centerOnOrigin,
				96,
				ref best,
				ref bestScore,
				ref bestEval );
		}

		if ( ThornsWorldSettlementSiteAnalysis.CollectDebug && best.HasValue )
			ThornsWorldSettlementSiteAnalysis.FinalizeCityDebug( bestEval );

		if ( !best.HasValue && hasHeights )
			Log.Warning( "[Thorns Settlement] No acceptable city terrain site in search window; using map center fallback." );

		if ( best.HasValue && hasHeights )
			Log.Info(
				$"[Thorns Settlement] City site retainability={bestEval.Retainability:F2} continuity={bestEval.Continuity:F2} horizon={bestEval.HorizonBalance:F2} score={bestScore:F2}" );

		return best ?? new Vector2( cx, cy );
	}

	static void TryPickCityCandidate(
		Random rnd,
		float cx,
		float cy,
		float jitter,
		bool hasHeights,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		int maxAttempts,
		ref Vector2? best,
		ref float bestScore,
		ref ThornsWorldSettlementSiteEvaluation bestEval )
	{
		for ( var attempt = 0; attempt < maxAttempts; attempt++ )
		{
			var pos = new Vector2(
				cx + (float)( rnd.NextDouble() * 2.0 - 1.0 ) * jitter,
				cy + (float)( rnd.NextDouble() * 2.0 - 1.0 ) * jitter );

			var score = 1f;
			var eval = default( ThornsWorldSettlementSiteEvaluation );
			if ( hasHeights )
			{
				eval = ThornsWorldSettlementTerrainScore.ScoreCityCenterDetailed(
					heights, rx, rz, ww, wd, centerOnOrigin, pos.x, pos.y );
				score = eval.Acceptable ? eval.CompositeScore : -1f;
			}

			if ( ThornsWorldSettlementSiteAnalysis.CollectDebug )
				ThornsWorldSettlementSiteAnalysis.RecordCityCandidate( pos.x, pos.y, eval, false );

			if ( score < 0f )
				continue;

			if ( score > bestScore )
			{
				bestScore = score;
				best = pos;
				bestEval = eval;
			}
		}

		if ( best.HasValue && ThornsWorldSettlementSiteAnalysis.CollectDebug )
			ThornsWorldSettlementSiteAnalysis.RecordCityCandidate( best.Value.x, best.Value.y, bestEval, true );
	}

	static List<Vector2> PickTownCenters(
		Random rnd,
		Vector2 cityCenter,
		float mapCx,
		float mapCy,
		float mapRadius,
		float minX,
		float maxX,
		float minY,
		float maxY,
		float orbitMinFrac,
		float orbitMaxFrac,
		float minDistFromCity,
		float minDistTown,
		bool hasHeights,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		ThornsPerlinNoise2D foliageNoise,
		in ThornsTerrainNetSpec spec )
	{
		var towns = new List<Vector2>( 3 );
		var startAngle = (float)( rnd.NextDouble() * Math.PI * 2.0 );

		for ( var i = 0; i < 3; i++ )
		{
			Vector2? best = null;
			var bestScore = float.MinValue;

			for ( var attempt = 0; attempt < 96; attempt++ )
			{
				var angle = startAngle + i * ( MathF.PI * 2f / 3f ) + (float)( rnd.NextDouble() - 0.5 ) * 0.45f;
				var orbit = mapRadius * ( orbitMinFrac + (float)rnd.NextDouble() * ( orbitMaxFrac - orbitMinFrac ) );
				var pos = new Vector2(
					Math.Clamp( mapCx + MathF.Cos( angle ) * orbit, minX, maxX ),
					Math.Clamp( mapCy + MathF.Sin( angle ) * orbit, minY, maxY ) );

				if ( pos.Distance( cityCenter ) < minDistFromCity )
					continue;

				var ok = true;
				var minTownDist = float.MaxValue;
				for ( var t = 0; t < towns.Count; t++ )
				{
					var d = pos.Distance( towns[t] );
					minTownDist = MathF.Min( minTownDist, d );
					if ( d < minDistTown )
					{
						ok = false;
						break;
					}
				}

				if ( !ok )
					continue;

				var score = minTownDist + pos.Distance( cityCenter ) * 0.12f;
				if ( hasHeights && foliageNoise is not null )
				{
					var terrain = ThornsWorldSettlementTerrainScore.ScoreTownCenter(
						heights, rx, rz, ww, wd, centerOnOrigin, pos.x, pos.y, foliageNoise, in spec );
					if ( terrain < 0f )
						continue;

					score += terrain * mapRadius * 0.35f;
				}

				if ( score > bestScore )
				{
					bestScore = score;
					best = pos;
				}
			}

			if ( best.HasValue )
				towns.Add( best.Value );
			else
			{
				var fallbackAngle = startAngle + i * ( MathF.PI * 2f / 3f );
				var orbit = mapRadius * ( orbitMinFrac + orbitMaxFrac ) * 0.5f;
				towns.Add( new Vector2(
					Math.Clamp( mapCx + MathF.Cos( fallbackAngle ) * orbit, minX, maxX ),
					Math.Clamp( mapCy + MathF.Sin( fallbackAngle ) * orbit, minY, maxY ) ) );
			}
		}

		return towns;
	}

	static List<ThornsWorldTrailSegment> BuildTrails( Vector2 city, List<Vector2> towns, Random rnd )
	{
		var trails = new List<ThornsWorldTrailSegment>( 6 );
		for ( var i = 0; i < towns.Count; i++ )
		{
			trails.Add( new ThornsWorldTrailSegment
			{
				FromLocal = city,
				ToLocal = towns[i],
				Kind = ThornsWorldTrailKind.DirtRoad
			} );
		}

		if ( towns.Count >= 3 && rnd.NextDouble() < 0.65 )
		{
			var a = rnd.Next( 0, towns.Count );
			var b = ( a + 1 + rnd.Next( 0, towns.Count - 1 ) ) % towns.Count;
			trails.Add( new ThornsWorldTrailSegment
			{
				FromLocal = towns[a],
				ToLocal = towns[b],
				Kind = ThornsWorldTrailKind.Trail
			} );
		}

		return trails;
	}

	public static void SampleCityBuildingPosition(
		Random rnd,
		ThornsWorldBuildingSlot slot,
		ThornsWorldSettlementZone city,
		float minX,
		float maxX,
		float minY,
		float maxY,
		int attempt,
		out float lx,
		out float ly )
	{
		var ring = slot.CityRing ?? ThornsWorldCityRing.MidRing;
		var cell = ThornsBuildingModule.Cell;
		var (ringLocal, countInRing) = CityRingSlot( slot.Index, ring );
		if ( countInRing <= 0 )
		{
			lx = float.NaN;
			ly = float.NaN;
			return;
		}

		var phase = attempt / 36;
		var phaseAttempt = attempt % 36;
		var ringSlotOffset = phase >= 2 ? ( phase - 1 + phaseAttempt / 12 ) : 0;
		ringLocal = ( ringLocal + ringSlotOffset ) % countInRing;

		var rWant = ring switch
		{
			ThornsWorldCityRing.Core => cell * 4.5f,
			ThornsWorldCityRing.MidRing => cell * 6.2f,
			_ => cell * 5.4f
		};
		var rCap = city.Radius * ( ring switch
		{
			ThornsWorldCityRing.Core => 0.52f,
			ThornsWorldCityRing.MidRing => 0.8f,
			_ => 0.92f
		} );
		var radius = MathF.Min( rWant, MathF.Max( cell * 1.25f, rCap ) );
		if ( phase >= 1 )
			radius = MathF.Min( rCap, radius + cell * ( 0.12f + 0.08f * ( phase - 1 ) ) );
		if ( phase >= 3 )
			radius = radius + ( rCap * 0.95f - radius ) * 0.35f;

		var ang = ringLocal * ( MathF.PI * 2f / countInRing ) - MathF.PI * 0.5f;
		ang += (float)( rnd.NextDouble() - 0.5 ) * 0.35f;
		ang += phaseAttempt * 0.38f;
		ang += attempt * 0.09f;

		var jitterR = (float)( rnd.NextDouble() - 0.5 ) * cell * 0.14f;
		jitterR += phaseAttempt * cell * 0.045f;
		var r = MathF.Max( cell * 1.1f, radius + jitterR );

		lx = city.CenterLocal.x + MathF.Cos( ang ) * r;
		ly = city.CenterLocal.y + MathF.Sin( ang ) * r;

		if ( phase >= 3 )
		{
			var tang = cell * ( 1.2f + phaseAttempt * 0.35f );
			lx += -MathF.Sin( ang ) * tang;
			ly += MathF.Cos( ang ) * tang;
		}

		if ( phase >= 4 )
		{
			var backupAng = (float)( rnd.NextDouble() * Math.PI * 2.0 );
			var backupR = cell * ( 5.8f + phaseAttempt * 0.25f );
			var bx = city.CenterLocal.x + MathF.Cos( backupAng ) * backupR;
			var by = city.CenterLocal.y + MathF.Sin( backupAng ) * backupR;
			lx = lx + ( bx - lx ) * 0.55f;
			ly = ly + ( by - ly ) * 0.55f;
		}

		lx = Math.Clamp( lx, minX, maxX );
		ly = Math.Clamp( ly, minY, maxY );
	}

	/// <summary>Conservative OBB half-extent for overlap checks before layout exists.</summary>
	public static float EstimatedFootprintHalf( ThornsProcBuildingType type )
	{
		ThornsProcBuildingFootprintOverlap.GetTypeMaxHalfExtents( type, out var halfW, out var halfD );
		return MathF.Max( halfW, halfD );
	}

	/// <summary>Max registry footprint half-extents (preview reservation before compile).</summary>
	public static void GetEstimatedFootprintHalfExtents(
		ThornsProcBuildingType type,
		out float halfW,
		out float halfD )
	{
		ThornsProcBuildingFootprintOverlap.GetTypeMaxHalfExtents( type, out halfW, out halfD );
		const float pad = 1.06f;
		halfW *= pad;
		halfD *= pad;
	}

	static (int ringLocal, int countInRing) CityRingSlot( int index, ThornsWorldCityRing ring )
	{
		return ring switch
		{
			ThornsWorldCityRing.Core => ( index - 9, 3 ),
			ThornsWorldCityRing.MidRing => ( index - 4, 5 ),
			_ => ( index, 4 )
		};
	}

	public static void SampleTownBuildingPosition(
		Random rnd,
		ThornsWorldBuildingSlot slot,
		ThornsWorldSettlementZone town,
		float minX,
		float maxX,
		float minY,
		float maxY,
		int attempt,
		out float lx,
		out float ly )
	{
		var count = town.BuildingSlots?.Count ?? ThornsWorldSettlementPlan.BuildingsPerTown;
		var cell = ThornsBuildingModule.Cell;
		var phase = attempt / 24;
		var phaseAttempt = attempt % 24;
		var slotOffset = phase >= 2 ? phaseAttempt / 6 : 0;
		var slotIndex = ( slot.Index + slotOffset ) % Math.Max( 1, count );

		var ang = (float)( rnd.NextDouble() * Math.PI * 2.0 )
		          + slotIndex * ( MathF.PI * 2f / count );
		ang += phaseAttempt * 0.33f;
		ang += attempt * 0.07f;
		ang += (float)( rnd.NextDouble() - 0.5 ) * 0.4f;

		var rad = 220f + (float)rnd.NextDouble() * MathF.Max( 180f, town.Radius * 0.85f );
		rad += phase * cell * 1.8f;
		rad += phaseAttempt * cell * 0.35f;

		lx = town.CenterLocal.x + MathF.Cos( ang ) * rad;
		ly = town.CenterLocal.y + MathF.Sin( ang ) * rad;

		var tang = cell * ( 0.8f + phaseAttempt * 0.4f );
		lx += -MathF.Sin( ang ) * tang;
		ly += MathF.Cos( ang ) * tang;

		if ( phase >= 3 )
		{
			var towardCenter = 0.22f * phase;
			lx = lx + ( town.CenterLocal.x - lx ) * towardCenter;
			ly = ly + ( town.CenterLocal.y - ly ) * towardCenter;
		}

		lx = Math.Clamp( lx, minX, maxX );
		ly = Math.Clamp( ly, minY, maxY );
	}

	public static bool TrySampleIsolatedPosition(
		Random rnd,
		ThornsWorldIsolatedSite site,
		ThornsWorldSettlementPlan plan,
		float minX,
		float maxX,
		float minY,
		float maxY,
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float ww,
		float wd,
		bool centerOnOrigin,
		ThornsPerlinNoise2D foliageNoise,
		in ThornsTerrainNetSpec spec,
		int attempt,
		out float lx,
		out float ly )
	{
		lx = ly = 0f;
		var clearance = plan.IsolatedMinDistanceFromSettlements;

		for ( var tryN = 0; tryN < 4; tryN++ )
		{
			var a = attempt * 0.37f + site.Index * 2.1f + tryN * 1.7f + (float)rnd.NextDouble() * 6.28f;
			var orbit = 0.22f + ( site.Index + tryN ) * 0.11f + (float)rnd.NextDouble() * 0.38f;
			var mapCx = ( minX + maxX ) * 0.5f;
			var mapCy = ( minY + maxY ) * 0.5f;
			var mapR = MathF.Min( maxX - minX, maxY - minY ) * 0.5f * orbit;

			lx = Math.Clamp( mapCx + MathF.Cos( a ) * mapR, minX, maxX );
			ly = Math.Clamp( mapCy + MathF.Sin( a ) * mapR, minY, maxY );

			if ( !IsFarFromSettlements( new Vector2( lx, ly ), plan, clearance ) )
				continue;

			if ( heights.Length > 0 && foliageNoise is not null )
			{
				var score = ThornsWorldSettlementTerrainScore.ScoreIsolated(
					site.Type,
					heights,
					rx,
					rz,
					ww,
					wd,
					centerOnOrigin,
					lx,
					ly,
					foliageNoise,
					in spec );

				if ( attempt < 28 && score < 0.42f && rnd.NextDouble() > 0.18 )
					continue;
			}

			return true;
		}

		return false;
	}

	public static bool IsFarFromSettlements(
		Vector2 pos,
		ThornsWorldSettlementPlan plan,
		float clearance,
		bool includeTownRadii = true )
	{
		if ( pos.Distance( plan.MainCity.CenterLocal ) < plan.MainCity.Radius + clearance )
			return false;

		if ( !includeTownRadii )
			return pos.Distance( plan.MainCity.CenterLocal ) >= clearance;

		for ( var i = 0; i < plan.Towns.Count; i++ )
		{
			var town = plan.Towns[i];
			if ( pos.Distance( town.CenterLocal ) < town.Radius + clearance )
				return false;
		}

		foreach ( var placed in _isolatedPlaced.Values )
		{
			if ( pos.Distance( placed ) < clearance * 0.85f )
				return false;
		}

		return true;
	}

	internal static readonly Dictionary<int, Vector2> _isolatedPlaced = new();

	public static void ClearIsolatedPlacementCache() => _isolatedPlaced.Clear();

	public static void RegisterIsolatedPlaced( int index, Vector2 pos ) => _isolatedPlaced[index] = pos;
}
