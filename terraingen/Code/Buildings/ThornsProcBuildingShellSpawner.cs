namespace Terraingen.Buildings;

using Terraingen;
using Terraingen.Physics;
using Terraingen.Rendering;

/// <summary>Spawns a 3×3 proc building shell with ramps, trims, and scripted interior furniture.</summary>
public sealed class ThornsProcBuildingShellSpawner
{
	readonly Scene _scene;
	readonly Model _boxModel;
	readonly bool _debugLogging;
	readonly int _maxFurnitureDebugLogs;
	readonly Dictionary<string, Material> _materials = new( StringComparer.OrdinalIgnoreCase );
	Material _fallbackMaterial;
	int _spawnedFurniture;
	int _furnitureModelFallbacks;
	int _furnitureDebugLogs;

	public ThornsProcBuildingShellSpawner(
		Scene scene,
		string devBoxModel = "models/dev/box.vmdl",
		bool debugLogging = false,
		int maxFurnitureDebugLogs = 12 )
	{
		_scene = scene;
		_debugLogging = debugLogging;
		_maxFurnitureDebugLogs = maxFurnitureDebugLogs;
		var box = ThornsProcBoxMesh.GetOrCreate();
		_boxModel = box.IsValid && !box.IsError ? box : Model.Load( devBoxModel );
	}

	public int SpawnedFurniture => _spawnedFurniture;
	public int FurnitureModelFallbacks => _furnitureModelFallbacks;

	public void ResetCounters()
	{
		_spawnedFurniture = 0;
		_furnitureModelFallbacks = 0;
		_furnitureDebugLogs = 0;
	}

	public readonly record struct Request(
		Vector3 WorldPosition,
		Rotation WorldRotation,
		GameObject Parent,
		ThornsProcBuildingType BuildingType,
		int VariantIndex,
		int Stories,
		int BuildingIndex,
		float FoundationDepth,
		string Name = null,
		bool RegisterFootprint = true,
		int WidthCells = ThornsProcBuildingInterior.GridCells,
		int DepthCells = ThornsProcBuildingInterior.GridCells );

	public readonly record struct Result( GameObject Root, int PropsSpawned );

	public Result Spawn( in Request request, ThornsBuildingLootWorldService lootService, Random rng )
	{
		var stories = Math.Clamp( request.Stories, 1, ThornsProcBuildingSpawnDefaults.MaxStories );
		var root = _scene.CreateObject( true );
		root.Name = string.IsNullOrWhiteSpace( request.Name )
			? $"ProcBuilding_{request.BuildingType}"
			: request.Name;
		root.Parent = request.Parent;
		root.WorldPosition = request.WorldPosition;
		root.WorldRotation = request.WorldRotation;

		var widthCells = Math.Max( 1, request.WidthCells );
		var depthCells = Math.Max( 1, request.DepthCells );

		if ( request.RegisterFootprint )
			ThornsProcBuildingFootprintRegistry.Register( request.WorldPosition, request.WorldRotation, widthCells, depthCells );

		var palette = ThornsProcBuildingMaterialCatalog.Resolve( request.BuildingType, request.BuildingIndex );
		var wallMaterialSlug = palette.Wall;
		var wallMaterial = LoadMaterial( wallMaterialSlug );
		var floorMaterial = LoadMaterial( palette.Floor );
		var trimSlug = palette.Trim;
		var trimMaterial = LoadMaterial( trimSlug );
		var roofMaterial = LoadMaterial( palette.RoofSlug );
		var foundationMaterial = LoadMaterial( palette.FoundationSlug );
		var rampSlug = palette.Ramp;

		var foundationWidth = ThornsProcBuildingFootprintCatalog.ExteriorWidthInches( widthCells );
		var foundationDepthInches = ThornsProcBuildingFootprintCatalog.ExteriorDepthInches( depthCells );
		SpawnBox(
			root,
			"foundation",
			new Vector3( 0f, 0f, -request.FoundationDepth * 0.5f ),
			new Vector3( foundationWidth, foundationDepthInches, request.FoundationDepth ),
			foundationMaterial,
			solid: false );

		var rampsByStory = ThornsProcBuildingRampPlanner.BuildCompact3x3SwitchbackRampsByStory( stories );
		ThornsProcBuildingRampPlanner.MapRampsToFootprint( rampsByStory, widthCells, depthCells );
		var skipFloor = new bool[stories, widthCells, depthCells];
		ThornsProcTileRampHeadroom.MarkFloorCuts( skipFloor, rampsByStory, stories, widthCells, depthCells );

		var propsBefore = _spawnedFurniture;
		for ( var story = 0; story < stories; story++ )
		{
			SpawnStoryFloors( root, story, widthCells, depthCells, skipFloor, floorMaterial );
			SpawnExteriorWalls(
				root,
				story,
				stories,
				request.BuildingType,
				request.BuildingIndex,
				widthCells,
				depthCells,
				wallMaterial,
				wallMaterialSlug );
			SpawnFurnitureStory(
				root,
				story,
				stories,
				request.BuildingIndex,
				request.BuildingType,
				request.VariantIndex,
				widthCells,
				depthCells,
				skipFloor,
				lootService,
				rng );
		}

		for ( var rampStory = 0; rampStory < stories - 1; rampStory++ )
		{
			foreach ( var ramp in rampsByStory[rampStory] )
				SpawnProcRamp( root, ramp, widthCells, depthCells, stories, rampSlug, LoadMaterial( rampSlug ) );
		}

		var roofBaseZ = stories * ThornsBuildingModule.StoryHeight;
		SpawnRoofSilhouette(
			root,
			request.BuildingType,
			request.BuildingIndex,
			roofBaseZ,
			roofMaterial,
			palette.RoofSlug,
			trimMaterial,
			trimSlug,
			widthCells,
			depthCells );
		SpawnExteriorProps( root, request.BuildingType, request.BuildingIndex, roofBaseZ );
		if ( request.BuildingType == ThornsProcBuildingType.Store )
			SpawnStorefrontAwning( root, trimMaterial, roofMaterial );
		SpawnTrim( root, stories, trimMaterial, trimSlug, widthCells, depthCells );

		return new Result( root, _spawnedFurniture - propsBefore );
	}

