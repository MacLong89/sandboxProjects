namespace Sandbox;

/// <summary>Phase 8 — spawns procedural building scene hierarchy (floors, walls, ramps, POI marker).</summary>
public static class ThornsProcBuildingSceneSpawner
{
	public static int Spawn(
		ThornsWorldGenerationHostBridge host,
		ThornsWorldGenerationContext context,
		Vector3 worldPos,
		Rotation worldRot,
		ThornsProcBuildingLayout layout,
		int tier,
		bool destroyed,
		ThornsWorldSettlementKind settlementKind,
		int buildingIndex,
		string materialSlug = null )
	{
		var (_, pieces) = SpawnCore(
			context.PlacementRng,
			worldPos,
			worldRot,
			layout,
			tier,
			destroyed,
			settlementKind,
			buildingIndex,
			host.DebugBuildingTypeColors,
			materialSlug,
			registerLootAndNpc: true,
			buildingsForLoot: host.BuildingsForLoot,
			createPoiMarker: true,
			replicateToClients: true );
		return pieces;
	}

	/// <summary>Dev/test spawn without world-gen host — returns building root with layout host attached.</summary>
	public static GameObject SpawnStandalone(
		Random placementRng,
		Vector3 worldPos,
		Rotation worldRot,
		ThornsProcBuildingLayout layout,
		int tier = 0,
		bool destroyed = false,
		string materialSlug = null,
		int buildingIndex = 0,
		bool debugBuildingTypeColors = false )
	{
		var (root, pieces) = SpawnCore(
			placementRng,
			worldPos,
			worldRot,
			layout,
			tier,
			destroyed,
			ThornsWorldSettlementKind.Isolated,
			buildingIndex,
			debugBuildingTypeColors,
			materialSlug,
			registerLootAndNpc: false,
			buildingsForLoot: null,
			createPoiMarker: false,
			replicateToClients: false );
		if ( pieces <= 0 )
			return null;

		return root;
	}

