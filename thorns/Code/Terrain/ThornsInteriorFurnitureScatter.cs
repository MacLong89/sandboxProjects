using Terraingen.Foliage;

namespace Sandbox;

/// <summary>Decorative furniture inside procedural buildings — not networked structures.</summary>
public static class ThornsInteriorFurnitureScatter
{
	public const string DecorParentName = "ThornsInteriorFurniture";

	/// <summary>When true, interior props are local-only and get <see cref="ThornsFloorplanFurnitureTuneItem"/> (floorplan test scene).</summary>
	public static bool FloorplanLiveTuningEnabled { get; set; }

	/// <summary>3×3 corner rule: three non-ramp corners on the ground floor.</summary>
	public const int GroundStoryCornerFurnitureCount = 3;

	/// <summary>Hard cap on duplicate <see cref="ThornsPlaceableFurnitureCatalog"/> ids per storey.</summary>
	public const int MaxPerTypePerStory = 2;

	/// <summary>Footprints at or below this use compact props + anchor fill (3×3 cabin, etc.).</summary>
	public const int CompactFootprintMaxCells = 16;

	static readonly HashSet<string> LargeStructureIds = new( StringComparer.OrdinalIgnoreCase )
	{
		"kitchen_fridge",
		"couch",
		"dining_table",
		"conference",
		"pallets",
		"retail",
		"bunk",
		"military_supply",
		"workbench"
	};

