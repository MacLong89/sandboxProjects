using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Map-wide organic placement: random scatter with cluster bias, full OBB separation, terrain validation.
/// </summary>
public static class ThornsWorldGenOrganicClusterPlacer
{
	const int MaxAttemptsPerBuilding = 200;
	const int MaxAttemptsLandmark = 320;
	const float SlopeMax = ThornsWorldGenFootprintReservation.DefaultSlopeMax;
	const int NewClusterEveryNBuildings = 7;
	/// <summary>Hard cap on buildings placed near each other (prevents one map-wide super-city).</summary>
	const int MaxBuildingsPerOrganicCluster = 20;
	static readonly ThornsProcBuildingType[] OrganicLandmarkPool =
	[
		ThornsProcBuildingType.Skyscraper,
		ThornsProcBuildingType.OfficeBuilding,
		ThornsProcBuildingType.ApartmentTower
	];

	public static void PlaceAll( ThornsWorldGenerationContext ctx, ThornsWorldGenerationHostBridge host )
	{
		ThornsProcBuildingTypeDebugColors.UseTypeColors = host.DebugBuildingTypeColors;
		ThornsWorldSettlementPlacementDiagnostics.Reset();
		if ( host.DrawSettlementLayoutDebug )
			ThornsWorldSettlementPlacementDebugViz.Clear();

		var reservation = ctx.FootprintReservation;
		var layouts = ctx.LayoutFactory;
		var rnd = ctx.PlacementRng;
		var districtPlanner = ctx.DistrictPlanner;
		var chunkRot = host.ChunkRoot.WorldRotation;
		var scene = host.Scene;
		var floorT = ThornsBuildingModule.FloorThickness;
		var cell = ThornsBuildingModule.Cell;
		var targetCount = Math.Max( 1, host.OrganicBuildingCount );
		var clusterBias = Math.Clamp( host.OrganicClusterBias, 0f, 1f );
		var edgeGap = cell * Math.Max( 0.8f, host.OrganicBuildingBufferCells );
		var placedFootprints = host.BuildingFootprints;
		var skipRoads = ThornsWorldSettlementRoadCorridors.SkipPlacementCorridorCheck;
		ctx.Spec.ProcBuildingTerrainPads ??= new List<ThornsTerrainProcBuildingPad>();
		var terrainPadsAdded = 0;
		var terrainPadsSculpt = 0;

		districtPlanner.BeginCluster();
		var buildingsInSpatialCluster = 0;
		var buildingsSinceDistrictRoll = 0;
		var clusterFootprints = new List<ThornsWorldGenProcBuildingFootprint>( MaxBuildingsPerOrganicCluster );
		var placed = 0;
		var failed = 0;
		var layoutFailures = 0;
		var spawnFailures = 0;
		var landmarksPlaced = 0;
		var landmarkQuota = Math.Clamp( targetCount / 7, 3, 8 );
		var maxPlacementPasses = Math.Max( targetCount * 3, targetCount + 12 );
		var pass = 0;

		while ( placed < targetCount && pass < maxPlacementPasses )
		{
			pass++;
			if ( buildingsInSpatialCluster >= MaxBuildingsPerOrganicCluster )
			{
				districtPlanner.BeginCluster();
				buildingsInSpatialCluster = 0;
				buildingsSinceDistrictRoll = 0;
				clusterFootprints.Clear();
			}
			else if ( buildingsSinceDistrictRoll >= NewClusterEveryNBuildings )
			{
				districtPlanner.BeginCluster();
				buildingsSinceDistrictRoll = 0;
			}

			var forceLandmark = landmarksPlaced < landmarkQuota && placed >= 2 && ( pass % 6 == 2 || pass % 6 == 5 );
			var buildingType = forceLandmark
				? OrganicLandmarkPool[rnd.Next( OrganicLandmarkPool.Length )]
				: districtPlanner.PickBuildingTypeForOrganic( isolatedSite: false );
			var isLandmark = ThornsProcBuildingIdentityRegistry.IsVerticalLandmark( buildingType );
			var spacingMul = districtPlanner.SpacingMultiplierFor( buildingType );
			var gap = edgeGap * ( 0.55f + spacingMul * 0.12f );
			if ( isLandmark )
				gap *= 0.68f;

			if ( !layouts.TryCreateForOrganic(
				     buildingType,
				     districtPlanner.ClusterDistrict,
				     ThornsWorldSettlementKind.Town,
				     out var layoutResult )
			     && ( buildingType == ThornsProcBuildingType.House
			          || !layouts.TryCreateForOrganic(
				          ThornsProcBuildingType.House,
				          districtPlanner.ClusterDistrict,
				          ThornsWorldSettlementKind.Town,
				          out layoutResult ) ) )
			{
				layoutFailures++;
				failed++;
				continue;
			}

			var maxAttempts = isLandmark ? MaxAttemptsLandmark : MaxAttemptsPerBuilding;
			var placedOne = false;
			for ( var attempt = 0; attempt < maxAttempts && !placedOne; attempt++ )
			{
				if ( !TrySamplePosition(
					     rnd,
					     ctx,
					     clusterFootprints,
					     clusterBias,
					     cell,
					     gap,
					     layoutResult.HalfW,
					     layoutResult.HalfD,
					     out var lx,
					     out var ly ) )
					continue;

				if ( !reservation.TryPickYawWithPreview(
					     lx,
					     ly,
					     layoutResult.HalfW,
					     layoutResult.HalfD,
					     gap,
					     SlopeMax,
					     ThornsWorldSettlementKind.Town,
					     chunkRot,
					     rnd,
					     attempt,
					     out var yawDeg,
					     out var yawRad,
					     out _,
					     skipRoads ) )
					continue;

				if ( !reservation.TryValidateFootprint(
					     lx,
					     ly,
					     layoutResult.HalfW,
					     layoutResult.HalfD,
					     yawRad,
					     SlopeMax,
					     ThornsWorldSettlementKind.Town,
					     drawDebugOnReject: false,
					     out var baseZ,
					     out _,
					     out _ ) )
					continue;

				var footprintMin = ThornsTerrainGeometry.SampleObbMinSurfaceHeight(
					ctx.HeightsSpan,
					ctx.Spec,
					lx,
					ly,
					layoutResult.HalfW,
					layoutResult.HalfD,
					yawRad );
				var heightStats = ThornsTerrainGeometry.SampleFootprintHeightMetrics(
					ctx.HeightsSpan,
					ctx.Spec,
					ctx.WorldWidth,
					ctx.WorldDepth,
					lx,
					ly,
					layoutResult.HalfW,
					layoutResult.HalfD,
					yawRad );
				var surfaceZ = ThornsTerrainGeometry.ResolveOrganicBuildingFlatPlotSurfaceZ(
					in heightStats,
					baseZ,
					footprintMin,
					floorT );
				var slabCenterZ = surfaceZ + floorT * 0.5f;
				var flat = new Vector3( lx, ly, slabCenterZ );
				var worldPos = host.ChunkRoot.WorldPosition + chunkRot * flat;
				var verticalLift = MathF.Max( 0f, host.OrganicBuildingVerticalLift );
				if ( verticalLift > 0.01f )
					worldPos += Vector3.Up * verticalLift;

				var groundWorldZ = surfaceZ;
				if ( !ThornsTerrainSystem.IsWorldTerrainSurfaceDryAccessible( scene, groundWorldZ ) )
					continue;

				var procBuildingRootWorld = worldPos;
				var pieces = ThornsProcBuildingSceneSpawner.Spawn(
					host,
					ctx,
					procBuildingRootWorld,
					chunkRot * Rotation.FromYaw( yawDeg ),
					layoutResult.Layout,
					layoutResult.Tier,
					layoutResult.Destroyed,
					ThornsWorldSettlementKind.Town,
					ctx.SpawnedBuildingCount,
					layoutResult.MaterialSlug );

				if ( pieces <= 0 )
				{
					spawnFailures++;
					failed++;
					continue;
				}

				ctx.SpawnedPieceCount += pieces;
				ctx.SpawnedBuildingCount++;
				reservation.Commit( lx, ly, layoutResult.HalfW, layoutResult.HalfD, yawRad, surfaceZ );
				var placedFp = placedFootprints[^1];
				clusterFootprints.Add( placedFp );
				districtPlanner.RegisterPlaced( buildingType );
				buildingsInSpatialCluster++;
				buildingsSinceDistrictRoll++;
				placed++;
				if ( isLandmark )
					landmarksPlaced++;

				var terrainPad = ThornsWorldGenTerrainPadFactory.CreateOrganicBuildingPad(
					lx,
					ly,
					layoutResult.HalfW,
					layoutResult.HalfD,
					yawRad,
					surfaceZ,
					layoutResult.Layout.DoorSide );
				terrainPad.SculptHeightmap = true;
				ctx.Spec.ProcBuildingTerrainPads.Add( terrainPad );
				terrainPadsAdded++;
				terrainPadsSculpt++;
				ThornsWorldSettlementTerrainDiagnostics.RecordLocalFeatherPad();

				if ( host.DrawSettlementLayoutDebug )
				{
					ThornsWorldSettlementPlacementDebugViz.Record(
						lx, ly, layoutResult.HalfW, layoutResult.HalfD, yawRad, true, buildingType );
				}

				host.SiteFootprintsLocal.Add( new Vector2( lx, ly ) );
				placedOne = true;
			}

			if ( !placedOne )
				failed++;
		}

		if ( ctx.Heights is not null && terrainPadsAdded > 0 )
		{
			var heightSpan = ctx.Heights.AsSpan( 0, ctx.HeightCells );
			ThornsWorldGenSettlementTerrainAligner.SyncPlacedBuildingPads( ctx.Spec, placedFootprints );
			ThornsTerrainGeometry.ApplyProcBuildingTerrainPads( ctx.Spec, heightSpan );
		}

		ThornsProcBuildingSettlementDiagnostics.LogSummary();
		ThornsWorldSettlementTerrainDiagnostics.LogSummary();
		Log.Info(
			$"[Thorns Organic] placed={placed}/{targetCount} landmarks={landmarksPlaced}/{landmarkQuota} failed={failed} layoutFail={layoutFailures} spawnFail={spawnFailures} passes={pass}/{maxPlacementPasses} clusterBias={clusterBias:F2} buffer={edgeGap:F0} terrainPads={terrainPadsAdded} sculpt={terrainPadsSculpt} snapOnly={terrainPadsAdded - terrainPadsSculpt}" );

		if ( host.DrawSettlementLayoutDebug && placedFootprints.Count > 0 )
			DrawClusterDebug( host, placedFootprints );
	}