	void SpawnStoryFloors(
		GameObject root,
		int story,
		int widthCells,
		int depthCells,
		bool[,,] skipFloor,
		Material floorMaterial )
	{
		for ( var gridY = 0; gridY < depthCells; gridY++ )
		{
			for ( var gridX = 0; gridX < widthCells; gridX++ )
			{
				if ( skipFloor[story, gridX, gridY] )
					continue;

				if ( !ThornsProcBuildingInterior.TryGridCellCenterLocal( gridX, gridY, widthCells, depthCells, out var center ) )
					continue;

				center.z = story * ThornsBuildingModule.StoryHeight;
				SpawnBox(
					root,
					$"floor_{story}_{gridX}_{gridY}",
					center,
					new Vector3( ThornsBuildingModule.Cell, ThornsBuildingModule.Cell, ThornsBuildingModule.FloorThickness ),
					floorMaterial,
					solid: true );
			}
		}
	}

	void SpawnProcRamp(
		GameObject root,
		ThornsProcRampSpec ramp,
		int widthCells,
		int depthCells,
		int stories,
		string rampSlug,
		Material rampMaterial )
	{
		var model = ThornsBuildingRampMesh.GetOrCreate( rampSlug );
		if ( !model.IsValid || model.IsError )
		{
			var fallbackSize = new Vector3(
				ThornsProcBuildingRampGeometry.RampRunWorld,
				ThornsProcBuildingRampGeometry.RampSpanYWorld,
				ThornsProcBuildingRampGeometry.RampRiseWorld );
			SpawnBox(
				root,
				$"ramp_{ramp.Story}_{ramp.X}_{ramp.Y}",
				ThornsProcBuildingRampGeometry.GetRampSpawnLocalPosition( ramp, widthCells, depthCells, stories ),
				fallbackSize,
				rampMaterial,
				solid: true );
			return;
		}

		var obj = _scene.CreateObject( true );
		obj.Name = $"ramp_{ramp.Story}_{ramp.X}_{ramp.Y}";
		obj.Parent = root;
		obj.LocalPosition = ThornsProcBuildingRampGeometry.GetRampSpawnLocalPosition( ramp, widthCells, depthCells, stories );
		obj.LocalRotation = ThornsProcBuildingRampGeometry.GetRampSpawnRotation( ramp );
		obj.LocalScale = Vector3.One;

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.MaterialOverride = rampMaterial ?? _fallbackMaterial;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		EnsureProcStructureTags( obj );
		TerraingenAnchoredPhysics.EnsureStaticModelMeshCollider( obj, model );
	}

	void SpawnExteriorWalls(
		GameObject root,
		int story,
		int stories,
		ThornsProcBuildingType buildingType,
		int buildingIndex,
		int widthCells,
		int depthCells,
		Material wall,
		string wallMaterialSlug )
	{
		for ( var side = 0; side < 4; side++ )
		{
			var cellsAlong = side is 0 or 2 ? widthCells : depthCells;
			for ( var cell = 0; cell < cellsAlong; cell++ )
			{
				var panel = ThornsProcBuildingSilhouetteCatalog.ResolveWallPanel(
					buildingType,
					side,
					cell,
					story,
					stories,
					buildingIndex,
					cellsAlong );

				if ( panel == ThornsProcBuildingSilhouetteCatalog.WallPanelKind.Omitted )
					continue;

				var isEntry = panel == ThornsProcBuildingSilhouetteCatalog.WallPanelKind.Door;
				var hasWindow = panel == ThornsProcBuildingSilhouetteCatalog.WallPanelKind.Window;
				SpawnWallPanel(
					root,
					side,
					cell,
					story,
					widthCells,
					depthCells,
					wall,
					wallMaterialSlug,
					hasWindow,
					isEntry );
			}
		}
	}