	static (GameObject Root, int SpawnedPieces) SpawnCore(
		Random rnd,
		Vector3 worldPos,
		Rotation worldRot,
		ThornsProcBuildingLayout layout,
		int tier,
		bool destroyed,
		ThornsWorldSettlementKind settlementKind,
		int buildingIndex,
		bool useTypeDebugColors,
		string materialSlug,
		bool registerLootAndNpc,
		List<ThornsWorldGenProcBuildingInteriorLoot> buildingsForLoot,
		bool createPoiMarker,
		bool replicateToClients )
	{
		// Perimeter holes only for authored ruin variants — not the generic world-gen "destroyed" tier flag.
		var visualRuin = layout.Identity?.IsRuinVariant ?? false;
		var widthCells = layout.WidthCells;
		var depthCells = layout.DepthCells;
		var stories = layout.Stories;
		var doorSide = layout.DoorSide;
		var doorIndexGround = layout.DoorIndex;
		var cell = ThornsBuildingModule.Cell;
		var floorT = ThornsBuildingModule.FloorThickness;
		var wallH = ThornsBuildingModule.WallHeight;
		var storyHeight = wallH + floorT;
		var spawnedPieces = 0;
		var buildingType = layout.Identity?.Type ?? ThornsProcBuildingType.House;

		var root = new GameObject( true, $"ThornsProcBuilding_{buildingIndex}" );
		root.WorldPosition = worldPos;
		root.WorldRotation = worldRot;
		root.Tags.Add( "thorns_procedural_site" );
		root.Tags.Add( "thorns_proc_building" );

		var layoutHost = root.Components.Create<ThornsProcBuildingLayoutHost>();
		layoutHost.Layout = layout;
		layoutHost.MaterialSlug = string.IsNullOrEmpty( materialSlug )
			? ThornsProcBuildingMaterialPalette.SlugFromIndex( Math.Clamp( tier, 0, 2 ) )
			: materialSlug;
		var materialIndex = ThornsProcBuildingMaterialPalette.IndexOfSlug( layoutHost.MaterialSlug );

		var decorRoot = new GameObject( true, ThornsInteriorFurnitureScatter.DecorParentName );
		decorRoot.SetParent( root );
		decorRoot.LocalPosition = Vector3.Zero;
		decorRoot.LocalRotation = Rotation.Identity;
		decorRoot.LocalScale = Vector3.One;

		float Lx( int x ) => layout.GridAxisLocalX( x );
		float Ly( int y ) => layout.GridAxisLocalY( y );

		bool HasShell( int s, int x, int y ) => layout.HasPerimeterShellCell( s, x, y );

		bool IsOutsideShell( int s, int x, int y ) =>
			x < 0 || x >= widthCells || y < 0 || y >= depthCells || !HasShell( s, x, y );

		bool IsGroundDoorEdge( int side, int x, int y ) =>
			side switch
			{
				0 => doorSide == 0 && x == doorIndexGround && IsOutsideShell( 0, x, y - 1 ),
				2 => doorSide == 2 && x == doorIndexGround && IsOutsideShell( 0, x, y + 1 ),
				3 => doorSide == 3 && y == doorIndexGround && IsOutsideShell( 0, x - 1, y ),
				1 => doorSide == 1 && y == doorIndexGround && IsOutsideShell( 0, x + 1, y ),
				_ => false
			};

		string WallDef( int s, int side, int x, int y )
		{
			var primaryDoor = s == 0 && IsGroundDoorEdge( side, x, y );
			if ( layout.Identity?.Facade is { } facade )
				return facade.PickWallDef( rnd, s, side, x, y, primaryDoor );

			return primaryDoor
				? "wood_doorframe"
				: rnd.NextDouble() < 0.2 ? "wood_window" : "wood_wall";
		}

		void SpawnPiece(
			string defId,
			Vector3 localPos,
			Rotation localRot,
			float verticalSpanWorld = -1f,
			Vector3? scaleOverride = null )
		{
			var go = new GameObject( true, $"Proc_{materialIndex:D2}_{defId}" );
			go.SetParent( root );
			go.LocalPosition = localPos;
			go.LocalRotation = localRot;
			var scale = scaleOverride ?? ThornsBuildingVisuals.StructureLocalScale( defId );
			if ( scaleOverride is null
			     && verticalSpanWorld > 0.01f
			     && defId is "wood_wall" or "wood_window" or "wood_doorframe" )
			{
				scale = scale with { z = scale.z * (verticalSpanWorld / wallH) };
				scale = ThornsBuildingVisuals.PerimeterShellWallLocalScale( scale, localRot.Angles().yaw );
			}

			go.LocalScale = scale;
			ThornsAnchoredWorldPhysics.EnsureWorldSolidTags( go );
			go.Tags.Add( ThornsCollisionTags.Structure );
			var mr = go.Components.Create<ModelRenderer>();
			var model = ThornsBuildingVisuals.StructureModel( defId, layoutHost.MaterialSlug );
			mr.Model = model;
			mr.Tint = useTypeDebugColors
				? ThornsProcBuildingTypeDebugColors.StructureTint( buildingType )
				: Color.White;
			if ( !useTypeDebugColors )
			{
				var pieceMat = ThornsBuildingVisuals.ResolveProcBuildingPieceMaterial( defId, layoutHost.MaterialSlug );
				if ( pieceMat.IsValid() )
					mr.MaterialOverride = pieceMat;
			}

			var mc = go.Components.GetOrCreate<ModelCollider>();
			mc.Model = model;
			mc.Static = true;
			mc.IsTrigger = false;
			mc.Enabled = true;
			_ = go.Components.Create<ThornsProcBuildingPieceFixup>();
			spawnedPieces++;
		}

		for ( var s = 0; s < stories; s++ )
		for ( var x = 0; x < widthCells; x++ )
		for ( var y = 0; y < depthCells; y++ )
		{
			if ( !layout.IsCellOccupied( s, x, y ) )
				continue;
			if ( layout.CellNeedsRampShaftUpperCutAt( s, x, y ) )
				continue;

			SpawnPiece( "wood_foundation", new Vector3( Lx( x ), Ly( y ), s * storyHeight ), Rotation.Identity );
		}

		var topStory = stories - 1;
		for ( var x = 0; x < widthCells; x++ )
		for ( var y = 0; y < depthCells; y++ )
		{
			if ( !HasShell( topStory, x, y ) )
				continue;

			SpawnPiece( "wood_foundation", new Vector3( Lx( x ), Ly( y ), stories * storyHeight ), Rotation.Identity );
		}

		var perimeterWallSpan = ThornsBuildingModule.ProcPerimeterWallSpanWorld( stories );
		for ( var s = 0; s < stories; s++ )
		{
			var z = ThornsBuildingModule.ProcPerimeterWallCenterLocalZ( s, stories );
			for ( var x = 0; x < widthCells; x++ )
			for ( var y = 0; y < depthCells; y++ )
			{
				if ( !HasShell( s, x, y ) )
					continue;

				if ( IsOutsideShell( s, x, y - 1 ) )
				{
					var def = WallDef( s, 0, x, y );
					if ( def is not null && ( !visualRuin || rnd.NextDouble() > 0.12 ) )
						SpawnPiece( def, new Vector3( Lx( x ), Ly( y ) - cell * 0.5f, z ), Rotation.FromYaw( 90f ), perimeterWallSpan );
				}

				if ( IsOutsideShell( s, x, y + 1 ) )
				{
					var def = WallDef( s, 2, x, y );
					if ( def is not null && ( !visualRuin || rnd.NextDouble() > 0.12 ) )
						SpawnPiece( def, new Vector3( Lx( x ), Ly( y ) + cell * 0.5f, z ), Rotation.FromYaw( 270f ), perimeterWallSpan );
				}

				if ( IsOutsideShell( s, x - 1, y ) )
				{
					var def = WallDef( s, 3, x, y );
					if ( def is not null && ( !visualRuin || rnd.NextDouble() > 0.12 ) )
						SpawnPiece( def, new Vector3( Lx( x ) - cell * 0.5f, Ly( y ), z ), Rotation.FromYaw( 180f ), perimeterWallSpan );
				}

				if ( IsOutsideShell( s, x + 1, y ) )
				{
					var def = WallDef( s, 1, x, y );
					if ( def is not null && ( !visualRuin || rnd.NextDouble() > 0.12 ) )
						SpawnPiece( def, new Vector3( Lx( x ) + cell * 0.5f, Ly( y ), z ), Rotation.FromYaw( 0f ), perimeterWallSpan );
				}
			}

		}

		bool HasExteriorFace( int story, int side, int x, int y )
		{
			if ( !HasShell( story, x, y ) )
				return false;

			return side switch
			{
				0 => IsOutsideShell( story, x, y - 1 ),
				2 => IsOutsideShell( story, x, y + 1 ),
				3 => IsOutsideShell( story, x - 1, y ),
				1 => IsOutsideShell( story, x + 1, y ),
				_ => false
			};
		}

		bool HasExteriorFaceAnyStory( int side, int x, int y )
		{
			for ( var st = 0; st < stories; st++ )
			{
				if ( HasExteriorFace( st, side, x, y ) )
					return true;
			}

			return false;
		}

		bool HasShellAnyStory( int x, int y )
		{
			for ( var st = 0; st < stories; st++ )
			{
				if ( HasShell( st, x, y ) )
					return true;
			}

			return false;
		}

		var walls = layout.InteriorWalls;
		for ( var s = 0; s < stories; s++ )
		{
			var z = ThornsBuildingModule.ProcPerimeterWallCenterLocalZ( s, stories );
			for ( var x = 0; x < widthCells; x++ )
			for ( var y = 0; y < depthCells; y++ )
			{
				if ( x + 1 < widthCells && walls.HasInteriorWallEast( s, x, y ) )
				{
					if ( !destroyed || rnd.NextDouble() > 0.22 )
						SpawnPiece( "wood_wall", new Vector3( Lx( x ) + cell * 0.5f, Ly( y ), z ), Rotation.Identity, perimeterWallSpan );
				}

				if ( y + 1 < depthCells && walls.HasInteriorWallNorth( s, x, y ) )
				{
					if ( !destroyed || rnd.NextDouble() > 0.22 )
						SpawnPiece( "wood_wall", new Vector3( Lx( x ), Ly( y ) + cell * 0.5f, z ), Rotation.FromYaw( 90f ), perimeterWallSpan );
				}
			}
		}

		for ( var rampStory = 0; rampStory < stories - 1; rampStory++ )
		{
			var rz = ThornsBuildingModule.ProcPerimeterWallCenterLocalZ( rampStory, stories );
			foreach ( var ramp in layout.GetRampsOnStory( rampStory ) )
			{
				if ( !layout.IsCellOccupied( rampStory, ramp.X, ramp.Y ) )
					continue;

				if ( rampStory > 0 )
				{
					var stacked = false;
					foreach ( var below in layout.GetRampsOnStory( rampStory - 1 ) )
					{
						ThornsProcTileRampHeadroom.GetRiseDelta( below.Direction, out var riseDx, out var riseDy );
						var headX = below.X + riseDx;
						var headY = below.Y + riseDy;
						if ( ( ramp.X == below.X && ramp.Y == below.Y )
						     || ( ramp.X == headX && ramp.Y == headY ) )
						{
							stacked = true;
							break;
						}
					}

					if ( stacked )
						continue;
				}

				var yaw = ramp.Direction != ThornsProcRampDirection.None
					? ThornsProcBuildingRampGeometry.YawFromRampDirection( ramp.Direction )
					: layout.GetRampYawDegrees( rampStory );
				yaw += 90f;

				SpawnPiece(
					"wood_ramp",
					ThornsProcBuildingRampGeometry.GetRampSpawnLocalPosition( layout, ramp, rz ),
					Rotation.FromYaw( yaw ) );
			}
		}

		if ( spawnedPieces <= 0 )
		{
			Log.Warning(
				$"[Thorns] Proc building produced 0 pieces (type={buildingType} index={buildingIndex} {widthCells}x{depthCells}x{stories}) — discarding empty site." );
			root.Destroy();
			return (null, 0);
		}

		var spawnTrim = CreatePerimeterTrimSpawner(
			root,
			materialIndex,
			layoutHost.MaterialSlug,
			rnd,
			visualRuin,
			useTypeDebugColors,
			buildingType );
		void TrySpawnTrim( Vector3 localPos, float yawDeg, Vector3 trimScale, bool isHorizontalBand = false )
		{
			spawnTrim( localPos, yawDeg, trimScale, isHorizontalBand );
			spawnedPieces++;
		}

		var trimCount = SpawnPerimeterTrimsOnLayout(
			layout,
			stories,
			perimeterWallSpan,
			cell,
			widthCells,
			depthCells,
			Lx,
			Ly,
			HasExteriorFaceAnyStory,
			HasShellAnyStory,
			TrySpawnTrim );

		if ( trimCount > 0 )
		{
			Log.Info(
				$"[Thorns ProcBuilding] {trimCount} perimeter trim(s) on {buildingType} #{buildingIndex} "
				+ $"mat={layoutHost.MaterialSlug} {widthCells}x{depthCells}x{stories}" );
		}
		else
		{
			Log.Warning(
				$"[Thorns ProcBuilding] 0 perimeter trims on {buildingType} #{buildingIndex} "
				+ $"({widthCells}x{depthCells}x{stories}) — corner pillars missing?" );
		}

		if ( replicateToClients )
		{
			if ( Networking.IsActive
			     && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( root ) )
			{
				Log.Warning(
					$"[Thorns] Proc building NetworkSpawn failed (type={buildingType} index={buildingIndex} pieces={spawnedPieces}) — site may be invisible to joiners." );
			}
		}
		else
			ThornsNetworkReplication.SetSubtreeNetworkModeNever( root );

		if ( registerLootAndNpc && buildingsForLoot is not null )
		{
			buildingsForLoot.Add(
				new ThornsWorldGenProcBuildingInteriorLoot(
					root,
					widthCells,
					depthCells,
					stories,
					tier,
					buildingType,
					layoutHost.MaterialSlug,
					doorSide,
					doorIndexGround ) );
			ThornsProcBuildingNpcRegistry.HostRegister( root, widthCells, depthCells, stories, tier );
		}

		if ( !createPoiMarker )
			return (root, spawnedPieces);

		var marker = root.Components.Create<ThornsPoiMarker>();
		marker.ShowOnMinimap = true;
		var typeLabel = ThornsProcBuildingTypeDebugColors.ShortLabel( buildingType );
		marker.DisplayName = useTypeDebugColors
			? $"{typeLabel} ({stories}F)"
			: layout.Identity?.DisplayName ?? ThornsProcBuildingLayout.DisplayNameFor( stories, visualRuin );
		var poiMaterialTier = (ThornsBuildingMaterialTier)Math.Clamp( tier, 0, 2 );
		marker.CategoryKey = settlementKind switch
		{
			ThornsWorldSettlementKind.MainCity => "world_gen_main_city",
			ThornsWorldSettlementKind.Town => "world_gen_town",
			ThornsWorldSettlementKind.Isolated => layout.Identity is not null
				? $"world_gen_{layout.Identity.Type.ToString().ToLowerInvariant()}_{ThornsProcBuildingMaterialAffinity.MaterialTierKey( poiMaterialTier )}"
				: "world_gen_wilderness",
			_ => layout.Identity is not null
				? $"world_gen_{layout.Identity.Type.ToString().ToLowerInvariant()}_{ThornsProcBuildingMaterialAffinity.MaterialTierKey( poiMaterialTier )}"
				: "world_gen_building"
		};
		marker.MinimapColor = useTypeDebugColors
			? ThornsProcBuildingTypeDebugColors.MinimapColor( buildingType )
			: settlementKind switch
			{
				ThornsWorldSettlementKind.MainCity => poiMaterialTier switch
				{
					ThornsBuildingMaterialTier.Metal => new Color( 0.45f, 0.88f, 1f, 1f ),
					ThornsBuildingMaterialTier.Stone => new Color( 0.78f, 0.78f, 0.82f, 1f ),
					_ => new Color( 0.82f, 0.66f, 0.46f, 1f )
				},
				ThornsWorldSettlementKind.Town => poiMaterialTier switch
				{
					ThornsBuildingMaterialTier.Stone => new Color( 0.7f, 0.7f, 0.7f, 0.95f ),
					ThornsBuildingMaterialTier.Metal => new Color( 0.55f, 0.62f, 0.7f, 0.95f ),
					_ => new Color( 0.7f, 0.55f, 0.38f, 0.95f )
				},
				_ => poiMaterialTier switch
				{
					ThornsBuildingMaterialTier.Stone => new Color( 0.72f, 0.72f, 0.72f, 0.9f ),
					ThornsBuildingMaterialTier.Metal => new Color( 0.58f, 0.64f, 0.72f, 0.9f ),
					_ => new Color( 0.72f, 0.56f, 0.4f, 0.9f )
				}
			};
		var blipBase = settlementKind == ThornsWorldSettlementKind.MainCity ? 2f : 0f;
		marker.MinimapBlipDiameterPx = blipBase + stories switch
		{
			>= 8 => 20f,
			>= 5 => 16f,
			4 => 14f,
			>= 2 => 12f,
			_ => 10f
		};

		return (root, spawnedPieces);
	}

