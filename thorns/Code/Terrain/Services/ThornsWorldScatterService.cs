using System.Buffers;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Host scatter: resources, boulders, procedural sites, interior loot/furniture/defenders, footprint queries.</summary>
public static class ThornsWorldScatterService
{
	const float FootprintSpatialCellSize = 400f;

	public static int ScaleScatterMineralCountForWorldArea( int baseCount, float worldW, float worldD, int maxAfterScale )
	{
		if ( baseCount <= 0 )
			return 0;

		const float refSpan = 32768f;
		var refArea = refSpan * refSpan;
		var area = Math.Max( 64f, worldW ) * Math.Max( 64f, worldD );
		var scale = area / refArea;
		scale = Math.Clamp( scale, 0.25f, 16f );
		var scaled = Math.Max( 0, (int)Math.Round( baseCount * scale ) );
		if ( maxAfterScale > 0 )
			scaled = Math.Min( scaled, maxAfterScale );

		return scaled;
	}

	static bool IsWoodForestAnchorValleyFloor(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float worldW,
		float worldD,
		bool centerOnWorldOrigin,
		float lx,
		float ly,
		float centerHz,
		float neighborRadius,
		float depthBelowNeighborMean )
	{
		if ( neighborRadius <= 4f )
			return false;

		const int ringSamples = 8;
		var sum = 0f;
		for ( var i = 0; i < ringSamples; i++ )
		{
			var ang = i * (MathF.PI * 2f / ringSamples);
			var nx = lx + MathF.Cos( ang ) * neighborRadius;
			var ny = ly + MathF.Sin( ang ) * neighborRadius;
			sum += ThornsTerrainGeometry.SampleHeightLocalZUp(
				heights,
				rx,
				rz,
				worldW,
				worldD,
				centerOnWorldOrigin,
				nx,
				ny );
		}

		var meanNeighbor = sum / ringSamples;
		return centerHz < meanNeighbor - depthBelowNeighborMean;
	}

	static bool ScatterAcceptByNoise01( Random rnd, float noise01 )
	{
		noise01 = Math.Clamp( noise01, 0f, 1f );
		var p = 0.14f + MathF.Pow( noise01, 1.1f ) * 0.86f;
		return rnd.NextDouble() < Math.Clamp( p, 0.12f, 0.993f );
	}

	static bool ChunkXYInsideFootprintObb(
		float lx,
		float ly,
		ThornsWorldGenProcBuildingFootprint fp,
		float margin )
	{
		var dx = lx - fp.CenterX;
		var dy = ly - fp.CenterY;
		var c = MathF.Cos( -fp.YawRad );
		var s = MathF.Sin( -fp.YawRad );
		var bx = dx * c - dy * s;
		var by = dx * s + dy * c;
		return MathF.Abs( bx ) <= fp.HalfW + margin && MathF.Abs( by ) <= fp.HalfD + margin;
	}

	public static bool ChunkPointOverlapsAnyProcBuildingFootprint(
		ThornsTerrainOrchestrationState state,
		float lx,
		float ly )
	{
		const float margin = ThornsBuildingModule.Cell * 0.65f;
		if ( state.FootprintSpatialIndex.Count > 0 )
		{
			var gx = (int)MathF.Floor( lx / FootprintSpatialCellSize );
			var gy = (int)MathF.Floor( ly / FootprintSpatialCellSize );
			if ( !state.FootprintSpatialIndex.TryGetValue( PackFootprintCellKey( gx, gy ), out var candidates ) )
				return false;

			for ( var c = 0; c < candidates.Count; c++ )
			{
				var i = candidates[c];
				if ( i < 0 || i >= state.ProcBuildingFootprintsChunk.Count )
					continue;

				if ( ChunkXYInsideFootprintObb( lx, ly, state.ProcBuildingFootprintsChunk[i], margin ) )
					return true;
			}

			return false;
		}

		for ( var i = 0; i < state.ProcBuildingFootprintsChunk.Count; i++ )
		{
			if ( ChunkXYInsideFootprintObb( lx, ly, state.ProcBuildingFootprintsChunk[i], margin ) )
				return true;
		}

		return false;
	}

	static long PackFootprintCellKey( int gx, int gy ) => ( (long)gx << 32 ) | (uint)gy;