	void SpawnRoofSilhouette(
		GameObject root,
		ThornsProcBuildingType buildingType,
		int buildingIndex,
		float roofBaseZ,
		Material roofMaterial,
		string roofMaterialSlug,
		Material trimMaterial,
		string trimSlug,
		int widthCells,
		int depthCells )
	{
		var spec = ThornsProcBuildingSilhouetteCatalog.RoofSpec.For( buildingType );
		var wallFootprintW = ThornsProcBuildingFootprintCatalog.ExteriorWidthInches( widthCells );
		var wallFootprintD = ThornsProcBuildingFootprintCatalog.ExteriorDepthInches( depthCells );
		var roofFootprintW = ThornsProcBuildingFootprintCatalog.RoofSurfaceWidthInches( widthCells );
		var roofFootprintD = ThornsProcBuildingFootprintCatalog.RoofSurfaceDepthInches( depthCells );
		var deckThickness = ThornsBuildingModule.FloorThickness;

		switch ( spec.Style )
		{
			case ThornsProcBuildingSilhouetteCatalog.RoofStyle.Gable:
				SpawnFlatRoofDeck( root, roofBaseZ, roofFootprintW, roofFootprintD, deckThickness, roofMaterial, solid: true );
				SpawnGableRoof(
					root,
					roofBaseZ + deckThickness,
					roofFootprintW,
					roofFootprintD,
					spec.GableRiseInches,
					roofMaterialSlug );
				break;

			case ThornsProcBuildingSilhouetteCatalog.RoofStyle.Shed:
				SpawnFlatRoofDeck( root, roofBaseZ, roofFootprintW, roofFootprintD, deckThickness, roofMaterial, solid: true );
				SpawnShedRoof(
					root,
					roofBaseZ + deckThickness,
					roofFootprintW,
					roofFootprintD,
					spec.GableRiseInches,
					roofMaterialSlug );
				break;

			case ThornsProcBuildingSilhouetteCatalog.RoofStyle.FlatParapet:
				SpawnFlatRoofDeck( root, roofBaseZ, roofFootprintW, roofFootprintD, deckThickness, roofMaterial, solid: true );
				SpawnParapet( root, roofBaseZ + deckThickness, roofFootprintW, roofFootprintD, spec.ParapetHeightInches, roofMaterial );
				break;

			case ThornsProcBuildingSilhouetteCatalog.RoofStyle.Penthouse:
				SpawnFlatRoofDeck( root, roofBaseZ, roofFootprintW, roofFootprintD, deckThickness, roofMaterial, solid: true );
				SpawnPenthouse(
					root,
					roofBaseZ + deckThickness,
					MathF.Max( wallFootprintW, wallFootprintD ),
					spec.PenthouseScale,
					spec.PenthouseHeightInches,
					roofMaterial,
					trimMaterial,
					trimSlug );
				break;

			case ThornsProcBuildingSilhouetteCatalog.RoofStyle.Sawtooth:
				SpawnFlatRoofDeck( root, roofBaseZ, roofFootprintW, roofFootprintD, deckThickness * 0.6f, roofMaterial, solid: true );
				SpawnSawtoothRoof(
					root,
					roofBaseZ + deckThickness * 0.6f,
					roofFootprintW,
					roofFootprintD,
					spec.SawtoothRiseInches,
					spec.SawtoothSegments,
					roofMaterialSlug );
				break;

			case ThornsProcBuildingSilhouetteCatalog.RoofStyle.RuinPartial:
				SpawnRuinRoof( root, roofBaseZ, roofFootprintW, roofFootprintD, deckThickness, roofMaterial, buildingIndex );
				break;

			default:
				SpawnFlatRoofDeck( root, roofBaseZ, roofFootprintW, roofFootprintD, deckThickness, roofMaterial, solid: true );
				break;
		}
	}

	void SpawnFlatRoofDeck(
		GameObject root,
		float roofBaseZ,
		float footprintWidth,
		float footprintDepth,
		float thickness,
		Material roofMaterial,
		bool solid )
	{
		SpawnBox(
			root,
			"roof_deck",
			new Vector3( 0f, 0f, roofBaseZ + thickness * 0.5f ),
			new Vector3( footprintWidth, footprintDepth, thickness ),
			roofMaterial,
			solid );
	}

	void SpawnGableRoof(
		GameObject root,
		float eaveZ,
		float footprintWidth,
		float footprintDepth,
		float rise,
		string roofMaterialSlug )
	{
		var mat = LoadMaterial( roofMaterialSlug );
		var model = ThornsBuildingRoofMesh.GetPeakedRoof( roofMaterialSlug, footprintWidth, footprintDepth, rise );
		SpawnRoofPanel( root, "roof_gable", model, mat, new Vector3( 0f, 0f, eaveZ ) );
	}