	/// <summary>
	/// Adds corner pillars and panel-seam trims on an existing proc building (e.g. after hotload or an older save).
	/// Removes prior <c>thorns_proc_building_trim</c> children first. No-op when layout host is missing.
	/// </summary>
	/// <returns>Trim pieces spawned (corners + wall seams).</returns>
	public static int RefreshPerimeterTrims( GameObject buildingRoot, Random placementRng = null )
	{
		if ( buildingRoot is null or not { IsValid: true } )
			return 0;

		var host = buildingRoot.Components.Get<ThornsProcBuildingLayoutHost>( FindMode.EnabledInSelf );
		if ( host?.Layout is not { } layout )
			return 0;

		foreach ( var ch in buildingRoot.Children.ToArray() )
		{
			if ( ch.IsValid() && ch.Tags.Has( "thorns_proc_building_trim" ) )
				ch.Destroy();
		}

		var materialSlug = string.IsNullOrEmpty( host.MaterialSlug )
			? ThornsProcBuildingMaterialPalette.AllSlugs[0]
			: host.MaterialSlug;
		var materialIndex = ThornsProcBuildingMaterialPalette.IndexOfSlug( materialSlug );
		var visualRuin = layout.Identity?.IsRuinVariant ?? false;
		var rnd = placementRng ?? new Random( HashCode.Combine( buildingRoot.Id, layout.GetHashCode() ) );
		var stories = layout.Stories;
		var perimeterWallSpan = ThornsBuildingModule.ProcPerimeterWallSpanWorld( stories );
		var cell = ThornsBuildingModule.Cell;
		var widthCells = layout.WidthCells;
		var depthCells = layout.DepthCells;

		float Lx( int x ) => layout.GridAxisLocalX( x );
		float Ly( int y ) => layout.GridAxisLocalY( y );

		bool HasShell( int s, int x, int y ) => layout.HasPerimeterShellCell( s, x, y );

		bool IsOutsideShell( int s, int x, int y ) =>
			x < 0 || x >= widthCells || y < 0 || y >= depthCells || !HasShell( s, x, y );

		bool HasExteriorFace( int story, int side, int x, int y )
		{
			if ( !HasShell( story, x, y ) )
				return false;

			return side switch
			{
				0 => IsOutsideShell( story, x, y - 1 ),
				2 => IsOutsideShell( story, x, y + 1 ),
				3 => IsOutsideShell( story, x - 1, y ),
				1 => IsOutsideShell( story, x + 1, y ),
				_ => false
			};
		}

		bool HasExteriorFaceAnyStory( int side, int x, int y )
		{
			for ( var st = 0; st < stories; st++ )
			{
				if ( HasExteriorFace( st, side, x, y ) )
					return true;
			}

			return false;
		}

		bool HasShellAnyStory( int x, int y )
		{
			for ( var st = 0; st < stories; st++ )
			{
				if ( HasShell( st, x, y ) )
					return true;
			}

			return false;
		}

		var trySpawnTrim = CreatePerimeterTrimSpawner(
			buildingRoot,
			materialIndex,
			materialSlug,
			rnd,
			visualRuin,
			useTypeDebugColors: false,
			layout.Identity?.Type ?? ThornsProcBuildingType.House );

		return SpawnPerimeterTrimsOnLayout(
			layout,
			stories,
			perimeterWallSpan,
			cell,
			widthCells,
			depthCells,
			Lx,
			Ly,
			HasExteriorFaceAnyStory,
			HasShellAnyStory,
			trySpawnTrim );
	}