	static bool TrySamplePosition(
		Random rnd,
		ThornsWorldGenerationContext ctx,
		List<ThornsWorldGenProcBuildingFootprint> clusterAnchors,
		float clusterBias,
		float cell,
		float edgeGap,
		float halfW,
		float halfD,
		out float lx,
		out float ly )
	{
		lx = ly = 0f;
		var useCluster = clusterAnchors.Count > 0 && rnd.NextDouble() < clusterBias;

		if ( useCluster )
		{
			var anchor = clusterAnchors[rnd.Next( clusterAnchors.Count )];
			var reach = anchor.HalfW + anchor.HalfD + halfW + halfD + edgeGap;
			var minDist = reach + cell * 0.12f;
			var maxDist = reach + cell * ( 1.5f + (float)rnd.NextDouble() * 4.5f );
			var ang = (float)rnd.NextDouble() * MathF.PI * 2f;
			var dist = minDist + (float)rnd.NextDouble() * ( maxDist - minDist );
			lx = anchor.CenterX + MathF.Cos( ang ) * dist;
			ly = anchor.CenterY + MathF.Sin( ang ) * dist;
		}
		else
		{
			lx = ctx.MinX + (float)rnd.NextDouble() * ( ctx.MaxX - ctx.MinX );
			ly = ctx.MinY + (float)rnd.NextDouble() * ( ctx.MaxY - ctx.MinY );
		}

		var margin = halfW + halfD + edgeGap + cell;
		lx = Math.Clamp( lx, ctx.MinX + margin, ctx.MaxX - margin );
		ly = Math.Clamp( ly, ctx.MinY + margin, ctx.MaxY - margin );
		return true;
	}