	void SpawnShedRoof(
		GameObject root,
		float eaveZ,
		float footprintWidth,
		float footprintDepth,
		float rise,
		string roofMaterialSlug )
	{
		var mat = LoadMaterial( roofMaterialSlug );
		var model = ThornsBuildingRoofMesh.GetShedRoof( roofMaterialSlug, footprintWidth, footprintDepth, rise );
		SpawnRoofPanel( root, "roof_shed", model, mat, new Vector3( 0f, 0f, eaveZ ) );
	}

	void SpawnRoofPanel(
		GameObject root,
		string name,
		Model model,
		Material material,
		Vector3 localPosition )
	{
		if ( !model.IsValid() || model.IsError )
		{
			SpawnBox( root, name, localPosition, new Vector3( 160f, 160f, 24f ), material, solid: false );
			return;
		}

		var originFix = Vector3.Zero;
		if ( model.Bounds.Size.LengthSquared > 1e-8f )
		{
			var center = model.Bounds.Center;
			originFix = new Vector3( center.x, center.y, 0f );
		}

		var obj = _scene.CreateObject( true );
		obj.Name = name;
		obj.Parent = root;
		obj.LocalPosition = localPosition - originFix;
		obj.LocalRotation = Rotation.Identity;
		obj.LocalScale = Vector3.One;

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = model;
		if ( material is not null && material.IsValid() )
			renderer.MaterialOverride = material;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );
	}

	void SpawnParapet(
		GameObject root,
		float deckTopZ,
		float footprintWidth,
		float footprintDepth,
		float parapetHeight,
		Material material )
	{
		var lip = 6f;
		var halfW = footprintWidth * 0.5f;
		var halfD = footprintDepth * 0.5f;
		var centerZ = deckTopZ + parapetHeight * 0.5f;
		SpawnBox( root, "parapet_s", new Vector3( 0f, -halfD + lip * 0.5f, centerZ ), new Vector3( footprintWidth, lip, parapetHeight ), material, solid: false );
		SpawnBox( root, "parapet_n", new Vector3( 0f, halfD - lip * 0.5f, centerZ ), new Vector3( footprintWidth, lip, parapetHeight ), material, solid: false );
		SpawnBox( root, "parapet_w", new Vector3( -halfW + lip * 0.5f, 0f, centerZ ), new Vector3( lip, footprintDepth - lip * 2f, parapetHeight ), material, solid: false );
		SpawnBox( root, "parapet_e", new Vector3( halfW - lip * 0.5f, 0f, centerZ ), new Vector3( lip, footprintDepth - lip * 2f, parapetHeight ), material, solid: false );
	}

	void SpawnPenthouse(
		GameObject root,
		float deckTopZ,
		float footprint,
		float scale,
		float height,
		Material roofMaterial,
		Material trimMaterial,
		string trimSlug )
	{
		var size = footprint * scale;
		SpawnBox(
			root,
			"roof_penthouse",
			new Vector3( 0f, 0f, deckTopZ + height * 0.5f ),
			new Vector3( size, size, height ),
			roofMaterial,
			solid: true );

		var bandH = ThornsBuildingModule.TrimBandHeight;
		SpawnBox(
			root,
			"roof_penthouse_band",
			new Vector3( 0f, 0f, deckTopZ + height + bandH * 0.5f ),
			new Vector3( size + 8f, size + 8f, bandH ),
			trimMaterial ?? roofMaterial,
			solid: false );
	}

	void SpawnSawtoothRoof(
		GameObject root,
		float baseZ,
		float footprintWidth,
		float footprintDepth,
		float rise,
		int segments,
		string roofMaterialSlug )
	{
		var mat = LoadMaterial( roofMaterialSlug );
		var model = ThornsBuildingRoofMesh.GetSawtoothRoof(
			roofMaterialSlug,
			footprintWidth,
			footprintDepth,
			rise,
			segments );
		SpawnRoofPanel( root, "roof_sawtooth", model, mat, new Vector3( 0f, 0f, baseZ ) );
	}

	void SpawnRuinRoof(
		GameObject root,
		float roofBaseZ,
		float footprintWidth,
		float footprintDepth,
		float thickness,
		Material roofMaterial,
		int buildingIndex )
	{
		var quadrants = new (string name, Vector3 pos, Vector3 size)[]
		{
			("roof_ruin_nw", new Vector3( -footprintWidth * 0.25f, footprintDepth * 0.25f, roofBaseZ + thickness * 0.5f ), new Vector3( footprintWidth * 0.48f, footprintDepth * 0.48f, thickness )),
			("roof_ruin_ne", new Vector3( footprintWidth * 0.25f, footprintDepth * 0.25f, roofBaseZ + thickness * 0.5f ), new Vector3( footprintWidth * 0.48f, footprintDepth * 0.48f, thickness )),
			("roof_ruin_sw", new Vector3( -footprintWidth * 0.25f, -footprintDepth * 0.25f, roofBaseZ + thickness * 0.5f ), new Vector3( footprintWidth * 0.48f, footprintDepth * 0.48f, thickness )),
			("roof_ruin_se", new Vector3( footprintWidth * 0.25f, -footprintDepth * 0.25f, roofBaseZ + thickness * 0.5f ), new Vector3( footprintWidth * 0.48f, footprintDepth * 0.48f, thickness ))
		};

		for ( var i = 0; i < quadrants.Length; i++ )
		{
			if ( (HashCode.Combine( buildingIndex, i, 0x7710001 ) & 3) == 0 )
				continue;

			var q = quadrants[i];
			SpawnBox( root, q.name, q.pos, q.size, roofMaterial, solid: i < 2 );
		}
	}

	void SpawnExteriorProps(
		GameObject root,
		ThornsProcBuildingType buildingType,
		int buildingIndex,
		float roofBaseZ )
	{
		var props = new List<ThornsProcBuildingSilhouetteCatalog.ExteriorPropSpec>( 4 );
		ThornsProcBuildingSilhouetteCatalog.CollectExteriorProps(
			buildingType,
			0,
			buildingIndex,
			roofBaseZ + ThornsBuildingModule.FloorThickness,
			props );

		for ( var i = 0; i < props.Count; i++ )
		{
			var prop = props[i];
			SpawnBox(
				root,
				$"exterior_prop_{i}",
				prop.LocalOffset,
				prop.Size,
				LoadMaterial( prop.MaterialSlug ),
				prop.Solid );
		}
	}

	void SpawnStorefrontAwning( GameObject root, Material trimMaterial, Material roofMaterial )
	{
		var northY = ThornsProcBuildingSilhouetteCatalog.NorthWallExteriorY;
		var doorTopZ = ThornsProcBuildingSilhouetteCatalog.GroundDoorTopLocalZ;
		var awningZ = doorTopZ + 8f;
		var width = ThornsProcBuildingSilhouetteCatalog.StorefrontAwningWidthInches;
		var depth = ThornsProcBuildingSilhouetteCatalog.StorefrontAwningDepthInches;
		const float canvasThickness = 6f;

		SpawnBox(
			root,
			"store_awning_header",
			new Vector3( 0f, northY + 4f, awningZ + 4f ),
			new Vector3( width, 8f, 12f ),
			trimMaterial,
			solid: false );

		SpawnBox(
			root,
			"store_awning_canvas",
			new Vector3( 0f, northY + 6f + depth * 0.5f, awningZ ),
			new Vector3( width, depth, canvasThickness ),
			roofMaterial,
			solid: false );
	}

	void SpawnWallPanel(
		GameObject root,
		int side,
		int cell,
		int story,
		int widthCells,
		int depthCells,
		Material wall,
		string wallMaterialSlug,
		bool window,
		bool entry )
	{
		ThornsProcBuildingInterior.GetPerimeterWallExtents(
			widthCells,
			depthCells,
			out var westX,
			out var eastX,
			out var southY,
			out var northY );

		var cellsAlong = side is 0 or 2 ? widthCells : depthCells;
		var along = ThornsProcBuildingInterior.GridAxisLocal( cell, cellsAlong, ThornsBuildingModule.Cell );
		var zBase = story * ThornsBuildingModule.StoryHeight + ThornsBuildingModule.FloorThickness * 0.5f;
		var wallCenterZ = zBase + ThornsBuildingModule.WallHeight * 0.5f;
		var local = side switch
		{
			0 => new Vector3( along, southY - ThornsBuildingModule.WallThickness * 0.5f, wallCenterZ ),
			2 => new Vector3( along, northY + ThornsBuildingModule.WallThickness * 0.5f, wallCenterZ ),
			1 => new Vector3( eastX + ThornsBuildingModule.WallThickness * 0.5f, along, wallCenterZ ),
			_ => new Vector3( westX - ThornsBuildingModule.WallThickness * 0.5f, along, wallCenterZ )
		};
		var rotation = ThornsBuildingWallMesh.RotationForSide( side );

		var model = entry
			? ThornsBuildingWallMesh.GetDoorFrameWall( wallMaterialSlug )
			: window
				? ThornsBuildingWallMesh.GetWindowWall( wallMaterialSlug )
				: ThornsBuildingWallMesh.GetSolidWall( wallMaterialSlug );

		SpawnProcWallPiece( root, model, local, rotation, wall, entry ? "door" : window ? "window" : "wall" );
	}

	void SpawnProcWallPiece(
		GameObject root,
		Model model,
		Vector3 localPosition,
		Rotation localRotation,
		Material material,
		string pieceName )
	{
		if ( !model.IsValid() || model.IsError )
		{
			SpawnBox(
				root,
				pieceName,
				localPosition,
				new Vector3(
					ThornsBuildingWallMesh.ProcWallRunWorld,
					ThornsBuildingModule.WallThickness,
					ThornsBuildingModule.WallHeight ),
				material,
				solid: true );
			return;
		}

		var obj = _scene.CreateObject( true );
		obj.Name = pieceName;
		obj.Parent = root;
		obj.LocalPosition = localPosition;
		obj.LocalRotation = localRotation;
		obj.LocalScale = Vector3.One;

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = model;
		if ( material is not null && material.IsValid() )
			renderer.MaterialOverride = material;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		EnsureProcStructureTags( obj );
		TerraingenAnchoredPhysics.EnsureStaticModelMeshCollider( obj, model );
	}

	void SpawnTrim(
		GameObject root,
		int stories,
		Material trim,
		string trimSlug,
		int widthCells,
		int depthCells )
	{
		ThornsBuildingModule.ProcPerimeterShellFullExtent( stories, out var shellCenterZ, out var shellSpan );

		var cell = ThornsBuildingModule.Cell;
		var half = cell * 0.5f;
		var westWallX = -half * ( widthCells - 1 ) - half;
		var eastWallX = half * ( widthCells - 1 ) + half;
		var southWallY = -half * ( depthCells - 1 ) - half;
		var northWallY = half * ( depthCells - 1 ) + half;
		var centerX = ( westWallX + eastWallX ) * 0.5f;
		var centerY = ( southWallY + northWallY ) * 0.5f;

		var cornerSize = ThornsBuildingModule.TrimCornerSize;
		var cornerModel = ThornsBuildingTrimMesh.GetTrimBox(
			trimSlug,
			new Vector3( cornerSize, cornerSize, MathF.Max( shellSpan, cornerSize ) ) );

		void SpawnCorner( float wallX, float wallY, int sideA, int sideB ) =>
			SpawnProcTrimPiece(
				root,
				cornerModel,
				CornerTrimLocalPosition( wallX, wallY, shellCenterZ, sideA, sideB ),
				Rotation.Identity,
				trim,
				"pillar",
				solid: true );

		SpawnCorner( westWallX, southWallY, 0, 3 );
		SpawnCorner( eastWallX, southWallY, 0, 1 );
		SpawnCorner( westWallX, northWallY, 2, 3 );
		SpawnCorner( eastWallX, northWallY, 2, 1 );

		var widthRun = ThornsBuildingTrimMesh.ProcPerimeterRunWorld( widthCells );
		var depthRun = ThornsBuildingTrimMesh.ProcPerimeterRunWorld( depthCells );
		var bandH = ThornsBuildingModule.TrimBandHeight;
		var bandDepth = ThornsBuildingModule.TrimSeamDepth;
		var bandNorthSouth = ThornsBuildingTrimMesh.GetTrimBox(
			trimSlug,
			new Vector3( widthRun, bandDepth, bandH ) );
		var bandEastWest = ThornsBuildingTrimMesh.GetTrimBox(
			trimSlug,
			new Vector3( bandDepth, depthRun, bandH ) );

		Span<float> bandCenterZ = stackalloc float[8];
		var bandCount = ThornsBuildingModule.CollectPerimeterBandTrimCenterZ( stories, bandCenterZ );
		for ( var bi = 0; bi < bandCount; bi++ )
		{
			var bandZ = bandCenterZ[bi];
			SpawnProcTrimPiece(
				root,
				bandNorthSouth,
				ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition( centerX, southWallY, bandZ, 0 ),
				Rotation.Identity,
				trim,
				"trim_s",
				solid: false );
			SpawnProcTrimPiece(
				root,
				bandNorthSouth,
				ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition( centerX, northWallY, bandZ, 2 ),
				Rotation.Identity,
				trim,
				"trim_n",
				solid: false );
			SpawnProcTrimPiece(
				root,
				bandEastWest,
				ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition( centerY, westWallX, bandZ, 3 ),
				Rotation.Identity,
				trim,
				"trim_w",
				solid: false );
			SpawnProcTrimPiece(
				root,
				bandEastWest,
				ThornsBuildingModule.ProcPerimeterBandTrimLocalPosition( centerY, eastWallX, bandZ, 1 ),
				Rotation.Identity,
				trim,
				"trim_e",
				solid: false );
		}
	}

	static Vector3 CornerTrimLocalPosition(
		float wallIntersectX,
		float wallIntersectY,
		float centerLocalZ,
		int outwardSideA,
		int outwardSideB )
	{
		var push = ThornsBuildingModule.WallThickness * 0.5f;
		var px = wallIntersectX;
		var py = wallIntersectY;
		if ( outwardSideA == 0 || outwardSideB == 0 )
			py -= push;
		if ( outwardSideA == 2 || outwardSideB == 2 )
			py += push;
		if ( outwardSideA == 3 || outwardSideB == 3 )
			px -= push;
		if ( outwardSideA == 1 || outwardSideB == 1 )
			px += push;
		return new Vector3( px, py, centerLocalZ );
	}

	void SpawnProcTrimPiece(
		GameObject root,
		Model model,
		Vector3 localPosition,
		Rotation localRotation,
		Material material,
		string pieceName,
		bool solid )
	{
		if ( !model.IsValid() || model.IsError )
		{
			SpawnBox( root, pieceName, localPosition, new Vector3( 24f, 24f, 24f ), material, solid: solid );
			return;
		}

		var obj = _scene.CreateObject( true );
		obj.Name = pieceName;
		obj.Parent = root;
		obj.LocalPosition = localPosition;
		obj.LocalRotation = localRotation;
		obj.LocalScale = Vector3.One;

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = model;
		if ( material is not null && material.IsValid() )
			renderer.MaterialOverride = material;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		if ( !solid )
			return;

		EnsureProcStructureTags( obj );
		TerraingenAnchoredPhysics.EnsureStaticModelMeshCollider( obj, model );
	}

	void SpawnFurnitureStory(
		GameObject root,
		int story,
		int stories,
		int buildingIndex,
		ThornsProcBuildingType buildingType,
		int variantIndex,
		int widthCells,
		int depthCells,
		bool[,,] skipFloor,
		ThornsBuildingLootWorldService service,
		Random rng )
	{
		var placements = new List<ThornsProcBuildingInterior.CellPlacement>( 8 );
		if ( !ThornsInteriorFurnitureAsciiLayouts.TryCollectScriptedPlacements(
			     buildingType,
			     variantIndex,
			     story,
			     widthCells,
			     depthCells,
			     placements,
			     stories,
			     skipFloor ) )
			return;

		foreach ( var cell in placements )
		{
			var structureDefId = cell.StructureDefId;
			var ctx = new ThornsProcBuildingInteriorPose.SpawnContext(
				root,
				widthCells,
				depthCells,
				cell.Story,
				cell.GridX,
				cell.GridY );
			var obj = SpawnFurniture( in ctx, structureDefId, buildingIndex );
			service?.RegisterFurniture( obj, structureDefId, buildingType, rng );
		}
	}

	GameObject SpawnFurniture(
		in ThornsProcBuildingInteriorPose.SpawnContext ctx,
		string structureDefId,
		int buildingIndex )
	{
		var parent = ctx.BuildingRoot;
		var modelPath = ThornsPlaceableFurnitureCatalog.GetModelPath( structureDefId );
		var primaryModel = ThornsModelResourceLoad.TryLoad( modelPath );
		var usingFallbackModel = !ThornsModelResourceLoad.IsUsable( primaryModel );

		var obj = _scene.CreateObject( true );
		obj.Name = $"Furniture {structureDefId}";
		obj.Parent = parent;

		if ( usingFallbackModel )
		{
			_furnitureModelFallbacks++;
			SpawnFurnitureFallbackBox( obj, structureDefId, in ctx );
		}
		else
		{
			ThornsPlaceableFurniturePresentation.Apply(
				obj,
				structureDefId,
				ctx.Story,
				ctx.GridX,
				ctx.GridY,
				ctx.WidthCells,
				ctx.DepthCells );

			var renderer = obj.Components.Get<ModelRenderer>();
			if ( renderer is null || !ThornsModelResourceLoad.IsUsable( renderer.Model ) )
			{
				usingFallbackModel = true;
				_furnitureModelFallbacks++;
				SpawnFurnitureFallbackBox( obj, structureDefId, in ctx );
			}
		}

		var fallbackWorld = parent.WorldPosition;
		if ( ThornsProcBuildingInterior.TryGridCellCenterLocal(
			     ctx.GridX,
			     ctx.GridY,
			     ctx.WidthCells,
			     ctx.DepthCells,
			     out var cellCenter ) )
		{
			cellCenter.z = ThornsProcBuildingInterior.InteriorFloorWalkLocalZ( ctx.Story );
			fallbackWorld = parent.WorldPosition + parent.WorldRotation * cellCenter;
		}

		ThornsProcBuildingInteriorPose.SeatOnBuilding(
			obj,
			parent,
			in ctx,
			structureDefId,
			parent.WorldRotation,
			fallbackWorld );

		EnsureProcStructureTags( obj );
		if ( !obj.Tags.Contains( "furniture" ) )
			obj.Tags.Add( "furniture" );

		ThornsProcRadioShopAttach.TryAttach( obj, structureDefId );

		var mr = obj.Components.Get<ModelRenderer>();
		ThornsFurnitureMaterialDebug.LogSpawn(
			"SpawnFurniture",
			structureDefId,
			modelPath,
			mr?.Model ?? default,
			obj,
			usingFallbackModel,
			mr );

		_spawnedFurniture++;
		if ( _debugLogging && usingFallbackModel && _furnitureDebugLogs < _maxFurnitureDebugLogs )
		{
			_furnitureDebugLogs++;
			Log.Info(
				$"[Thorns Buildings] Furniture {_spawnedFurniture:000}: building={buildingIndex:00}, story={ctx.Story}, id={structureDefId}, fallbackModel={usingFallbackModel}, local={obj.LocalPosition}, world={obj.WorldPosition}." );
		}

		return obj;
	}

	void SpawnFurnitureFallbackBox(
		GameObject obj,
		string structureDefId,
		in ThornsProcBuildingInteriorPose.SpawnContext ctx )
	{
		_ = ctx;
		var worldSize = ThornsPlaceableFurnitureCatalog.GetWorldSizeInches( structureDefId );
		obj.LocalScale = ThornsBuildingModule.ScaleBoxToWorldAxes( worldSize.x, worldSize.y, worldSize.z );

		var renderer = obj.Components.Get<ModelRenderer>() ?? obj.Components.Create<ModelRenderer>();
		renderer.Model = _boxModel;
		renderer.MaterialOverride = LoadMaterial( "wood" );
		renderer.Tint = FurnitureFallbackTint( structureDefId );
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		EnsureProcStructureTags( obj );
		TerraingenAnchoredPhysics.EnsureFurnitureCollider( obj, _boxModel );
	}

	static Color FurnitureFallbackTint( string structureDefId ) => structureDefId?.Trim().ToLowerInvariant() switch
	{
		"couch" or "chair" or "dining_table" or "conference" => new Color( 0.58f, 0.34f, 0.2f ),
		"bed" => new Color( 0.38f, 0.48f, 0.72f ),
		"workbench" or "pallets" => new Color( 0.46f, 0.42f, 0.35f ),
		"military_supply" => new Color( 0.32f, 0.42f, 0.32f ),
		"retail" => new Color( 0.6f, 0.52f, 0.3f ),
		_ => new Color( 0.55f, 0.4f, 0.28f )
	};

	GameObject SpawnBox( GameObject parent, string name, Vector3 localPosition, Vector3 worldSize, Material material, bool solid )
	{
		var obj = _scene.CreateObject( true );
		obj.Name = name;
		obj.Parent = parent;
		obj.LocalPosition = localPosition;
		obj.LocalScale = ThornsBuildingModule.ScaleBoxToWorldAxes( worldSize.x, worldSize.y, worldSize.z );

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = _boxModel;
		renderer.MaterialOverride = material ?? _fallbackMaterial;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		if ( solid )
		{
			EnsureProcStructureTags( obj );
			TerraingenAnchoredPhysics.EnsureStaticModelMeshCollider( obj, _boxModel );
		}

		return obj;
	}

	Vector3 ScaleModelToSize( Model model, Vector3 worldSize )
	{
		var bounds = ResolveBounds( model );
		var size = bounds.Size;
		return new Vector3(
			worldSize.x / Math.Max( 1f, size.x ),
			worldSize.y / Math.Max( 1f, size.y ),
			worldSize.z / Math.Max( 1f, size.z ) );
	}

	static BBox ResolveBounds( Model model )
	{
		if ( model.IsValid && model.Bounds.Size.LengthSquared > 1e-8f )
			return model.Bounds;

		return new BBox( new Vector3( -25f, -25f, -25f ), new Vector3( 25f, 25f, 25f ) );
	}

	Material LoadMaterial( string name )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			return null;

		if ( _materials.TryGetValue( name, out var cached ) )
			return cached;

		var mat = Material.Load( $"{ThornsProcBuildingMaterialCatalog.PathPrefix}{name}.vmat" );
		if ( mat is null || !mat.IsValid() )
			mat = _fallbackMaterial is not null && _fallbackMaterial.IsValid()
				? _fallbackMaterial
				: Material.Load( "materials/default/default.vmat" );

		if ( _fallbackMaterial is null || !_fallbackMaterial.IsValid() )
			_fallbackMaterial = mat;

		_materials[name] = mat;
		return mat;
	}

	public GameObject SpawnGalleryFloor(
		GameObject parent,
		Vector3 worldPosition,
		Vector3 planarScale,
		string floorModelPath = "models/dev/box.vmdl",
		Color? tint = null )
	{
		var model = Model.Load( floorModelPath );
		if ( !model.IsValid || model.IsError )
			model = _boxModel;

		var planar = new Vector3(
			MathF.Max( 8f, planarScale.x ),
			MathF.Max( 8f, planarScale.y ),
			MathF.Max( 2f, planarScale.z ) );

		var floor = _scene.CreateObject( true );
		floor.Name = "FloorplanTest_Floor";
		floor.Parent = parent;
		floor.LocalScale = planar;

		var bb = model.Bounds;
		var topLocalZ = bb.Maxs.z;
		if ( topLocalZ < 1e-4f )
			topLocalZ = bb.Size.z * 0.5f + bb.Mins.z;

		floor.WorldPosition = new Vector3(
			worldPosition.x,
			worldPosition.y,
			worldPosition.z - topLocalZ * planar.z );

		var mr = floor.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.Tint = tint ?? new Color( 0.32f, 0.36f, 0.42f );
		ThornsWorldShadowUtil.EnableWorldShadows( mr );

		EnsureProcStructureTags( floor );
		var bounds = TerraingenAnchoredPhysics.GetTightModelBounds( model );
		var collider = floor.Components.GetOrCreate<BoxCollider>();
		collider.Center = bounds.Center;
		collider.Scale = bounds.Size;
		collider.Static = true;
		collider.Enabled = true;

		return floor;
	}

	static void EnsureProcStructureTags( GameObject obj )
	{
		TerraingenAnchoredPhysics.EnsureSolidTags( obj );
		if ( obj.IsValid() && !obj.Tags.Contains( "thorns_structure" ) )
			obj.Tags.Add( "thorns_structure" );
	}
}
