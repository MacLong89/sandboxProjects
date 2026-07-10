using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>Runs city, town, and isolated placement using reservation (6), layout (7), and spawn (8) services.</summary>
public static class ThornsWorldGenSettlementPlacer
{
	const int CityMaxAttempts = 220;
	const int TownMaxAttempts = 120;
	const int IsolatedMaxAttempts = 128;
	const int CityLotPlacementMaxAttempts = 96;
	const int LotPlacementMaxAttempts = 40;
	const float LotFootprintSlack = 1.08f;

	static bool IsCityLandmarkType( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.Skyscraper
			or ThornsProcBuildingType.ApartmentTower
			or ThornsProcBuildingType.OfficeBuilding;

	public static void PlaceAll( ThornsWorldGenerationContext ctx, ThornsWorldGenerationHostBridge host )
	{
		ThornsProcBuildingTypeDebugColors.UseTypeColors = host.DebugBuildingTypeColors;
		ThornsWorldSettlementPlacementDiagnostics.Reset();
		if ( host.DrawSettlementLayoutDebug )
			ThornsWorldSettlementPlacementDebugViz.Clear();

		var reservation = ctx.FootprintReservation;
		var layouts = ctx.LayoutFactory;
		var rnd = ctx.PlacementRng;
		var plan = ctx.Plan;
		var districtPlanner = ctx.DistrictPlanner;
		var chunkRot = host.ChunkRoot.WorldRotation;
		var scene = host.Scene;
		var floorT = ThornsBuildingModule.FloorThickness;
		var procBuildingRootVerticalDownShiftWorld = ThornsTerrainSystem.ProcBuildingRootVerticalDownShiftWorld;
		ctx.TownPlacedPerTown = new int[3];
		var clusterPlacement = host.TerrainFirstClusterPlacement;
		var blockPlan = clusterPlacement ? null : ctx.BlockPlan;
		bool TryPlace(
			ThornsProcBuildingType buildingType,
			ThornsProcBuildingDistrict district,
			float spacingMul,
			int maxAttempts,
			ThornsWorldSettlementKind settlementKind,
			ThornsWorldCityRing? cityRing,
			ThornsWorldSettlementPlacementDiagnostics.ZoneStats zoneStats,
			Func<int, (float lx, float ly)> samplePosition,
			IReadOnlyList<ThornsWorldRoadCorridor> roadCorridors = null,
			float? preferredYawRad = null,
			ThornsWorldSettlementLot lot = null,
			int townIndex = -1 )
		{
			zoneStats.Attempted++;
			ThornsWorldSettlementPlanner.GetEstimatedFootprintHalfExtents( buildingType, out var estHalfW, out var estHalfD );

			var onLot = lot is not null;
			var edgeGap = reservation.SettlementFootprintEdgeGap( spacingMul, settlementKind );
			if ( onLot )
				edgeGap *= settlementKind == ThornsWorldSettlementKind.MainCity ? 0.22f : 0.5f;
			var footprintSlopeMax = settlementKind == ThornsWorldSettlementKind.MainCity
				? ThornsWorldGenFootprintReservation.CitySlopeMax
				: ThornsWorldGenFootprintReservation.DefaultSlopeMax;
			var relaxWater = settlementKind == ThornsWorldSettlementKind.MainCity
			                 || settlementKind == ThornsWorldSettlementKind.Town;

			ThornsWorldSettlementPlacementFailureReason lastReason =
				ThornsWorldSettlementPlacementFailureReason.Unknown;

			ThornsWorldGenBuildingLayoutResult? lotLayout = null;
				if ( onLot )
				{
					var lotMaxHalfW = lot.HalfW * LotFootprintSlack;
					var lotMaxHalfD = lot.HalfD * LotFootprintSlack;
					var coreLandmark = settlementKind == ThornsWorldSettlementKind.MainCity
					                   && cityRing == ThornsWorldCityRing.Core
					                   && IsCityLandmarkType( buildingType );
					if ( !layouts.TryCreateForSettlement(
						     buildingType,
						     district,
						     settlementKind,
						     cityRing,
						     out var resolvedLotLayout,
						     coreLandmark ? null : lotMaxHalfW,
						     coreLandmark ? null : lotMaxHalfD )
					     && !layouts.TryCreateForSettlement(
						     buildingType,
						     district,
						     settlementKind,
						     cityRing,
						     out resolvedLotLayout,
						     lotMaxHalfW,
						     lotMaxHalfD ) )
					{
						lastReason = ThornsWorldSettlementPlacementFailureReason.FallbackInvalid;
						zoneStats.RecordFailure( lastReason );
						Log.Warning(
							$"[Thorns Placement] Skipped {buildingType} in {zoneStats.Label} — no valid layout for lot (max {lotMaxHalfW:F0}x{lotMaxHalfD:F0})." );
						return false;
					}

					lotLayout = resolvedLotLayout;
				}

			for ( var attempt = 0; attempt < maxAttempts; attempt++ )
			{
				var (lx, ly) = samplePosition( attempt );
				if ( float.IsNaN( lx ) || float.IsNaN( ly ) )
				{
					lastReason = ThornsWorldSettlementPlacementFailureReason.NoValidRingSlot;
					zoneStats.RecordFailure( lastReason );
					continue;
				}

				ThornsWorldGenBuildingLayoutResult layoutResult;
				float yawDeg;
				float yawRad;
				float halfW;
				float halfD;

				if ( onLot )
				{
					layoutResult = lotLayout!.Value;
					halfW = layoutResult.HalfW;
					halfD = layoutResult.HalfD;

					if ( !reservation.TryPickYawWithPreview(
						     lx,
						     ly,
						     halfW,
						     halfD,
						     edgeGap,
						     footprintSlopeMax,
						     settlementKind,
						     chunkRot,
						     rnd,
						     attempt,
						     out yawDeg,
						     out yawRad,
						     out var lotPreviewFail,
						     ThornsWorldSettlementRoadCorridors.SkipPlacementCorridorCheck,
						     lot.YawRadians ) )
					{
						lastReason = lotPreviewFail;
						zoneStats.RecordFailure( lastReason );
						continue;
					}

					if ( reservation.FootprintOverlapsPlaced( lx, ly, halfW, halfD, yawRad, edgeGap, chunkRot ) )
					{
						lastReason = ThornsWorldSettlementPlacementFailureReason.Overlap;
						zoneStats.RecordFailure( lastReason );
						continue;
					}
				}
				else
				{
					if ( !reservation.TryPickYawWithPreview(
						     lx,
						     ly,
						     estHalfW,
						     estHalfD,
						     edgeGap,
						     footprintSlopeMax,
						     settlementKind,
						     chunkRot,
						     rnd,
						     attempt,
						     out yawDeg,
						     out yawRad,
						     out var previewFail,
						     roadCorridors,
						     preferredYawRad ) )
					{
						lastReason = previewFail;
						zoneStats.RecordFailure( lastReason );
						if ( host.DrawSettlementLayoutDebug )
						{
							ThornsWorldSettlementPlacementDebugViz.Record(
								lx, ly, estHalfW, estHalfD, 0f, false, buildingType, lastReason );
						}

						continue;
					}

					if ( !layouts.TryCreateForSettlement(
						     buildingType,
						     district,
						     settlementKind,
						     cityRing,
						     out layoutResult ) )
					{
						lastReason = ThornsWorldSettlementPlacementFailureReason.FallbackInvalid;
						zoneStats.RecordFailure( lastReason );
						if ( host.DrawSettlementLayoutDebug )
						{
							ThornsWorldSettlementPlacementDebugViz.Record(
								lx, ly, estHalfW, estHalfD, yawRad, false, buildingType, lastReason );
						}

						continue;
					}

					halfW = layoutResult.HalfW;
					halfD = layoutResult.HalfD;

					if ( ThornsWorldSettlementRoadCorridors.FootprintIntersectsCorridor( lx, ly, halfW, halfD, roadCorridors )
					     || reservation.FootprintOverlapsPlaced(
						     lx, ly, halfW, halfD, yawRad, edgeGap, chunkRot, settlementKind ) )
					{
						lastReason = ThornsWorldSettlementPlacementFailureReason.Overlap;
						zoneStats.RecordFailure( lastReason );
						if ( host.DrawSettlementLayoutDebug )
						{
							ThornsWorldSettlementPlacementDebugViz.Record(
								lx, ly, halfW, halfD, yawRad, false, buildingType, lastReason );
						}

						continue;
					}
				}

				var layout = layoutResult.Layout;
				var tier = layoutResult.Tier;
				var materialSlug = layoutResult.MaterialSlug;
				var destroyed = layoutResult.Destroyed;

				if ( !reservation.TryValidateFootprint(
					     lx,
					     ly,
					     halfW,
					     halfD,
					     yawRad,
					     footprintSlopeMax,
					     settlementKind,
					     drawDebugOnReject: true,
					     out var baseZ,
					     out _,
					     out var finalReason ) )
				{
					lastReason = finalReason;
					zoneStats.RecordFailure( lastReason );
					if ( host.DrawSettlementLayoutDebug )
					{
						ThornsWorldSettlementPlacementDebugViz.Record(
							lx, ly, halfW, halfD, yawRad, false, buildingType, lastReason );
					}

					continue;
				}

				if ( ( clusterPlacement || settlementKind != ThornsWorldSettlementKind.MainCity )
				     && ThornsWorldSettlementTerrainValidation.TryEvaluateFootprint(
					     ctx.HeightsSpan,
					     ctx.Spec,
					     ctx.WorldWidth,
					     ctx.WorldDepth,
					     lx,
					     ly,
					     halfW,
					     halfD,
					     yawRad,
					     settlementKind,
					     out var placedTerrain,
					     out _ ) )
				{
					ThornsWorldGenerationQaMetrics.RecordPlacedFootprintTerrain( placedTerrain );
				}

				var useSettlementPrep = !clusterPlacement
				                        && ( settlementKind == ThornsWorldSettlementKind.MainCity
				                             || settlementKind == ThornsWorldSettlementKind.Town );
				var surfaceZ = useSettlementPrep
					? ThornsWorldSettlementBlockTerrain.ResolvePlacementSurfaceZ(
						blockPlan,
						settlementKind,
						townIndex,
						lx,
						ly,
						ctx.HeightsSpan,
						ctx.Spec,
						lot )
					: baseZ - floorT * 0.5f;

				if ( useSettlementPrep )
				{
					var footprintMin = ThornsTerrainGeometry.SampleObbMinSurfaceHeight(
						ctx.HeightsSpan,
						ctx.Spec,
						lx,
						ly,
						halfW,
						halfD,
						yawRad );
					surfaceZ = MathF.Min( surfaceZ, footprintMin );
				}

				var blockIndex = lot?.BlockIndex ?? -1;
				var blockBuildingCount = 1;
				if ( lot is not null && blockPlan is { IsPopulated: true } )
				{
					var area = settlementKind == ThornsWorldSettlementKind.MainCity
						? blockPlan.MainCity
						: townIndex >= 0 ? blockPlan.Town( townIndex ) : null;
					if ( area is not null
					     && ThornsWorldSettlementBlockTerrain.TryFindBlock( area, lot.BlockIndex, out var block ) )
						blockBuildingCount = Math.Max( 1, block.BuildingCount );
				}

				ThornsTerrainProcBuildingPad featherPad = null;
				if ( useSettlementPrep )
				{
					featherPad = ThornsWorldGenTerrainPadFactory.CreateLocalBuildingFeatherPad(
						lx,
						ly,
						halfW,
						halfD,
						yawRad,
						surfaceZ,
						layout.DoorSide,
						mainCity: settlementKind == ThornsWorldSettlementKind.MainCity,
						town: settlementKind == ThornsWorldSettlementKind.Town,
						blockIndex,
						blockBuildingCount );
					featherPad.TargetZ = surfaceZ;
					ctx.Spec.ProcBuildingTerrainPads.Add( featherPad );
					ThornsWorldSettlementTerrainDiagnostics.RecordLocalFeatherPad();
				}

				baseZ = surfaceZ + floorT * 0.5f;

				var flat = new Vector3( lx, ly, baseZ );
				var approx = host.ChunkRoot.WorldPosition + chunkRot * flat;
				var worldPos = approx;
				float groundWorldZ;
				if ( !useSettlementPrep
				     && ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
					     scene,
					     approx,
					     startLiftZ: 4096f,
					     segmentLength: 32768f,
					     out var snapped ) )
				{
					groundWorldZ = snapped.z;
					worldPos = snapped + Vector3.Up * (floorT * 0.5f);
				}
				else
					groundWorldZ = approx.z - floorT * 0.5f;

				if ( !relaxWater && !ThornsTerrainSystem.IsWorldTerrainSurfaceDryAccessible( scene, groundWorldZ ) )
				{
					lastReason = ThornsWorldSettlementPlacementFailureReason.Unknown;
					zoneStats.RecordFailure( lastReason );
					if ( featherPad is not null )
						ctx.Spec.ProcBuildingTerrainPads.Remove( featherPad );
					continue;
				}

				var downShift = useSettlementPrep ? 0f : procBuildingRootVerticalDownShiftWorld;
				var procBuildingRootWorld = worldPos - Vector3.Up * downShift;
				var buildingIndex = ctx.SpawnedBuildingCount;
				var pieces = ThornsProcBuildingSceneSpawner.Spawn(
					host,
					ctx,
					procBuildingRootWorld,
					chunkRot * Rotation.FromYaw( yawDeg ),
					layout,
					tier,
					destroyed,
					settlementKind,
					buildingIndex,
					materialSlug );

				if ( pieces <= 0 )
				{
					if ( featherPad is not null )
						ctx.Spec.ProcBuildingTerrainPads.Remove( featherPad );
					lastReason = ThornsWorldSettlementPlacementFailureReason.FallbackInvalid;
					zoneStats.RecordFailure( lastReason );
					continue;
				}

				ctx.SpawnedPieceCount += pieces;
				ctx.SpawnedBuildingCount++;
				reservation.Commit( lx, ly, halfW, halfD, yawRad, surfaceZ );
				zoneStats.Placed++;

				if ( !useSettlementPrep )
				{
					featherPad = ThornsWorldGenTerrainPadFactory.CreateIsolatedFeatherPad(
						lx,
						ly,
						halfW,
						halfD,
						yawRad,
						surfaceZ,
						layout.DoorSide );
					ctx.Spec.ProcBuildingTerrainPads.Add( featherPad );
					ThornsWorldSettlementTerrainDiagnostics.RecordLocalFeatherPad();
				}

				if ( host.DrawSettlementLayoutDebug )
				{
					ThornsWorldSettlementPlacementDebugViz.Record(
						lx, ly, halfW, halfD, yawRad, true, buildingType );
					ThornsBuildingFoundationDebugViz.DrawPad(
						host.Scene,
						host.ChunkRoot,
						featherPad,
						ctx.HeightsSpan,
						ctx.Spec,
						ctx.WorldWidth,
						ctx.WorldDepth );
				}

				host.SiteFootprintsLocal.Add( new Vector2( lx, ly ) );
				return true;
			}

			zoneStats.RecordFailure( lastReason );
			Log.Warning(
				$"[Thorns Placement] Skipped {buildingType} in {zoneStats.Label} after {maxAttempts} attempts (last={lastReason})." );
			return false;
		}

		var useBlocks = !clusterPlacement && blockPlan is { IsPopulated: true };

		ThornsWorldSettlementBuildingPicker.BeginMainCityCluster( districtPlanner );
		if ( useBlocks && blockPlan.MainCity is not null )
		{
			var cell = ThornsBuildingModule.Cell;
			var placedCitySlots = new HashSet<int>();
			foreach ( var lot in blockPlan.AssignedLotsOrdered( blockPlan.MainCity ) )
			{
				var ring = lot.CityRing ?? ThornsWorldCityRing.MidRing;
				var ringSpacing = ThornsWorldSettlementBuildingPicker.CityRingClearanceMul( ring )
				                  * plan.MainCity.SpacingMultiplier;
				if ( TryPlace(
					     lot.AssignedType!.Value,
					     districtPlanner.ClusterDistrict,
					     ringSpacing,
					     CityLotPlacementMaxAttempts,
					     ThornsWorldSettlementKind.MainCity,
					     ring,
					     ThornsWorldSettlementPlacementDiagnostics.City,
					     attempt => SampleLotPosition(
						     lot, cell, attempt, ctx.MinX, ctx.MaxX, ctx.MinY, ctx.MaxY, mainCity: true ),
					     lot: lot ) )
				{
					ctx.CityPlacedCount++;
					lot.State = ThornsWorldSettlementLotState.Placed;
					if ( lot.SlotIndex >= 0 )
						placedCitySlots.Add( lot.SlotIndex );
				}
			}

			var citySlots = plan.MainCity.BuildingSlots.ToList();
			citySlots.Sort( ThornsWorldSettlementPlacementPriority.CompareSlots );
			foreach ( var slot in citySlots )
			{
				if ( placedCitySlots.Contains( slot.Index ) )
					continue;

				var ring = slot.CityRing ?? ThornsWorldCityRing.MidRing;
				var ringSpacing = ThornsWorldSettlementBuildingPicker.CityRingClearanceMul( ring )
				                  * plan.MainCity.SpacingMultiplier;
				if ( TryPlace(
					     slot.Type,
					     districtPlanner.ClusterDistrict,
					     ringSpacing,
					     CityMaxAttempts,
					     ThornsWorldSettlementKind.MainCity,
					     ring,
					     ThornsWorldSettlementPlacementDiagnostics.City,
					     attempt =>
					     {
						     ThornsWorldSettlementPlanner.SampleCityBuildingPosition(
							     rnd, slot, plan.MainCity, ctx.MinX, ctx.MaxX, ctx.MinY, ctx.MaxY, attempt, out var lx, out var ly );
						     return (lx, ly);
					     },
					     ThornsWorldSettlementRoadCorridors.SkipPlacementCorridorCheck ) )
				{
					ctx.CityPlacedCount++;
					placedCitySlots.Add( slot.Index );
				}
			}
		}
		else
		{
			var citySlots = plan.MainCity.BuildingSlots.ToList();
			citySlots.Sort( ThornsWorldSettlementPlacementPriority.CompareSlots );
			foreach ( var slot in citySlots )
			{
				var ring = slot.CityRing ?? ThornsWorldCityRing.MidRing;
				var ringSpacing = ThornsWorldSettlementBuildingPicker.CityRingClearanceMul( ring )
				                  * plan.MainCity.SpacingMultiplier;
				if ( TryPlace(
					     slot.Type,
					     districtPlanner.ClusterDistrict,
					     ringSpacing,
					     CityMaxAttempts,
					     ThornsWorldSettlementKind.MainCity,
					     ring,
					     ThornsWorldSettlementPlacementDiagnostics.City,
					     attempt =>
					     {
						     ThornsWorldSettlementPlanner.SampleCityBuildingPosition(
							     rnd, slot, plan.MainCity, ctx.MinX, ctx.MaxX, ctx.MinY, ctx.MaxY, attempt, out var lx, out var ly );
						     return (lx, ly);
					     } ) )
					ctx.CityPlacedCount++;
			}
		}

		for ( var t = 0; t < plan.Towns.Count; t++ )
		{
			var town = plan.Towns[t];
			var townStats = ThornsWorldSettlementPlacementDiagnostics.Town( t );
			ThornsWorldSettlementBuildingPicker.BeginTownCluster( districtPlanner );
			var townArea = useBlocks ? blockPlan.Town( t ) : null;
			if ( townArea is not null )
			{
				var cell = ThornsBuildingModule.Cell;
				var placedTownSlots = new HashSet<int>();
				foreach ( var lot in blockPlan.AssignedLotsOrdered( townArea ) )
				{
					if ( TryPlace(
						     lot.AssignedType!.Value,
						     districtPlanner.ClusterDistrict,
						     town.SpacingMultiplier,
						     LotPlacementMaxAttempts,
						     ThornsWorldSettlementKind.Town,
						     null,
						     townStats,
						     attempt => SampleLotPosition( lot, cell, attempt, ctx.MinX, ctx.MaxX, ctx.MinY, ctx.MaxY ),
						     lot: lot,
						     townIndex: t ) )
					{
						ctx.TownPlacedCount++;
						ctx.TownPlacedPerTown[t]++;
						lot.State = ThornsWorldSettlementLotState.Placed;
						if ( lot.SlotIndex >= 0 )
							placedTownSlots.Add( lot.SlotIndex );
					}
				}

				var townSlots = town.BuildingSlots
					.OrderByDescending( s => ThornsWorldSettlementPlacementPriority.GetRank( s.Type ) )
					.ThenBy( s => s.Index )
					.ToList();
				foreach ( var slot in townSlots )
				{
					if ( placedTownSlots.Contains( slot.Index ) )
						continue;

					if ( TryPlace(
						     slot.Type,
						     districtPlanner.ClusterDistrict,
						     town.SpacingMultiplier,
						     TownMaxAttempts,
						     ThornsWorldSettlementKind.Town,
						     null,
						     townStats,
						     attempt =>
						     {
							     ThornsWorldSettlementPlanner.SampleTownBuildingPosition(
								     rnd, slot, town, ctx.MinX, ctx.MaxX, ctx.MinY, ctx.MaxY, attempt, out var lx, out var ly );
							     return (lx, ly);
						     },
						     ThornsWorldSettlementRoadCorridors.SkipPlacementCorridorCheck,
						     townIndex: t ) )
					{
						ctx.TownPlacedCount++;
						ctx.TownPlacedPerTown[t]++;
						placedTownSlots.Add( slot.Index );
					}
				}
			}
			else
			{
				var townSlots = town.BuildingSlots
					.OrderByDescending( s => ThornsWorldSettlementPlacementPriority.GetRank( s.Type ) )
					.ThenBy( s => s.Index )
					.ToList();
				foreach ( var slot in townSlots )
				{
					if ( TryPlace(
						     slot.Type,
						     districtPlanner.ClusterDistrict,
						     town.SpacingMultiplier,
						     TownMaxAttempts,
						     ThornsWorldSettlementKind.Town,
						     null,
						     townStats,
						     attempt =>
						     {
							     ThornsWorldSettlementPlanner.SampleTownBuildingPosition(
								     rnd, slot, town, ctx.MinX, ctx.MaxX, ctx.MinY, ctx.MaxY, attempt, out var lx, out var ly );
							     return (lx, ly);
						     },
						     townIndex: t ) )
					{
					ctx.TownPlacedCount++;
					ctx.TownPlacedPerTown[t]++;
					}
				}
			}
		}

		var isolatedSites = plan.IsolatedSites.ToList();
		isolatedSites.Sort( ThornsWorldSettlementPlacementPriority.CompareIsolated );
		foreach ( var site in isolatedSites )
		{
			ThornsWorldSettlementBuildingPicker.BeginIsolatedCluster( districtPlanner );
			if ( TryPlace(
				     site.Type,
				     districtPlanner.ClusterDistrict,
				     1.45f,
				     IsolatedMaxAttempts,
				     ThornsWorldSettlementKind.Isolated,
				     null,
				     ThornsWorldSettlementPlacementDiagnostics.Isolated,
				     attempt =>
				     {
					     if ( !ThornsWorldSettlementPlanner.TrySampleIsolatedPosition(
						          rnd,
						          site,
						          plan,
						          ctx.MinX,
						          ctx.MaxX,
						          ctx.MinY,
						          ctx.MaxY,
						          ctx.HeightsSpan,
						          ctx.HeightRx,
						          ctx.HeightRz,
						          ctx.WorldWidth,
						          ctx.WorldDepth,
						          ctx.Spec.CenterOnWorldOrigin,
						          ctx.FoliagePropsNoise,
						          ctx.Spec,
						          attempt,
						          out var lx,
						          out var ly ) )
						     return (float.NaN, float.NaN);

					     return (lx, ly);
				     } ) )
			{
				var placedAt = host.SiteFootprintsLocal[^1];
				ThornsWorldSettlementPlanner.RegisterIsolatedPlaced( site.Index, placedAt );
				ctx.IsolatedPlacedCount++;
				if ( host.DrawSettlementLayoutDebug )
					ThornsWorldSettlementDebugViz.DrawIsolatedSite( scene, host.ChunkRoot, placedAt, site.Type );
			}
		}

		ThornsProcBuildingSettlementDiagnostics.LogSummary();
		ThornsWorldSettlementTerrainDiagnostics.LogSummary();
		ThornsWorldSettlementPlacementDiagnostics.LogSummaryDetailed(
			ctx.CityPlacedCount,
			ctx.TownPlacedPerTown,
			ctx.IsolatedPlacedCount );

		if ( ctx.Heights is not null )
		{
			var heightSpan = ctx.Heights.AsSpan( 0, ctx.HeightCells );
			if ( clusterPlacement )
			{
				if ( ctx.Spec.ProcBuildingTerrainPads is { Count: > 0 } )
				{
					ThornsTerrainGeometry.ApplyProcBuildingTerrainPads( ctx.Spec, heightSpan );
				}
			}
			else
			{
				if ( blockPlan?.MainCity is not null )
				{
					ThornsWorldSettlementBlockTerrain.RefineFromPlacements(
						ctx.Spec,
						blockPlan.MainCity,
						host.BuildingFootprints );
				}

				for ( var t = 0; t < plan.Towns.Count; t++ )
				{
					var townArea = blockPlan?.Town( t );
					if ( townArea is null )
						continue;

					ThornsWorldSettlementBlockTerrain.RefineFromPlacements(
						ctx.Spec,
						townArea,
						host.BuildingFootprints );
				}

				ThornsSettlementTerrainInfluence.SyncHubTargetsFromBlocks( ctx.Spec );
				ThornsSettlementTerrainInfluence.ApplyToHeightmap(
					ctx.Spec,
					heightSpan,
					reconcile: true,
					collectDirectionalDebug: host.DrawSettlementLayoutDebug );
				ThornsSettlementTerrainReconciliation.SoftenRoadExitBanks( ctx.Spec, heightSpan );
				ThornsWorldSettlementBlockTerrain.ApplySurfacesToHeightmap( ctx.Spec, heightSpan );

				if ( ctx.Spec.ProcBuildingTerrainPads is { Count: > 0 } )
				{
					ThornsWorldGenSettlementTerrainAligner.SyncPlacedBuildingPads(
						ctx.Spec,
						host.BuildingFootprints );
					ThornsTerrainGeometry.ApplyProcBuildingTerrainPads( ctx.Spec, heightSpan );
				}
			}
		}

		if ( host.DrawSettlementLayoutDebug )
		{
			ThornsWorldSettlementPlacementDebugViz.DrawAll( scene, host.ChunkRoot, plan );
			if ( !clusterPlacement && blockPlan is { IsPopulated: true } )
				ThornsWorldSettlementBlockDebugViz.Draw( scene, host.ChunkRoot, blockPlan );
			if ( !clusterPlacement && ctx.Spec is not null && ctx.Heights is not null )
			{
				ThornsWorldSettlementBlockGroundingDebugViz.Draw(
					scene,
					host.ChunkRoot,
					ctx.Spec,
					ctx.Heights.AsSpan( 0, ctx.HeightCells ),
					ctx.WorldWidth,
					ctx.WorldDepth );
				ThornsWorldSettlementInteriorReadabilityDebugViz.Draw(
					scene,
					host.ChunkRoot,
					ctx.Spec,
					ctx.Heights.AsSpan( 0, ctx.HeightCells ),
					ctx.WorldWidth,
					ctx.WorldDepth );
			}
		}
	}

	static (float lx, float ly) SampleLotPosition(
		ThornsWorldSettlementLot lot,
		float cell,
		int attempt,
		float minX,
		float maxX,
		float minY,
		float maxY,
		bool mainCity = false )
	{
		if ( attempt == 0 )
			return (
				Math.Clamp( lot.CenterLocal.x, minX, maxX ),
				Math.Clamp( lot.CenterLocal.y, minY, maxY ) );

		var phase = attempt / 12;
		var ring = attempt % 12;
		var jitter = cell * ( mainCity ? 0.14f + ring * 0.055f : 0.08f + ring * 0.04f );
		var ang = ring * ( MathF.PI * 2f / 12f );
		var lx = lot.CenterLocal.x + MathF.Cos( ang ) * jitter;
		var ly = lot.CenterLocal.y + MathF.Sin( ang ) * jitter;
		if ( phase >= 1 )
		{
			var step = mainCity ? 0.52f : 0.35f;
			var stepY = mainCity ? 0.44f : 0.28f;
			lx += ( phase - 1 ) * cell * step;
			ly += ( phase - 2 ) * cell * stepY;
		}

		return (
			Math.Clamp( lx, minX, maxX ),
			Math.Clamp( ly, minY, maxY ) );
	}
}