	public static void RebuildProcBuildingFootprintIndex( ThornsTerrainOrchestrationState state )
	{
		state.FootprintSpatialIndex.Clear();
		const float margin = ThornsBuildingModule.Cell * 0.65f;
		var cell = FootprintSpatialCellSize;

		for ( var i = 0; i < state.ProcBuildingFootprintsChunk.Count; i++ )
		{
			var fp = state.ProcBuildingFootprintsChunk[i];
			var reach = MathF.Max( fp.HalfW, fp.HalfD ) + margin + cell * 0.75f;
			var minGx = (int)MathF.Floor( ( fp.CenterX - reach ) / cell );
			var maxGx = (int)MathF.Floor( ( fp.CenterX + reach ) / cell );
			var minGy = (int)MathF.Floor( ( fp.CenterY - reach ) / cell );
			var maxGy = (int)MathF.Floor( ( fp.CenterY + reach ) / cell );

			for ( var gx = minGx; gx <= maxGx; gx++ )
			for ( var gy = minGy; gy <= maxGy; gy++ )
			{
				var key = PackFootprintCellKey( gx, gy );
				if ( !state.FootprintSpatialIndex.TryGetValue( key, out var list ) )
				{
					list = new List<int>( 4 );
					state.FootprintSpatialIndex[key] = list;
				}

				if ( list.Count == 0 || list[list.Count - 1] != i )
					list.Add( i );
			}
		}
	}

	public static void HostScatterTerrainBoulders(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		if ( state.BoulderScatterDone )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( spec.UseTerraingenWorld && spec.UseTerraingenFoliage )
		{
			state.BoulderScatterDone = true;
			Log.Info( "[Thorns] Terrain boulders skipped (terraingen clutter provides ground rocks)." );
			return;
		}

		if ( !terrain.ScatterTerrainBoulders || terrain.ScatterBoulderCount <= 0 )
		{
			state.BoulderScatterDone = true;
			Log.Info( "[Thorns] Terrain boulders skipped (ScatterTerrainBoulders off or ScatterBoulderCount is 0)." );
			return;
		}

		state.BoulderScatterDone = true;

		if ( !state.ChunkRoot.IsValid() )
			return;

		ThornsTerrainHeightmapService.RentScatterHeightmapOrFill( spec, out var heights, out var cells );
		try
		{
			var ww = Math.Max( 64f, spec.WorldWidth );
			var wd = Math.Max( 64f, spec.WorldDepth );
			var hw = ww * 0.5f;
			var hd = wd * 0.5f;
			var inset = Math.Clamp( terrain.ScatterEdgeInsetFraction, 0f, 0.45f );
			var minX = -hw + ww * inset;
			var maxX = hw - ww * inset;
			var minY = -hd + wd * inset;
			var maxY = hd - wd * inset;
			var rx = Math.Max( 2, spec.HeightmapResolutionX );
			var rz = Math.Max( 2, spec.HeightmapResolutionZ );

			var rnd = new Random( unchecked( spec.Seed ^ (int)0xb01de33eu ) );
			var placed = ThornsTerrainBoulderScatter.HostScatter(
				state.ChunkRoot,
				spec,
				heights.AsSpan( 0, cells ),
				rx,
				rz,
				ww,
				wd,
				rnd,
				ThornsTerrainBoulderScatter.DefaultRockModelPaths,
				minX,
				maxX,
				minY,
				maxY,
				Math.Max( 0, terrain.ScatterBoulderCount ),
				terrain.ScatterBoulderMinSeparation,
				terrain.ScatterBoulderResourceClearance,
				terrain.ScatterBoulderFoliageClearance,
				terrain.ScatterBoulderUniformScaleMin,
				terrain.ScatterBoulderUniformScaleMax,
				terrain.ScatterBoulderMaxSlopeDelta,
				Math.Max( 8, terrain.ScatterBoulderMaxAttemptsPerRock ),
				( lx, ly ) =>
				{
					if ( ChunkPointOverlapsAnyProcBuildingFootprint( state, lx, ly ) )
						return true;
					if ( !ThornsWorldRoadTerrain.PointInBoulderClearance( lx, ly, in spec ) )
						return false;
					ThornsWorldGenerationQaMetrics.RecordBoulderScatterRoadSkip();
					return true;
				} );

			Log.Info(
				$"[Thorns] Terrain boulders: requested={terrain.ScatterBoulderCount} placed={placed} minSep={terrain.ScatterBoulderMinSeparation} clearNode={terrain.ScatterBoulderResourceClearance} clearFoliage={terrain.ScatterBoulderFoliageClearance}." );
		}
		finally
		{
			ArrayPool<float>.Shared.Return( heights );
		}
	}