	static void DrawClusterDebug(
		ThornsWorldGenerationHostBridge host,
		List<ThornsWorldGenProcBuildingFootprint> footprints )
	{
		var dbg = host.Scene?.GetSystem<DebugOverlaySystem>();
		if ( dbg is null || !host.ChunkRoot.IsValid() )
			return;

		var wt = host.ChunkRoot.Transform.World;
		const float duration = 90f;
		foreach ( var fp in footprints )
		{
			var fz = float.IsNaN( fp.FloorSurfaceZ ) ? 0f : fp.FloorSurfaceZ;
			var center = wt.PointToWorld( new Vector3( fp.CenterX, fp.CenterY, fz + 16f ) );
			var cy = MathF.Cos( fp.YawRad );
			var sy = MathF.Sin( fp.YawRad );
			var corners = new (float bx, float by)[]
			{
				(-fp.HalfW, -fp.HalfD), (fp.HalfW, -fp.HalfD), (fp.HalfW, fp.HalfD), (-fp.HalfW, fp.HalfD)
			};
			for ( var i = 0; i < 4; i++ )
			{
				var (bx0, by0) = corners[i];
				var (bx1, by1) = corners[(i + 1) % 4];
				var a = center + new Vector3( bx0 * cy - by0 * sy, bx0 * sy + by0 * cy, 0 );
				var b = center + new Vector3( bx1 * cy - by1 * sy, bx1 * sy + by1 * cy, 0 );
				dbg.Line( a, b, new Color( 0.45f, 0.9f, 0.55f, 0.7f ), duration, default, false );
			}
		}
	}
}