	public static int ScatterBuilding(
		Scene scene,
		Random rnd,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		ThornsProcBuildingType buildingType,
		in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
		ThornsInteriorFurniturePlacement.Batch placementBatch,
		int piecesPerFloorMin = 0,
		int piecesPerFloorMax = 0,
		int layoutVariantOverride = -1,
		bool scriptedPlacementsOnly = false )
	{
		if ( scene is null || !scene.IsValid() || buildingRoot is null || !buildingRoot.IsValid() )
			return 0;

		stories = ThornsProcBuildingPoc.EffectiveStories( stories );
		if ( stories <= 0 )
			return 0;

		var decorRoot = buildingRoot.Children.FirstOrDefault( c => c.IsValid() && c.Name == DecorParentName );
		if ( !decorRoot.IsValid() )
		{
			decorRoot = new GameObject( true, DecorParentName );
			decorRoot.SetParent( buildingRoot );
			decorRoot.LocalScale = Vector3.One;
			decorRoot.LocalPosition = Vector3.Zero;
		}

		var compact = widthCells * depthCells <= CompactFootprintMaxCells;
		var tightFloor = compact || buildingType == ThornsProcBuildingType.ApartmentTower;
		var layoutHost = ThornsProcBuildingLayoutHost.TryGet( buildingRoot ) is not null;

		var buildingStats = new ThornsInteriorFurnitureScatterDebug.BuildingStats
		{
			Type = buildingType,
			WidthCells = widthCells,
			DepthCells = depthCells,
			Stories = stories,
			WorldPos = buildingRoot.WorldPosition
		};

		var layouts = ThornsInteriorFurnitureProfiles.LayoutsForBuilding( buildingType );
		var procLayout = ThornsProcBuildingLayoutHost.TryGet( buildingRoot );
		var useScriptedFloorplan = ThornsInteriorFurnitureAsciiLayouts.SupportsBuildingType( buildingType );
		var asciiVariantCount = useScriptedFloorplan
			? ThornsInteriorFurnitureAsciiLayouts.VariantCount( buildingType )
			: 0;
		var layoutVariant = layoutVariantOverride >= 0 && asciiVariantCount > 0
			? Math.Clamp( layoutVariantOverride, 0, asciiVariantCount - 1 )
			: procLayout?.Identity?.InteriorAsciiVariantIndex >= 0 && asciiVariantCount > 0
				? Math.Clamp(
					procLayout.Identity.InteriorAsciiVariantIndex,
					0,
					asciiVariantCount - 1 )
				: useScriptedFloorplan
					? ThornsInteriorFurnitureAsciiLayouts.PickVariantIndex( rnd, buildingType )
					: ThornsInteriorFurnitureFloorLayouts.PickVariantIndex( rnd, layouts );
		var spawned = 0;

		for ( var story = 0; story < stories; story++ )
		{
			var targetCorners = useScriptedFloorplan
				? ThornsInteriorFurnitureCanonicalSlots.ExpectedFurnitureCountForStory(
					story,
					widthCells,
					depthCells,
					procLayout )
				: Math.Max( piecesPerFloorMin, piecesPerFloorMax );
			var minPerFloor = useScriptedFloorplan ? targetCorners : piecesPerFloorMin;
			var maxPerFloor = useScriptedFloorplan ? targetCorners : piecesPerFloorMax;
			if ( scriptedPlacementsOnly && !useScriptedFloorplan )
			{
				minPerFloor = 0;
				maxPerFloor = 0;
			}

			var floorStats = new ThornsInteriorFurnitureScatterDebug.StoryStats
			{
				TargetMin = minPerFloor,
				WalkableCells = ThornsInteriorFurnitureScatterDebug.CountWalkableFloorCells(
					buildingRoot,
					widthCells,
					depthCells,
					stories,
					story ),
				LayoutHostPresent = layoutHost ? 1 : 0
			};
			buildingStats.Floors.Add( floorStats );

			var storySpawned = 0;

			if ( useScriptedFloorplan )
			{
				var scripted = new List<ThornsInteriorFurnitureFloorplanAscii.CellPlacement>( 8 );
				if ( !ThornsInteriorFurnitureAsciiLayouts.TryCollectScriptedPlacements(
					     buildingType,
					     layoutVariant,
					     story,
					     widthCells,
					     depthCells,
					     procLayout,
					     scripted ) )
					continue;

				var scriptedHints = new ThornsProcBuildingInteriorSample.InteriorPlacementHints
				{
					DoorSide = hints.DoorSide,
					DoorIndex = hints.DoorIndex,
					ScriptedFloorplanExactExclusions = true
				};

				storySpawned = ScatterScriptedCells(
					scene,
					buildingRoot,
					widthCells,
					depthCells,
					stories,
					scriptedHints,
					scripted,
					placementBatch );

				spawned += storySpawned;
				floorStats.Placed = storySpawned;
				floorStats.TargetMin = targetCorners;
				floorStats.LayoutNormalOk = storySpawned;
				continue;
			}

			var floorPieces = scriptedPlacementsOnly
				? null
				: ThornsInteriorFurnitureFloorLayouts.GetFloorPieces( layouts, layoutVariant, story );
			var countByType = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );

			if ( floorPieces is not null )
			{
				for ( var i = 0; i < floorPieces.Count && storySpawned < maxPerFloor; i++ )
				{
					var id = floorPieces[i];
					if ( tightFloor && IsLargeStructure( id ) )
						continue;

					if ( TryPlaceOnStory(
						     rnd,
						     scene,
						     buildingRoot,
						     widthCells,
						     depthCells,
						     stories,
						     hints,
						     placementBatch,
						     decorRoot,
						     id,
						     story,
						     relaxed: false,
						     maxPerFloor,
						     ref storySpawned,
						     countByType,
						     ref spawned,
						     floorStats ) )
						floorStats.LayoutNormalOk++;
					else
						floorStats.LayoutNormalFail++;

					if ( storySpawned >= maxPerFloor )
						continue;

					if ( TryPlaceOnStory(
						     rnd,
						     scene,
						     buildingRoot,
						     widthCells,
						     depthCells,
						     stories,
						     hints,
						     placementBatch,
						     decorRoot,
						     id,
						     story,
						     relaxed: true,
						     maxPerFloor,
						     ref storySpawned,
						     countByType,
						     ref spawned,
						     floorStats ) )
						floorStats.LayoutRelaxedOk++;
					else
						floorStats.LayoutRelaxedFail++;
				}
			}

			if ( minPerFloor > 0 || maxPerFloor > 0 )
			{
				FillStoryToMinimum(
					rnd,
					scene,
					buildingRoot,
					widthCells,
					depthCells,
					stories,
					hints,
					placementBatch,
					decorRoot,
					story,
					minPerFloor,
					maxPerFloor,
					tightFloor,
					ref storySpawned,
					countByType,
					ref spawned,
					floorStats );
			}

			floorStats.Placed = storySpawned;
		}