	public static void HostScatterResourceNodesOnTerrain(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		if ( state.ResourceScatterDone )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( !terrain.GenerateResourceNodes || !terrain.ScatterResourceNodes )
		{
			state.ResourceScatterDone = true;
			Log.Info( "[Thorns] Resource nodes skipped (GenerateResourceNodes or ScatterResourceNodes is off)." );
			return;
		}

		state.ResourceScatterDone = true;

		ThornsResourceNode.HostResetMineralSpawnStats();
		ThornsResourceNode.HostWarmupMineralHarvestCatalog();

		ThornsTerrainHeightmapService.RentScatterHeightmapOrFill( spec, out var heights, out var cells );
		try
		{
			var ww = Math.Max( 64f, spec.WorldWidth );
			var wd = Math.Max( 64f, spec.WorldDepth );
			var hw = ww * 0.5f;
			var hd = wd * 0.5f;
			var inset = Math.Clamp( terrain.ScatterEdgeInsetFraction, 0f, 0.45f );
			var minX = -hw + ww * inset;
			var maxX = hw - ww * inset;
			var minY = -hd + wd * inset;
			var maxY = hd - wd * inset;
			var rx = Math.Max( 2, spec.HeightmapResolutionX );
			var rz = Math.Max( 2, spec.HeightmapResolutionZ );

			var scene = terrain.GameObject.Scene;
			var spawnQueue = state.ChunkRoot.IsValid()
				? ThornsDeferredHostSpawnQueue.EnsureOn( state.ChunkRoot, terrain.DeferredHostSpawnsPerFrame )
				: default;
			spawnQueue?.ArmMineralSpawnSummaryLog();

			var rnd = new Random( spec.Seed ^ unchecked((int)0x50b71973u) );
			var foliagePropsNoise = ThornsWorldNoise.CreateFoliagePropsNoise( spec.Seed );
			var woodPlaced = 0;
			var woodForestAnchorsPlaced = 0;

			bool TryPickScatterWorld(
				float lx,
				float ly,
				ThornsResourceKind kind,
				out Vector3 worldPos )
			{
				worldPos = default;
				if ( spec.UseTerraingenWorld
				     && ThornsResourceNode.TryResolveHostScatterWorldPosition(
					     scene,
					     spec,
					     state.ChunkRoot,
					     lx,
					     ly,
					     kind,
					     terrain.ScatterWoodTreeUniformScale,
					     out worldPos ) )
					return true;

				var hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights.AsSpan( 0, cells ),
					rx,
					rz,
					ww,
					wd,
					spec.CenterOnWorldOrigin,
					lx,
					ly );
				if ( !ThornsTerrainSystem.IsSpawnableLandHeight( spec, hz ) )
					return false;

				var flatLocal = new Vector3( lx, ly, hz );
				var approxWorld = state.ChunkRoot.WorldPosition + state.ChunkRoot.WorldRotation * flatLocal;
				worldPos = approxWorld;
				if ( ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
					     scene,
					     approxWorld,
					     startLiftZ: 4096f,
					     segmentLength: 32768f,
					     out var snapped ) )
					worldPos = snapped;
				return true;
			}

			void QueueResourceSpawn( Vector3 worldPos, ThornsResourceKind kind, float woodUniformScale )
			{
				if ( spawnQueue.IsValid() )
				{
					spawnQueue.EnqueueOrRunNow( () => _ = ThornsResourceNode.SpawnHost( scene, worldPos, kind, denseBand: false, woodUniformScale ) );
					return;
				}

				var node = ThornsResourceNode.SpawnHost( scene, worldPos, kind, denseBand: false, woodUniformScale );
				if ( kind == ThornsResourceKind.Wood && node.IsValid() )
					woodPlaced++;
			}