	static Action<Vector3, float, Vector3, bool> CreatePerimeterTrimSpawner(
		GameObject buildingRoot,
		int materialIndex,
		string materialSlug,
		Random rnd,
		bool visualRuin,
		bool useTypeDebugColors,
		ThornsProcBuildingType buildingType )
	{
		return ( localPos, yawDeg, trimScale, isHorizontalBand ) =>
		{
			if ( visualRuin && rnd.NextDouble() <= 0.12 )
				return;

			SpawnPerimeterTrimPiece(
				buildingRoot,
				materialIndex,
				materialSlug,
				localPos,
				yawDeg,
				trimScale,
				useTypeDebugColors,
				buildingType,
				isHorizontalBand );
		};
	}

	static void SpawnPerimeterTrimPiece(
		GameObject buildingRoot,
		int materialIndex,
		string materialSlug,
		Vector3 localPos,
		float yawDeg,
		Vector3 trimScale,
		bool useTypeDebugColors,
		ThornsProcBuildingType buildingType,
		bool isHorizontalBand = false )
	{
		if ( buildingRoot is not { IsValid: true } )
			return;

		var oriented = ThornsBuildingVisuals.AlignPerimeterTrimFoundationScaleToYaw( trimScale, yawDeg );
		const string trimDefId = "wood_perimeter_trim";
		var go = new GameObject( true, $"Proc_{materialIndex:D2}_{trimDefId}" );
		go.SetParent( buildingRoot );
		go.LocalPosition = localPos;
		go.LocalRotation = Rotation.FromYaw( yawDeg );
		go.LocalScale = oriented;
		go.NetworkMode = NetworkMode.Never;
		go.Tags.Add( "thorns_proc_building_trim" );
		if ( isHorizontalBand )
			go.Tags.Add( "thorns_proc_building_trim_band" );
		ThornsAnchoredWorldPhysics.EnsureWorldSolidTags( go );
		go.Tags.Add( ThornsCollisionTags.Structure );
		var mr = go.Components.Create<ModelRenderer>();
		var model = ThornsBuildingVisuals.StructureModel( trimDefId, materialSlug );
		if ( !model.IsValid() || model.IsError )
		{
			Log.Warning( $"[Thorns ProcBuilding] perimeter trim model failed mat={materialSlug}" );
			go.Destroy();
			return;
		}

		mr.Enabled = true;
		mr.Model = model;
		mr.Tint = useTypeDebugColors
			? ThornsProcBuildingTypeDebugColors.StructureTint( buildingType )
			: Color.White;
		ThornsBuildingVisuals.ApplyProcBuildingPieceUvScale( mr, go, materialSlug, trimDefId );
		var mc = go.Components.GetOrCreate<ModelCollider>();
		mc.Model = model;
		mc.Static = true;
		mc.IsTrigger = false;
		mc.Enabled = true;
		_ = go.Components.Create<ThornsProcBuildingPieceFixup>();
	}