		buildingStats.TotalPlaced = spawned;
		buildingStats.LogSummary();

		return spawned;
	}

	/// <summary>Places furniture at explicit grid cells (ASCII floorplan scripts).</summary>
	public static int ScatterScriptedCells(
		Scene scene,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
		IReadOnlyList<ThornsInteriorFurnitureFloorplanAscii.CellPlacement> cells,
		ThornsInteriorFurniturePlacement.Batch placementBatch = null )
	{
		if ( scene is null || !scene.IsValid() || buildingRoot is null || !buildingRoot.IsValid() || cells is null )
			return 0;

		stories = ThornsProcBuildingPoc.EffectiveStories( stories );
		var decorRoot = buildingRoot.Children.FirstOrDefault( c => c.IsValid() && c.Name == DecorParentName );
		if ( !decorRoot.IsValid() )
		{
			decorRoot = new GameObject( true, DecorParentName );
			decorRoot.SetParent( buildingRoot );
			decorRoot.LocalScale = Vector3.One;
			decorRoot.LocalPosition = Vector3.Zero;
		}

		var batch = placementBatch ?? new ThornsInteriorFurniturePlacement.Batch();
		var spawned = 0;

		foreach ( var cell in cells )
		{
			if ( cell.Story < 0 || cell.Story >= stories )
				continue;

			if ( !ThornsPlaceableFurnitureCatalog.TryCreateSizedEntry( cell.StructureDefId, out var entry ) )
				continue;

			if ( !batch.TryPlaceAtGridCell(
				     buildingRoot,
				     widthCells,
				     depthCells,
				     stories,
				     cell.Story,
				     cell.GridX,
				     cell.GridY,
				     hints,
				     entry,
				     out var wp,
				     out var rot ) )
			{
				Log.Warning(
					$"[Thorns InteriorFurniture] Scripted cell failed {buildingRoot?.Name} story={cell.Story} "
					+ $"({cell.GridX},{cell.GridY}) {cell.StructureDefId} reason={batch.LastReject}" );
				continue;
			}

			if ( FloorplanLiveTuningEnabled && Game.IsPlaying && buildingRoot.IsValid() )
			{
				var local = buildingRoot.WorldRotation.Inverse * ( wp - buildingRoot.WorldPosition );
				Log.Info(
					$"[Thorns InteriorFurniture] placed {cell.StructureDefId} story={cell.Story} "
					+ $"cell=({cell.GridX},{cell.GridY}) buildingLocal=({local.x:F0},{local.y:F0},{local.z:F0})" );
			}

			var tuneCtx = new ThornsFloorplanFurnitureTuneItem.SpawnContext(
				buildingRoot,
				widthCells,
				depthCells,
				hints.DoorSide,
				hints.DoorIndex,
				cell.Story,
				cell.GridX,
				cell.GridY );

			if ( !SpawnDecorProp( decorRoot, entry, wp, rot, tuneCtx ) )
				continue;

			spawned++;
		}

		return spawned;
	}

	static bool IsLargeStructure( string structureDefId ) =>
		!string.IsNullOrEmpty( structureDefId ) && LargeStructureIds.Contains( structureDefId );

	static void FillStoryToMinimum(
		Random rnd,
		Scene scene,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
		ThornsInteriorFurniturePlacement.Batch placementBatch,
		GameObject decorRoot,
		int story,
		int minPerFloor,
		int maxPerFloor,
		bool tightFloor,
		ref int storySpawned,
		Dictionary<string, int> countByType,
		ref int spawnedTotal,
		ThornsInteriorFurnitureScatterDebug.StoryStats floorStats )
	{
		var fillPool = tightFloor
			? ["chair", "cabinet"]
			: ThornsInteriorFurniturePlacement.CompactOnlyStructureIds;

		// Footprint validation rarely passes on fallback towers — spread anchor fill instead.
		var spreadAttempts = 0;
		var maxSpreadAttempts = Math.Max( 48, minPerFloor * 24 );

		while ( storySpawned < minPerFloor && spreadAttempts < maxSpreadAttempts )
		{
			spreadAttempts++;
			floorStats.FillAttempts++;
			var id = fillPool[rnd.Next( 0, fillPool.Length )];
			if ( !CanPlaceTypeOnStory( storySpawned, countByType, id, maxPerFloor ) )
				continue;

			if ( !ThornsPlaceableFurnitureCatalog.TryCreateSizedEntry( id, out var entry ) )
				continue;

			if ( !placementBatch.TrySelectSpreadAnchorCell(
				     rnd,
				     buildingRoot,
				     widthCells,
				     depthCells,
				     stories,
				     hints,
				     entry,
				     story,
				     out var wp,
				     out var rot,
				     relaxed: true ) )
			{
				floorStats.AddReject( placementBatch.LastReject );
				continue;
			}

			var tuneCtx = new ThornsFloorplanFurnitureTuneItem.SpawnContext(
				buildingRoot,
				widthCells,
				depthCells,
				hints.DoorSide,
				hints.DoorIndex,
				story );

			if ( !SpawnDecorProp( decorRoot, entry, wp, rot, tuneCtx ) )
			{
				floorStats.AddReject( ThornsInteriorFurnitureScatterDebug.RejectReason.ModelLoadFailed );
				continue;
			}

			placementBatch.CommitSpreadAnchorPlacement(
				buildingRoot,
				widthCells,
				depthCells,
				wp,
				rot,
				entry,
				story,
				relaxed: true );

			floorStats.SpreadFillOk++;
			RecordStoryPlacement( id, ref storySpawned, countByType, ref spawnedTotal );
		}
	}

	static bool CanPlaceTypeOnStory(
		int storySpawned,
		Dictionary<string, int> countByType,
		string structureDefId,
		int maxPerFloor )
	{
		if ( storySpawned >= maxPerFloor )
			return false;

		countByType.TryGetValue( structureDefId, out var count );
		return count < MaxPerTypePerStory;
	}

	static void RecordStoryPlacement(
		string structureDefId,
		ref int storySpawned,
		Dictionary<string, int> countByType,
		ref int spawnedTotal )
	{
		storySpawned++;
		spawnedTotal++;
		countByType.TryGetValue( structureDefId, out var count );
		countByType[structureDefId] = count + 1;
	}

	static bool TryPlaceOnStory(
		Random rnd,
		Scene scene,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
		ThornsInteriorFurniturePlacement.Batch placementBatch,
		GameObject decorRoot,
		string structureDefId,
		int story,
		bool relaxed,
		int maxPerFloor,
		ref int storySpawned,
		Dictionary<string, int> countByType,
		ref int spawnedTotal,
		ThornsInteriorFurnitureScatterDebug.StoryStats floorStats )
	{
		if ( !CanPlaceTypeOnStory( storySpawned, countByType, structureDefId, maxPerFloor ) )
			return false;

		if ( !TryPlaceStructure(
			     rnd,
			     scene,
			     buildingRoot,
			     widthCells,
			     depthCells,
			     stories,
			     hints,
			     placementBatch,
			     decorRoot,
			     structureDefId,
			     story,
			     relaxed,
			     out var reject ) )
		{
			if ( reject == ThornsInteriorFurnitureScatterDebug.RejectReason.None )
				reject = placementBatch.LastReject;
			floorStats.AddReject( reject );
			return false;
		}

		RecordStoryPlacement( structureDefId, ref storySpawned, countByType, ref spawnedTotal );
		return true;
	}

	static bool TryPlaceStructure(
		Random rnd,
		Scene scene,
		GameObject buildingRoot,
		int widthCells,
		int depthCells,
		int stories,
		in ThornsProcBuildingInteriorSample.InteriorPlacementHints hints,
		ThornsInteriorFurniturePlacement.Batch placementBatch,
		GameObject decorRoot,
		string structureDefId,
		int story,
		bool relaxed,
		out ThornsInteriorFurnitureScatterDebug.RejectReason reject )
	{
		reject = ThornsInteriorFurnitureScatterDebug.RejectReason.None;
		if ( !ThornsPlaceableFurnitureCatalog.TryCreateSizedEntry( structureDefId, out var entry ) )
		{
			reject = ThornsInteriorFurnitureScatterDebug.RejectReason.CatalogMissing;
			return false;
		}

		if ( entry.PlacementStyle == ThornsPlaceableFurnitureCatalog.InteriorPlacementStyle.CenterTile
		     && (widthCells < 3 || depthCells < 3) )
		{
			reject = ThornsInteriorFurnitureScatterDebug.RejectReason.TooSmallForCenterTile;
			return false;
		}

		var placed = ThornsPlaceableFurnitureCatalog.RequiresFlushAgainstWall( structureDefId )
			? placementBatch.TryPlaceWallFlush(
				rnd,
				scene,
				buildingRoot,
				widthCells,
				depthCells,
				stories,
				hints,
				entry,
				story,
				out var wp,
				out var rot,
				relaxed )
			: entry.PlacementStyle == ThornsPlaceableFurnitureCatalog.InteriorPlacementStyle.CenterTile
				? placementBatch.TryPlaceCenterTile(
					rnd,
					scene,
					buildingRoot,
					widthCells,
					depthCells,
					stories,
					hints,
					entry,
					story,
					out wp,
					out rot,
					relaxed )
				: placementBatch.TryPlaceWallBacked(
					rnd,
					scene,
					buildingRoot,
					widthCells,
					depthCells,
					stories,
					hints,
					entry,
					story,
					out wp,
					out rot,
					relaxed );

		if ( !placed )
		{
			reject = placementBatch.LastReject;
			return false;
		}

		var tuneCtx = new ThornsFloorplanFurnitureTuneItem.SpawnContext(
			buildingRoot,
			widthCells,
			depthCells,
			hints.DoorSide,
			hints.DoorIndex,
			story );

		if ( !SpawnDecorProp( decorRoot, entry, wp, rot, tuneCtx ) )
		{
			reject = ThornsInteriorFurnitureScatterDebug.RejectReason.ModelLoadFailed;
			return false;
		}

		return true;
	}

	static bool SpawnDecorProp(
		GameObject decorRoot,
		in ThornsPlaceableFurnitureCatalog.Entry entry,
		Vector3 worldPos,
		Rotation worldRot,
		ThornsFloorplanFurnitureTuneItem.SpawnContext tuneCtx = default )
	{
		if ( !ThornsPlaceableFurnitureCatalog.TryCreateSizedEntry( entry.StructureDefId, out var sizedEntry ) )
			return false;

		var mdl = ThornsFoliageModelCache.Load( sizedEntry.ModelPath );
		if ( !mdl.IsValid() || mdl.IsError )
		{
			Log.Warning( $"[Thorns] Interior furniture model failed: {sizedEntry.ModelPath}" );
			return false; // ModelLoadFailed tracked by caller via placement batch when applicable
		}

		var procBuildingRoot = tuneCtx.BuildingRoot.IsValid()
			? tuneCtx.BuildingRoot
			: decorRoot.IsValid() ? decorRoot.Parent : null;

		var go = new GameObject( true, $"Furniture_{sizedEntry.StructureDefId}" );
		if ( procBuildingRoot.IsValid() )
			go.SetParent( procBuildingRoot );
		else if ( decorRoot.IsValid() )
			go.SetParent( decorRoot );

		go.Tags.Add( ThornsCollisionTags.InteriorFurniture );
		go.Tags.Add( ThornsCollisionTags.Solid );
		go.Tags.Add( ThornsCollisionTags.World );

		ThornsPlaceableFurniturePresentation.Apply( go, in sizedEntry );

		if ( procBuildingRoot.IsValid() )
		{
			ThornsProcBuildingInteriorSample.SeatProcInteriorFurnitureOnBuilding(
				go,
				procBuildingRoot,
				in tuneCtx,
				in sizedEntry,
				worldRot,
				worldPos );
		}

		if ( FloorplanLiveTuningEnabled && tuneCtx.BuildingRoot.IsValid() )
		{
			go.NetworkMode = NetworkMode.Never;
			var tune = go.Components.Create<ThornsFloorplanFurnitureTuneItem>();
			tune.InitializeFromSpawn( in tuneCtx, sizedEntry.StructureDefId );
			ThornsModelMaterialUvScale.EnsureFixupOnHierarchy( go, includeChildren: false );
			return true;
		}

		var prop = go.Components.Create<ThornsInteriorFurnitureProp>();
		prop.StructureDefId = sizedEntry.StructureDefId;
		prop.WorldSizeInches = sizedEntry.WorldSizeInches;
		prop.InteriorStoryIndex = tuneCtx.Story;
		prop.SpawnGridX = tuneCtx.GridX;
		prop.SpawnGridY = tuneCtx.GridY;
		prop.SpawnWidthCells = tuneCtx.WidthCells;
		prop.SpawnDepthCells = tuneCtx.DepthCells;

		if ( Networking.IsHost
		     && ThornsFurnitureLootPolicy.ShouldSpawnProcLootContainer( sizedEntry.StructureDefId ) )
		{
			var buildingType = ThornsProcBuildingType.House;
			var layout = procBuildingRoot.IsValid()
				? ThornsProcBuildingLayoutHost.TryGet( procBuildingRoot )
				: null;
			if ( layout?.Identity is not null )
				buildingType = layout.Identity.Type;

			var container = go.Components.Create<ThornsFurnitureContainer>();
			container.HostInitializeProcLoot( sizedEntry.StructureDefId, buildingType );
		}

		if ( !ThornsNetworkReplication.TryNetworkSpawnHostOwned( go ) )
		{
			Log.Warning( $"[Thorns] Interior furniture NetworkSpawn failed: {sizedEntry.StructureDefId}" );
			return false;
		}

		if ( procBuildingRoot.IsValid() )
		{
			ThornsProcBuildingInteriorSample.SeatProcInteriorFurnitureOnBuilding(
				go,
				procBuildingRoot,
				in tuneCtx,
				in sizedEntry,
				worldRot,
				worldPos );
		}

		prop.ApplyCatalogPresentation();
		ThornsModelMaterialUvScale.EnsureFixupOnHierarchy( go, includeChildren: false );
		LogSpawnSizeCheck( go, in sizedEntry, decorRoot );

		return true;
	}

	static readonly HashSet<string> SpawnSizeLogged = new( StringComparer.OrdinalIgnoreCase );

	static void LogSpawnSizeCheck( GameObject go, in ThornsPlaceableFurnitureCatalog.Entry entry, GameObject decorRoot )
	{
		if ( !Game.IsPlaying || !Networking.IsHost )
			return;

		if ( !SpawnSizeLogged.Add( entry.StructureDefId ) )
			return;

		var model = ThornsPlaceableFurniturePresentation.LoadModel( in entry );
		var est = ThornsPlaceableFurnitureScale.EstimateWorldExtents( model, go.LocalScale, go );
		var want = entry.WorldSizeInches;
		var parentWs = decorRoot.IsValid() ? decorRoot.WorldScale : Vector3.One;
		Log.Info(
			$"[Thorns] Furniture spawn-check id={entry.StructureDefId} rev={ThornsPlaceableFurniturePresentation.CatalogRevision} " +
			$"catalogIn={want.x:F0},{want.y:F0},{want.z:F0} " +
			$"meshLocalScale={go.LocalScale} worldScale={go.WorldScale} estExtents={est} parentWorldScale={parentWs}" );

		if ( want.LengthSquared >= 1f && est.LengthSquared >= 1f )
		{
			var bad =
				MathF.Abs( est.x - want.x ) > want.x * 0.12f
				|| MathF.Abs( est.y - want.y ) > want.y * 0.12f
				|| MathF.Abs( est.z - want.z ) > want.z * 0.12f;
			if ( bad )
			{
				Log.Warning(
					$"[Thorns] Furniture size mismatch id={entry.StructureDefId}: catalog {want} vs measured {est} — check parent scale or model bounds." );
			}
		}
	}

	/// <summary>Reapply catalog scale on all interior furniture (host drives synced sizes; clients re-apply in prop OnStart).</summary>
	public static int RefreshAllInteriorFurnitureScales( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return 0;

		var count = 0;
		foreach ( var prop in scene.GetAllComponents<ThornsInteriorFurnitureProp>() )
		{
			if ( prop is null || !prop.IsValid() || !prop.GameObject.IsValid() )
				continue;

			prop.ApplyCatalogPresentation();
			count++;
		}

		return count;
	}
}