			void ScatterKind( ThornsResourceKind kind, int count, bool useFoliagePropsNoiseGate )
			{
				var isMineral = kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre;
				var mineralInset = Math.Clamp( terrain.ScatterEdgeInsetFraction * 0.35f, 0.015f, 0.1f );
				var kindMinX = isMineral ? -hw + ww * mineralInset : minX;
				var kindMaxX = isMineral ? hw - ww * mineralInset : maxX;
				var kindMinY = isMineral ? -hd + wd * mineralInset : minY;
				var kindMaxY = isMineral ? hd - wd * mineralInset : maxY;
				var maxAttempts = useFoliagePropsNoiseGate ? 118 : isMineral ? 112 : 52;
				for ( var i = 0; i < count; i++ )
				{
					float lx = 0f, ly = 0f, hz = 0f;
					var picked = false;
					for ( var attempt = 0; attempt < maxAttempts && !picked; attempt++ )
					{
						lx = kindMinX + (float)rnd.NextDouble() * (kindMaxX - kindMinX);
						ly = kindMinY + (float)rnd.NextDouble() * (kindMaxY - kindMinY);
						if ( useFoliagePropsNoiseGate )
						{
							var fp = ThornsWorldNoise.SampleFoliageProps01( foliagePropsNoise, lx, ly, in spec );
							if ( !ScatterAcceptByNoise01( rnd, fp ) )
								continue;
						}

						hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
							heights.AsSpan( 0, cells ),
							rx,
							rz,
							ww,
							wd,
							spec.CenterOnWorldOrigin,
							lx,
							ly );
						if ( !ThornsTerrainSystem.IsSpawnableLandHeight( spec, hz ) )
							continue;
						if ( ChunkPointOverlapsAnyProcBuildingFootprint( state, lx, ly ) )
							continue;
						if ( ThornsWorldRoadTerrain.PointInFoliageClearance( lx, ly, in spec ) )
						{
							ThornsWorldGenerationQaMetrics.RecordFoliageScatterRoadSkip();
							continue;
						}

						if ( kind == ThornsResourceKind.Wood
						     && !ThornsTerrainSlope.IsPlanarSlopeWithinDegrees(
							     heights.AsSpan( 0, cells ),
							     rx,
							     rz,
							     ww,
							     wd,
							     spec.CenterOnWorldOrigin,
							     lx,
							     ly,
							     ThornsTerrainSlope.DefaultMaxTreeSlopeDegrees ) )
							continue;

						picked = true;
					}

					if ( !picked )
						continue;

					if ( !TryPickScatterWorld( lx, ly, kind, out var worldPos ) )
						continue;

					QueueResourceSpawn( worldPos, kind, terrain.ScatterWoodTreeUniformScale );
				}
			}

			var treeN = Math.Max( 0, terrain.ScatterTreeCount );
			if ( spec.UseTerraingenFoliage )
				treeN = 0; // foliage2 placement via ThornsFoliageFoundation (harvest on those instances)
			var stoneN = ScaleScatterMineralCountForWorldArea( Math.Max( 0, terrain.ScatterStoneCount ), ww, wd, terrain.MaxScatterStoneCountAfterAreaScale );
			var oreN = ScaleScatterMineralCountForWorldArea( Math.Max( 0, terrain.ScatterMetalOreCount ), ww, wd, terrain.MaxScatterMetalOreCountAfterAreaScale );
			var fiberN = 0;

			// Older scene JSON omitted MetalOre — deserialize as 0. If trees/stones are authored, use the code default for ore.
			if ( oreN == 0 && fiberN == 0 && (treeN > 0 || stoneN > 0) )
			{
				oreN = ScaleScatterMineralCountForWorldArea( 600, ww, wd, terrain.MaxScatterMetalOreCountAfterAreaScale );
			}

			var forestClusterTarget = spec.UseTerraingenFoliage
				? 0
				: Math.Max( 0, terrain.ScatterWoodForestClusterCount );
			var forestRadMin = Math.Min( terrain.ScatterWoodForestClusterRadiusMin, terrain.ScatterWoodForestClusterRadiusMax );
			var forestRadMax = Math.Max( terrain.ScatterWoodForestClusterRadiusMin, terrain.ScatterWoodForestClusterRadiusMax );
			var valleyRingR = Math.Max( 0f, terrain.ScatterWoodValleyNeighborRadius );
			var valleyDepth = Math.Max( 0f, terrain.ScatterWoodValleyDepthBelowNeighbors );

			if ( forestClusterTarget > 0 && treeN > 0 )
			{
				const int maxAnchorAttempts = 140;
				var anchors = new List<(float x, float y, float r)>( forestClusterTarget );
				for ( var a = 0; a < forestClusterTarget; a++ )
				{
					for ( var attempt = 0; attempt < maxAnchorAttempts; attempt++ )
					{
						var ax = minX + (float)rnd.NextDouble() * (maxX - minX);
						var ay = minY + (float)rnd.NextDouble() * (maxY - minY);
						if ( terrain.ScatterWoodUsesBiomeNoise )
						{
							var fp = ThornsWorldNoise.SampleFoliageProps01( foliagePropsNoise, ax, ay, in spec );
							if ( !ScatterAcceptByNoise01( rnd, fp ) )
								continue;
						}

						var ahz = ThornsTerrainGeometry.SampleHeightLocalZUp(
							heights.AsSpan( 0, cells ),
							rx,
							rz,
							ww,
							wd,
							spec.CenterOnWorldOrigin,
							ax,
							ay );
						if ( !ThornsTerrainSystem.IsSpawnableLandHeight( spec, ahz ) )
							continue;
						if ( ChunkPointOverlapsAnyProcBuildingFootprint( state, ax, ay ) )
							continue;
						if ( ThornsWorldRoadTerrain.PointInFoliageClearance( ax, ay, in spec ) )
						{
							ThornsWorldGenerationQaMetrics.RecordFoliageScatterRoadSkip();
							continue;
						}

						if ( !ThornsTerrainSlope.IsPlanarSlopeWithinDegrees(
							     heights.AsSpan( 0, cells ),
							     rx,
							     rz,
							     ww,
							     wd,
							     spec.CenterOnWorldOrigin,
							     ax,
							     ay,
							     ThornsTerrainSlope.DefaultMaxTreeSlopeDegrees ) )
							continue;

						if ( terrain.ScatterWoodSkipValleyAnchors &&
						     IsWoodForestAnchorValleyFloor(
							     heights.AsSpan( 0, cells ),
							     rx,
							     rz,
							     ww,
							     wd,
							     spec.CenterOnWorldOrigin,
							     ax,
							     ay,
							     ahz,
							     valleyRingR,
							     valleyDepth ) )
							continue;

						var diskR = forestRadMin + (float)rnd.NextDouble() * (forestRadMax - forestRadMin);
						anchors.Add( (ax, ay, diskR) );
						break;
					}
				}

				woodForestAnchorsPlaced = anchors.Count;

				if ( anchors.Count > 0 )
				{
					const int maxTreeAttempts = 52;
					for ( var i = 0; i < treeN; i++ )
					{
						for ( var attempt = 0; attempt < maxTreeAttempts; attempt++ )
						{
							var ac = anchors[rnd.Next( anchors.Count )];
							var ang = (float)(rnd.NextDouble() * Math.PI * 2.0);
							var rr = MathF.Sqrt( (float)rnd.NextDouble() ) * ac.r;
							var lx = ac.x + MathF.Cos( ang ) * rr;
							var ly = ac.y + MathF.Sin( ang ) * rr;
							if ( lx < minX || lx > maxX || ly < minY || ly > maxY )
								continue;

							var hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
								heights.AsSpan( 0, cells ),
								rx,
								rz,
								ww,
								wd,
								spec.CenterOnWorldOrigin,
								lx,
								ly );
							if ( !ThornsTerrainSystem.IsSpawnableLandHeight( spec, hz ) )
								continue;
							if ( ChunkPointOverlapsAnyProcBuildingFootprint( state, lx, ly ) )
								continue;

							if ( !ThornsTerrainSlope.IsPlanarSlopeWithinDegrees(
								     heights.AsSpan( 0, cells ),
								     rx,
								     rz,
								     ww,
								     wd,
								     spec.CenterOnWorldOrigin,
								     lx,
								     ly,
								     ThornsTerrainSlope.DefaultMaxTreeSlopeDegrees ) )
								continue;

							if ( !TryPickScatterWorld( lx, ly, ThornsResourceKind.Wood, out var worldPos ) )
								continue;

							QueueResourceSpawn( worldPos, ThornsResourceKind.Wood, terrain.ScatterWoodTreeUniformScale );
							break;
						}
					}
				}
				else
				{
					ScatterKind( ThornsResourceKind.Wood, treeN, terrain.ScatterWoodUsesBiomeNoise );
				}
			}
			else
			{
				ScatterKind( ThornsResourceKind.Wood, treeN, terrain.ScatterWoodUsesBiomeNoise );
			}

			ScatterKind( ThornsResourceKind.Stone, stoneN, useFoliagePropsNoiseGate: false );
			ScatterKind( ThornsResourceKind.MetalOre, oreN, useFoliagePropsNoiseGate: false );

			var deferredPending = spawnQueue.IsValid() ? spawnQueue.PendingCount : 0;
			Log.Info(
				$"[Thorns] Terrain resource scatter: trees requested={treeN} placedNow={woodPlaced} deferredPending={deferredPending} (@{terrain.DeferredHostSpawnsPerFrame}/frame) woodBiomeNoise={terrain.ScatterWoodUsesBiomeNoise} woodForestClusters={terrain.ScatterWoodForestClusterCount} woodForestAnchors={woodForestAnchorsPlaced} stones={stoneN} ore={oreN} fiber={fiberN} (skips {state.ProcBuildingFootprintsChunk.Count} proc-building footprints)." );
		}
		finally
		{
			ArrayPool<float>.Shared.Return( heights );
		}
	}

	public static void HostScatterProceduralSitesAndRingCrates(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		if ( state.ProceduralSiteScatterDone )
			return;

		state.ProceduralSiteScatterDone = true;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( !state.ChunkRoot.IsValid() )
			return;

		state.SiteFootprintsChunkLocal.Clear();
		state.ProcBuildingFootprintsChunk.Clear();
		state.ProcBuildingsForLoot.Clear();
		state.FootprintSpatialIndex.Clear();
		ThornsProcBuildingNpcRegistry.HostClear();

		var skipped = false;
		try
		{
			if ( !terrain.GenerateProceduralBuildings )
			{
				Log.Info( "[Thorns] Procedural buildings skipped (GenerateProceduralBuildings=false)." );
				skipped = true;
				return;
			}

			if ( !terrain.ScatterProceduralSites )
			{
				Log.Info( "[Thorns] Procedural buildings skipped (ScatterProceduralSites=false)." );
				skipped = true;
				return;
			}

			var bridge = ThornsWorldGenRunnerService.CreateHostBridge( terrain, state );
			if ( terrain.TimeSlicePreChunkWorldGen )
			{
				ThornsDeferredHostSpawnQueue.EnsureOn( state.ChunkRoot, terrain.DeferredHostSpawnsPerFrame );
				state.DeferredPreChunkWorldGen = ThornsDeferredWorldGenerationSession.Begin( bridge, spec, terrain.ScatterEdgeInsetFraction );
				state.AwaitingPreChunkWorldGen = true;
			}
			else
				new ThornsWorldGenerationPipeline( bridge ).RunPreChunkSettlementPipeline( spec );
		}
		finally
		{
			if ( skipped )
				ThornsWorldGenRunnerService.FinalizeTerrainSpecWithoutBuildings( terrain, state, spec );
		}
	}

	public static void RunEnvironmentScatter(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		HostScatterResourceNodesOnTerrain( terrain, state, spec );
		HostScatterTerrainBoulders( terrain, state, spec );
	}

	public static void RunInteriorLootScatter(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec ) =>
		HostScatterLootInsideProceduralBuildings( terrain, state, spec );

	public static void RunInteriorFurnitureScatter(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec ) =>
		HostScatterInteriorFurniture( terrain, state, spec );

	public static void RunInteriorCityDefenderScatter(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec ) =>
		HostScatterCityDefendersInsideProceduralBuildings( terrain, state, spec );

	public static void HostScatterLootInsideProceduralBuildings(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		if ( state.InteriorLootScatterDone )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( !state.ChunkRoot.IsValid() )
			return;

		state.InteriorLootScatterDone = true;

		if ( !terrain.ScatterLootCrates )
			return;

		var buildings = state.ProcBuildingsForLoot;
		var nb = buildings.Count;

		if ( nb == 0 )
		{
			Log.Info( "[Thorns] Loot crates: no procedural buildings — skipping interior scatter." );
			return;
		}

		var scene = terrain.GameObject.Scene;
		var rnd = new Random( unchecked(spec.Seed ^ (int)0x71c6c6e5u ) );
		var rndKinds = new Random( unchecked(spec.Seed ^ (int)0x10efc47eu ) );

		var spawned = 0;
		var floorRolls = 0;
		var placementFails = 0;
		var radiosSpawned = 0;
		var radiosEligible = 0;
		var radiosPlacementFailed = 0;
		var floorChance = Math.Clamp( terrain.InteriorLootCrateFloorChance, 0f, 1f );
		for ( var bi = 0; bi < nb; bi++ )
		{
			var b = buildings[bi];
			if ( !b.Root.IsValid() )
				continue;

			var tier = Math.Clamp( b.MaterialTier, 0, 2 );
			var placement = new ThornsProcBuildingInteriorSample.InteriorPlacementBatch();
			var placeHints = new ThornsProcBuildingInteriorSample.InteriorPlacementHints
			{
				DoorSide = b.DoorSide,
				DoorIndex = b.DoorIndex
			};
			for ( var story = 0; story < b.Stories; story++ )
			{
				if ( rnd.NextDouble() >= floorChance )
					continue;

				floorRolls++;
				if ( !placement.TrySampleLootAnchor(
					     rnd,
					     scene,
					     b.Root,
					     b.WidthCells,
					     b.DepthCells,
					     b.Stories,
					     placeHints,
					     out var wp,
					     forceStoryIndex: story ) )
				{
					placementFails++;
					continue;
				}

				var kind = ThornsLootGenerator.PickKindForProcBuilding( b.BuildingType, rndKinds );
				ThornsLootCrate.SpawnHost(
					scene,
					wp,
					kind,
					rndKinds,
					worldRegeneratesWhenEmpty: true,
					interiorProcBuildingMaterialTier: tier,
					interiorProcBuildingType: b.BuildingType );
				spawned++;
			}

			var radioStory = b.Stories >= ThornsProcBuildingInteriorSample.InteriorRadioStationStoryIndex + 1
				? ThornsProcBuildingInteriorSample.InteriorRadioStationStoryIndex
				: 0;
			var tryRadio = b.Stories >= ThornsProcBuildingInteriorSample.InteriorRadioStationStoryIndex + 1
			                 || ThornsInteriorFurnitureProfiles.ShouldSpawnInteriorRadioShop( b.BuildingType );
			if ( tryRadio && ( radioStory > 0 || rnd.NextDouble() < 0.42f ) )
			{
				radiosEligible++;
				if ( ThornsProcBuildingInteriorSample.TryFindRadioPlacementOnStory(
					     b.Root,
					     b.WidthCells,
					     b.DepthCells,
					     b.Stories,
					     radioStory,
					     placeHints,
					     rnd,
					     out var radioPos,
					     out var radioRot ) )
				{
					ThornsInteriorRadioSpawn.TrySpawnInteriorStation( scene, radioPos, radioRot );
					radiosSpawned++;
				}
				else
					radiosPlacementFailed++;
			}
		}

		Log.Info(
			$"[Thorns] Interior loot crates: buildings={nb} crates={spawned} floorRolls={floorRolls} placementFails={placementFails} floorChance={floorChance:P0} radios={radiosSpawned}/{radiosEligible} failed={radiosPlacementFailed} (5th storey index {ThornsProcBuildingInteriorSample.InteriorRadioStationStoryIndex} when stories≥5; regen after {ThornsLootCrate.WorldLootRegenSeconds:F0}s)" );
	}

	public static void HostScatterInteriorFurniture(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( !state.ChunkRoot.IsValid() )
			return;

		if ( state.InteriorFurnitureScatterDone )
			return;

		state.InteriorFurnitureScatterDone = true;

		if ( !terrain.ScatterInteriorFurniture )
		{
			Log.Info( "[Thorns] Interior furniture skipped (ScatterInteriorFurniture=false)." );
			return;
		}

		var buildings = state.ProcBuildingsForLoot;
		var nb = buildings.Count;
		if ( nb == 0 )
		{
			Log.Info( "[Thorns] Interior furniture: no procedural buildings — skipping." );
			return;
		}

		var scene = terrain.GameObject.Scene;
		var rnd = new Random( unchecked( spec.Seed ^ (int)0x8a3c91f2u ) );
		var stats = new InteriorFurnitureScatterStats { BuildingsTotal = nb };
		var queue = ThornsDeferredHostSpawnQueue.EnsureOn( state.ChunkRoot, terrain.DeferredHostSpawnsPerFrame );

		for ( var bi = 0; bi < nb; bi++ )
		{
			var building = buildings[bi];
			if ( !building.Root.IsValid() )
				continue;

			queue.EnqueueOrRunNow( () => HostScatterInteriorFurnitureBuilding( scene, rnd, building, stats ) );
		}

		queue.EnqueueOrRunNow( () =>
		{
			Log.Info(
				$"[Thorns] Interior furniture: buildings={stats.BuildingsTotal} filled={stats.BuildingsFilled} props={stats.FurnitureSpawned} "
				+ $"mode=scripted-corners (1–{ThornsInteriorFurnitureFloorplanAscii.MaxSettlementAsciiStories} storeys) "
				+ $"layoutRev={ThornsInteriorFurnitureAsciiLayouts.LayoutCatalogRevision} "
				+ $"catalogRev={ThornsPlaceableFurniturePresentation.CatalogRevision}" );

			HostRefreshInteriorFurnitureScalesIfNeeded( terrain, state, commitRevisionWhenNoProps: true );
			ThornsPlaceableFurniturePresentation.LogCatalogScaleProbe(
				"world-gen",
				"chair",
				"couch",
				"desk",
				"kitchen_fridge",
				"pallets",
				"retail" );
		} );
	}

	sealed class InteriorFurnitureScatterStats
	{
		public int BuildingsTotal;
		public int FurnitureSpawned;
		public int BuildingsFilled;
	}

	static void HostScatterInteriorFurnitureBuilding(
		Scene scene,
		Random rnd,
		ThornsWorldGenProcBuildingInteriorLoot building,
		InteriorFurnitureScatterStats stats )
	{
		if ( !building.Root.IsValid() )
			return;

		var placeHints = new ThornsProcBuildingInteriorSample.InteriorPlacementHints
		{
			DoorSide = building.DoorSide,
			DoorIndex = building.DoorIndex,
			ScriptedFloorplanExactExclusions = true
		};

		var furnitureBatch = new ThornsInteriorFurniturePlacement.Batch();
		SeedInteriorLootAnchorsOnFurnitureBatch( furnitureBatch, building.Root, building.WidthCells, building.DepthCells, building.Stories );
		var scriptedOnly = ThornsInteriorFurnitureAsciiLayouts.SupportsBuildingType( building.BuildingType );
		var n = ThornsInteriorFurnitureScatter.ScatterBuilding(
			scene,
			rnd,
			building.Root,
			building.WidthCells,
			building.DepthCells,
			building.Stories,
			building.BuildingType,
			placeHints,
			furnitureBatch,
			scriptedPlacementsOnly: scriptedOnly );

		if ( n > 0 )
			stats.BuildingsFilled++;

		stats.FurnitureSpawned += n;
	}

	public static void HostRefreshInteriorFurnitureScalesIfNeeded(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		bool commitRevisionWhenNoProps = false )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		var rev = ThornsPlaceableFurniturePresentation.CatalogRevision;
		if ( state.FurnitureCatalogScaleRevisionApplied == rev )
			return;

		var scene = terrain.GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var refreshed = ThornsInteriorFurnitureScatter.RefreshAllInteriorFurnitureScales( scene );
		if ( refreshed > 0 )
		{
			state.FurnitureCatalogScaleRevisionApplied = rev;
			Log.Info(
				$"[Thorns] Interior furniture catalog revision {rev}: refreshed {refreshed} prop(s) (normalized bounds × catalog size)." );
			ThornsPlaceableFurniturePresentation.LogCatalogScaleProbe( "furniture-refresh", "desk" );
			return;
		}

		if ( !commitRevisionWhenNoProps )
		{
			Log.Info(
				$"[Thorns] Interior furniture catalog revision {rev}: {refreshed} prop(s) in scene — will re-apply after scatter or when props exist." );
			return;
		}

		state.FurnitureCatalogScaleRevisionApplied = rev;
		Log.Info(
			$"[Thorns] Interior furniture catalog revision {rev}: committed after scatter ({refreshed} existing prop(s) refreshed)." );
	}

	static void SeedInteriorLootAnchorsOnFurnitureBatch(
		ThornsInteriorFurniturePlacement.Batch batch,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories )
	{
		if ( batch is null || buildingRoot is null || !buildingRoot.IsValid() )
			return;

		var cell = ThornsBuildingModule.Cell;
		var planar = ThornsProcBuildingInteriorSample.InteriorLootCratePlanarHalfExtent();
		var halfW = widthCells * cell * 0.55f;
		var halfD = depthCells * cell * 0.55f;
		var origin = buildingRoot.WorldPosition;

		foreach ( var crate in ThornsLootCrate.ActiveById.Values )
		{
			if ( crate is null || !crate.IsValid() || !crate.GameObject.IsValid() )
				continue;

			var p = crate.GameObject.WorldPosition;
			if ( MathF.Abs( p.x - origin.x ) > halfW || MathF.Abs( p.y - origin.y ) > halfD )
				continue;

			var local = buildingRoot.WorldRotation.Inverse * (p - origin);
			var story = stories <= 1
				? 0
				: Math.Clamp( (int)MathF.Floor( local.z / ThornsBuildingModule.StoryHeightWorld ), 0, stories - 1 );
			batch.RegisterReservedAnchor( buildingRoot, widthCells, depthCells, p, planar, story );
		}
	}

	public static void HostScatterCityDefendersInsideProceduralBuildings(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		if ( state.InteriorDefenderScatterDone )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( !state.ChunkRoot.IsValid() )
			return;

		state.InteriorDefenderScatterDone = true;

		if ( !terrain.ScatterCityDefenders )
			return;

		var buildings = state.ProcBuildingsForLoot;
		var nb = buildings.Count;
		if ( nb == 0 )
		{
			Log.Info( "[Thorns] City defenders: no procedural buildings — skipping interior scatter." );
			return;
		}

		var scene = terrain.GameObject.Scene;
		var rnd = new Random( unchecked( spec.Seed ^ (int)0x4d3f3a91u ) );
		var floorChance = Math.Clamp( terrain.InteriorCityDefenderFloorChance, 0f, 1f );
		var maxPerBuilding = Math.Max( 0, terrain.InteriorCityDefenderMaxPerBuilding );
		var spawned = 0;

		for ( var bi = 0; bi < nb; bi++ )
		{
			var b = buildings[bi];
			if ( !b.Root.IsValid() )
				continue;

			var hints = new ThornsProcBuildingInteriorSample.InteriorPlacementHints
			{
				DoorSide = b.DoorSide,
				DoorIndex = b.DoorIndex
			};
			spawned += ThornsProcBuildingCityDefenderSpawn.HostTryFillBuilding(
				scene,
				rnd,
				b.Root,
				b.WidthCells,
				b.DepthCells,
				b.Stories,
				floorChance,
				maxPerBuilding,
				hints );
		}

		Log.Info(
			$"[Thorns] City defenders (world-gen): buildings={nb} spawned={spawned} floorChance={floorChance:P0} maxPerBuilding={maxPerBuilding}; timer refill via ThornsCityDefenderScheduler" );
	}
}