	static int SpawnPerimeterTrimsOnLayout(
		ThornsProcBuildingLayout layout,
		int storyCount,
		float perStoryWallSpan,
		float cell,
		int widthCells,
		int depthCells,
		Func<int, float> lx,
		Func<int, float> ly,
		Func<int, int, int, bool> hasExteriorFaceAnyStory,
		Func<int, int, bool> hasShellAnyStory,
		Action<Vector3, float, Vector3, bool> trySpawnTrim )
	{
		var spawned = 0;
		void SpawnTrim( Vector3 localPos, float yawDeg, Vector3 trimScale, bool isHorizontalBand = false )
		{
			trySpawnTrim( localPos, yawDeg, trimScale, isHorizontalBand );
			spawned++;
		}

		var half = cell * 0.5f;
		ThornsBuildingModule.ProcPerimeterShellFullExtent( storyCount, out var shellCenterZ, out var shellSpan );
		if ( shellSpan < 0.01f )
			shellSpan = perStoryWallSpan > 0.01f ? perStoryWallSpan : ThornsBuildingModule.WallHeight;

		var cornerSize = ThornsBuildingModule.ProcPerimeterTrimCornerSizeWorld;
		var seamRun = ThornsBuildingModule.ProcPerimeterTrimSeamRunWorld;
		var seamDepth = ThornsBuildingModule.ProcPerimeterTrimSeamDepthWorld;
		var cornerScale = ThornsBuildingVisuals.PerimeterTrimFoundationScale( cornerSize, cornerSize, shellSpan );
		var seamScale = ThornsBuildingVisuals.PerimeterTrimFoundationScale( seamRun, seamDepth, shellSpan );
		var bandHeight = ThornsBuildingModule.ProcPerimeterBandTrimHeightWorld;
		Span<float> bandCenterZ = stackalloc float[12];
		var bandLevelCount = ThornsBuildingModule.CollectPerimeterBandTrimCenterZ( storyCount, bandCenterZ );
		var widthRun = widthCells * cell + ThornsBuildingModule.WallThickness;
		var depthRun = depthCells * cell + ThornsBuildingModule.WallThickness;
		// Match wall axis order: thin×run×height → ScaleBoxToWorldAxes( T, run, bandH ) at wall yaw.
		var bandScaleNorthSouth = ThornsBuildingVisuals.PerimeterBandTrimScale( widthRun, seamDepth, bandHeight );
		var bandScaleEastWest = ThornsBuildingVisuals.PerimeterBandTrimScale( seamDepth, depthRun, bandHeight );

		static float PerimeterWallYaw( int side ) =>
			side switch
			{
				0 => 90f,
				1 => 0f,
				2 => 270f,
				3 => 180f,
				_ => 0f
			};

		// Wall centerlines — match perimeter wall spawn (Lx ± cell*0.5, Ly ± cell*0.5).
		var westWallX = lx( 0 ) - half;
		var eastWallX = lx( widthCells - 1 ) + half;
		var southWallY = ly( 0 ) - half;
		var northWallY = ly( depthCells - 1 ) + half;

		void SpawnFootprintCorner( float wallIntersectX, float wallIntersectY, int sideA, int sideB, float yaw )
		{
			var pos = ThornsBuildingModule.ProcPerimeterCornerTrimLocalPosition(
				wallIntersectX,
				wallIntersectY,
				shellCenterZ,
				sideA,
				sideB );
			SpawnTrim( pos, yaw, cornerScale );
		}

		if ( widthCells > 0 && depthCells > 0 )
		{
			// Square corner posts — yaw 0 so run/depth are not swapped onto the wall plane.
			SpawnFootprintCorner( westWallX, southWallY, 0, 3, 0f );
			SpawnFootprintCorner( eastWallX, southWallY, 0, 1, 0f );
			SpawnFootprintCorner( westWallX, northWallY, 2, 3, 0f );
			SpawnFootprintCorner( eastWallX, northWallY, 2, 1, 0f );
		}

		for ( var x = 0; x < widthCells; x++ )
		for ( var y = 0; y < depthCells; y++ )
		{
			if ( !hasShellAnyStory( x, y ) )
				continue;

			if ( x + 1 < widthCells
			     && ThornsBuildingModule.IsInteriorPerimeterPanelSeam( x, widthCells )
			     && hasExteriorFaceAnyStory( 0, x, y )
			     && hasExteriorFaceAnyStory( 0, x + 1, y ) )
			{
				var pos = ThornsBuildingModule.ProcPerimeterSeamTrimLocalPosition(
					lx( x ) + half,
					ly( y ) - half,
					shellCenterZ,
					0 );
				SpawnTrim( pos, PerimeterWallYaw( 0 ), seamScale );
			}

			if ( x + 1 < widthCells
			     && ThornsBuildingModule.IsInteriorPerimeterPanelSeam( x, widthCells )
			     && hasExteriorFaceAnyStory( 2, x, y )
			     && hasExteriorFaceAnyStory( 2, x + 1, y ) )
			{
				var pos = ThornsBuildingModule.ProcPerimeterSeamTrimLocalPosition(
					lx( x ) + half,
					ly( y ) + half,
					shellCenterZ,
					2 );
				SpawnTrim( pos, PerimeterWallYaw( 2 ), seamScale );
			}

			if ( y + 1 < depthCells
			     && ThornsBuildingModule.IsInteriorPerimeterPanelSeam( y, depthCells )
			     && hasExteriorFaceAnyStory( 3, x, y )
			     && hasExteriorFaceAnyStory( 3, x, y + 1 ) )
			{
				var pos = ThornsBuildingModule.ProcPerimeterSeamTrimLocalPosition(
					lx( x ) - half,
					ly( y ) + half,
					shellCenterZ,
					3 );
				SpawnTrim( pos, PerimeterWallYaw( 3 ), seamScale );
			}

			if ( y + 1 < depthCells
			     && ThornsBuildingModule.IsInteriorPerimeterPanelSeam( y, depthCells )
			     && hasExteriorFaceAnyStory( 1, x, y )
			     && hasExteriorFaceAnyStory( 1, x, y + 1 ) )
			{
				var pos = ThornsBuildingModule.ProcPerimeterSeamTrimLocalPosition(
					lx( x ) + half,
					ly( y ) + half,
					shellCenterZ,
					1 );
				SpawnTrim( pos, PerimeterWallYaw( 1 ), seamScale );
			}
		}

		var centerX = ( lx( 0 ) + lx( widthCells - 1 ) ) * 0.5f;
		var centerY = ( ly( 0 ) + ly( depthCells - 1 ) ) * 0.5f;

		for ( var bi = 0; bi < bandLevelCount; bi++ )
		{
			var bandZ = bandCenterZ[bi];

			var southPos = ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition(
				centerX,
				southWallY,
				bandZ,
				0 );
			SpawnTrim( southPos, PerimeterWallYaw( 0 ), bandScaleNorthSouth, isHorizontalBand: true );

			var northPos = ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition(
				centerX,
				northWallY,
				bandZ,
				2 );
			SpawnTrim( northPos, PerimeterWallYaw( 2 ), bandScaleNorthSouth, isHorizontalBand: true );

			var westPos = ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition(
				centerY,
				westWallX,
				bandZ,
				3 );
			SpawnTrim( westPos, PerimeterWallYaw( 3 ), bandScaleEastWest, isHorizontalBand: true );

			var eastPos = ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition(
				centerY,
				eastWallX,
				bandZ,
				1 );
			SpawnTrim( eastPos, PerimeterWallYaw( 1 ), bandScaleEastWest, isHorizontalBand: true );
		}

		return spawned;
	}
}
