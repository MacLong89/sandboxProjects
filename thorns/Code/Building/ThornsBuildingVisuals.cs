using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Procedural kit meshes — facade vmats under <c>materials/*.vmat</c> (see <see cref="ThornsProcBuildingMaterialPalette"/>).</summary>
public static class ThornsBuildingVisuals
{
	static float Cs => ThornsBuildingModule.Cell;
	static float Ft => ThornsBuildingModule.FloorThickness;
	static float Wh => ThornsBuildingModule.WallHeight;
	static float T => ThornsBuildingModule.WallThickness;
	const float FloorUvTileWorldUnits = 28f;
	const float WallUvTileWorldUnits = 25f;
	const float RampUvTileWorldUnits = 24f;
	static readonly bool DebugLogFloorUv = false;

	/// <summary>Uniform multiplier for player placeable structure world scale (chest / campfire / workbench vmdl + matching root <see cref="ModelCollider"/>). Was 10f — vmdl art still read tiny at that factor; 100f targets ~10× that prior size.</summary>
	public const float PlaceableStructureWorldScale = 100f;

	/// <summary>
	/// Max distance for Use (E) on chest/campfire/radio-style anchors — <b>not</b> multiplied by <see cref="PlaceableStructureWorldScale"/>.
	/// (Tying use range to art scale made interaction spheres ~17k units when scale was 100.)
	/// </summary>
	public const float PlaceableInteractionUseRange = 128f;

	/// <summary>Slightly farther than <see cref="PlaceableInteractionUseRange"/> (legacy 185 vs 175).</summary>
	public const float WorkbenchInteractionUseRange = 136f;

	/// <summary>Bump when procedural mesh/material wiring changes — hotload can leave stale <see cref="Model"/> statics (wrong scale / ERROR mats).</summary>
	const int ProcBuildingGeomRevision = 42;

	static int _procGeomRevApplied = -1;

	static readonly Material[] _buildingMatsByTier = new Material[3];
	static readonly Material[] _floorMatsByTier = new Material[3];
	static readonly Dictionary<string, Material> _facadeMatsBySlug = new( StringComparer.Ordinal );
	static readonly Dictionary<string, Model> _procModelsBySlugAndDef = new( StringComparer.Ordinal );
	static Material _cleanWoodWallMat;

	static readonly Model[] _unitCubeByTier = new Model[3];
	static readonly Model[] _wallModelByTier = new Model[3];
	static readonly Model[] _foundationModelByTier = new Model[3];
	static readonly Model[] _doorModelByTier = new Model[3];
	static readonly Model[] _windowModelByTier = new Model[3];
	static readonly Model[] _doorFrameModelByTier = new Model[3];
	static readonly Model[] _rampModelByTier = new Model[3];
	static Model _placeableChestModel;
	static Model _placeableCampfireModel;
	static Model _placeableWorkbenchModel;
	static readonly Dictionary<string, Model> _placeableFurnitureModelsByDefId = new( StringComparer.OrdinalIgnoreCase );
	static int _placeableFurnitureCatalogRevision = -1;
	static Model _placeableBedModel;
	static Model _placeableRadioModel;
	static Material _placeableRadioBaseColorMat;
	static bool _loggedFloorUv;

	enum BuildingUvMode : byte
	{
		Generic = 0,
		Floor = 1,
		Wall = 2,
		Ramp = 3
	}

	static void InvalidateProcBuildingCachesIfStale()
	{
		if ( _procGeomRevApplied == ProcBuildingGeomRevision )
			return;

		_procGeomRevApplied = ProcBuildingGeomRevision;
		_loggedFloorUv = false;
		for ( var i = 0; i < 3; i++ )
		{
			_buildingMatsByTier[i] = default;
			_floorMatsByTier[i] = default;
			_unitCubeByTier[i] = default;
			_wallModelByTier[i] = default;
			_foundationModelByTier[i] = default;
			_doorModelByTier[i] = default;
			_windowModelByTier[i] = default;
			_doorFrameModelByTier[i] = default;
			_rampModelByTier[i] = default;
		}
		_cleanWoodWallMat = default;
		_facadeMatsBySlug.Clear();
		_procModelsBySlugAndDef.Clear();
		_placeableChestModel = default;
		_placeableCampfireModel = default;
		_placeableWorkbenchModel = default;
		_placeableFurnitureModelsByDefId.Clear();
		_placeableBedModel = default;
		_placeableRadioModel = default;
		_placeableRadioBaseColorMat = default;
	}

	static int MaterialTierIndexClamped( int tier ) => Math.Clamp( tier, 0, 2 );

	static string LegacySlugFromTier( int tier ) => tier switch
	{
		1 => "stone",
		2 => "metal",
		_ => "wood"
	};

	static string ProcModelCacheKey( string materialSlug, string structureDefId ) =>
		$"{ThornsProcBuildingMaterialPalette.IndexOfSlug( materialSlug ):D2}:{structureDefId}";

	static Material FacadeMaterial( string materialSlug )
	{
		InvalidateProcBuildingCachesIfStale();
		var slug = ThornsProcBuildingMaterialPalette.SlugFromIndex(
			ThornsProcBuildingMaterialPalette.IndexOfSlug( materialSlug ) );
		if ( _facadeMatsBySlug.TryGetValue( slug, out var cached ) && cached.IsValid() )
			return cached;

		var path = ThornsProcBuildingMaterialPalette.VmatPath( slug );
		var loaded = Material.Load( path );
		if ( !loaded.IsValid() )
		{
			Log.Warning( $"[Thorns] {path} failed to load — falling back to wood." );
			loaded = BuildingMaterial( 0 );
		}

		_facadeMatsBySlug[slug] = loaded;
		return loaded;
	}

	static bool UsesInteriorFloorMaterial( string structureDefId ) =>
		structureDefId is "wood_foundation" or "wood_ramp" or "wood_perimeter_trim";

	static Material InteriorFloorMaterial( string facadeSlug ) =>
		FacadeMaterial( ThornsProcBuildingMaterialPalette.InteriorFloorSlugForFacade( facadeSlug ) );

	static Material ProcPieceMaterial( string structureDefId, string facadeSlug ) =>
		UsesInteriorFloorMaterial( structureDefId )
			? InteriorFloorMaterial( facadeSlug )
			: FacadeMaterial( facadeSlug );

	/// <summary>Facade vmat for walls/doors/windows; interior floor vmat for foundations, ramps, and perimeter trim.</summary>
	public static Material ResolveProcBuildingPieceMaterial( string structureDefId, string facadeMaterialSlug ) =>
		ProcPieceMaterial( structureDefId, facadeMaterialSlug );

	static Material BuildingMaterial( int tier )
	{
		InvalidateProcBuildingCachesIfStale();
		var idx = MaterialTierIndexClamped( tier );
		if ( _buildingMatsByTier[idx].IsValid() )
			return _buildingMatsByTier[idx];

		var path = idx switch
		{
			1 => "materials/stone.vmat",
			2 => "materials/metal.vmat",
			_ => "materials/wood.vmat"
		};

		var loaded = Material.Load( path );
		if ( !loaded.IsValid() )
		{
			Log.Warning( $"[Thorns] {path} failed to load (compile assets / check texture). Using default.vmat." );
			loaded = Material.Load( "materials/default.vmat" );
		}

		_buildingMatsByTier[idx] = loaded;
		return loaded;
	}

	static Material StructureWallMaterial( int materialTier )
	{
		var idx = MaterialTierIndexClamped( materialTier );
		return idx == (int)ThornsBuildingMaterialTier.Wood
			? CleanWoodWallMaterial()
			: BuildingMaterial( idx );
	}

	static Material CleanWoodWallMaterial()
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _cleanWoodWallMat.IsValid() )
			return _cleanWoodWallMat;

		_cleanWoodWallMat = Material.Load( "materials/wood.vmat" );
		if ( !_cleanWoodWallMat.IsValid() )
		{
			Log.Warning(
				"[Thorns] materials/wood.vmat failed to load for clean wood walls. Falling back to tier wood." );
			_cleanWoodWallMat = BuildingMaterial( (int)ThornsBuildingMaterialTier.Wood );
		}

		return _cleanWoodWallMat;
	}

	static Material FloorMaterial( int tier )
	{
		InvalidateProcBuildingCachesIfStale();
		var idx = MaterialTierIndexClamped( tier );
		if ( _floorMatsByTier[idx].IsValid() )
			return _floorMatsByTier[idx];

		var path = idx switch
		{
			1 => "materials/stone.vmat",
			2 => "materials/metal.vmat",
			_ => "materials/wood.vmat"
		};

		var loaded = Material.Load( path );
		if ( !loaded.IsValid() )
		{
			Log.Warning( $"[Thorns] {path} failed to load for floor. Falling back to tier building material." );
			loaded = BuildingMaterial( idx );
		}

		_floorMatsByTier[idx] = loaded;
		return loaded;
	}

	/// <summary>
	/// Cube sized to <see cref="ThornsBuildingModule.DevReferenceSize"/> (same basis as legacy <c>models/dev/box.vmdl</c> for <see cref="ThornsBuildingModule.ScaleBoxToWorldAxes"/>).
	/// </summary>
	public static Model UnitCubeModel( int materialTier = (int)ThornsBuildingMaterialTier.Wood )
	{
		InvalidateProcBuildingCachesIfStale();
		var idx = MaterialTierIndexClamped( materialTier );

		if ( _unitCubeByTier[idx].IsValid() )
			return _unitCubeByTier[idx];

		var r = ThornsBuildingModule.DevReferenceSize;
		var extent = new Vector3( r, r, r );
		_unitCubeByTier[idx] = BuildBoxPartsModel(
			$"thorns/building/unit_cube_t{idx}_r4",
			new List<(Vector3 center, Vector3 size)> { (Vector3.Zero, extent) },
			materialTier );
		return _unitCubeByTier[idx];
	}

	static Model PlaceableChestModel()
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _placeableChestModel.IsValid() )
			return _placeableChestModel;

		_placeableChestModel = Model.Load( "models/placeables/chest.vmdl" );
		if ( !_placeableChestModel.IsValid() || _placeableChestModel.IsError )
		{
			Log.Warning( "[Thorns] models/placeables/chest.vmdl failed — falling back to dev box for storage chest." );
			_placeableChestModel = Model.Load( "models/dev/box.vmdl" );
		}

		return _placeableChestModel;
	}

	static Model PlaceableCampfireModel()
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _placeableCampfireModel.IsValid() )
			return _placeableCampfireModel;

		_placeableCampfireModel = Model.Load( "models/placeables/campfire.vmdl" );
		if ( !_placeableCampfireModel.IsValid() || _placeableCampfireModel.IsError )
		{
			Log.Warning( "[Thorns] models/placeables/campfire.vmdl failed — falling back to dev box for campfire." );
			_placeableCampfireModel = Model.Load( "models/dev/box.vmdl" );
		}

		return _placeableCampfireModel;
	}

	static Model PlaceableWorkbenchModel()
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _placeableWorkbenchModel.IsValid() )
			return _placeableWorkbenchModel;

		_placeableWorkbenchModel = Model.Load( "models/placeables/workbench.vmdl" );
		if ( !_placeableWorkbenchModel.IsValid() || _placeableWorkbenchModel.IsError )
		{
			Log.Warning( "[Thorns] models/placeables/workbench.vmdl failed — falling back to dev box for workbench." );
			_placeableWorkbenchModel = Model.Load( "models/dev/box.vmdl" );
		}

		return _placeableWorkbenchModel;
	}

	static Model PlaceableBedModel()
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _placeableBedModel.IsValid() )
			return _placeableBedModel;

		_placeableBedModel = Model.Load( "models/placeables/bed.vmdl" );
		if ( !_placeableBedModel.IsValid() || _placeableBedModel.IsError )
		{
			Log.Warning( "[Thorns] models/placeables/bed.vmdl failed — falling back to dev box for bed." );
			_placeableBedModel = Model.Load( "models/dev/box.vmdl" );
		}

		return _placeableBedModel;
	}

	static Model PlaceableRadioModel()
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _placeableRadioModel.IsValid() )
			return _placeableRadioModel;

		_placeableRadioModel = Model.Load( "models/placeables/radio.vmdl" );
		if ( !_placeableRadioModel.IsValid() || _placeableRadioModel.IsError )
		{
			Log.Warning( "[Thorns] models/placeables/radio.vmdl failed — falling back to dev box for radio." );
			_placeableRadioModel = Model.Load( "models/dev/box.vmdl" );
		}

		return _placeableRadioModel;
	}

	/// <summary>Loaded <c>models/placeables/radio.vmdl</c> for interior radio props.</summary>
	public static Model PlaceableRadioWorldModel() => PlaceableRadioModel();

	/// <summary>Albedo material referenced by <c>radio.vmdl</c> (not <c>radio_basecolor.vmat</c>).</summary>
	public static Material PlaceableRadioBaseColorMaterial()
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _placeableRadioBaseColorMat.IsValid() )
			return _placeableRadioBaseColorMat;

		var fromLibrary = ResourceLibrary.Get<Material>( "models/placeables/radiotable_basecolor.vmat" );
		if ( fromLibrary is { IsValid: true } )
		{
			_placeableRadioBaseColorMat = fromLibrary;
			return _placeableRadioBaseColorMat;
		}

		var loaded = Material.Load( "models/placeables/radiotable_basecolor.vmat" );
		if ( loaded.IsValid() )
			_placeableRadioBaseColorMat = loaded;

		return _placeableRadioBaseColorMat;
	}

	/// <summary>World scale for player-placed kit vmdls (hotbar / build ghost). Same as proc scatter — <see cref="ThornsPlaceableFurnitureScale"/>.</summary>
	public static Vector3 PlaceableStructureLocalScale( string structureDefId, float extraMul = 1f ) =>
		ThornsPlaceableFurniturePresentation.GetLocalScale( structureDefId, extraMul );

	/// <summary>Cached <c>models/placeables/*.vmdl</c> for crafted furniture.</summary>
	public static Model PlaceableFurnitureModel( string structureDefId )
	{
		InvalidateProcBuildingCachesIfStale();
		if ( _placeableFurnitureCatalogRevision != ThornsPlaceableFurnitureScale.CatalogRevision )
		{
			_placeableFurnitureCatalogRevision = ThornsPlaceableFurnitureScale.CatalogRevision;
			_placeableFurnitureModelsByDefId.Clear();
		}

		if ( _placeableFurnitureModelsByDefId.TryGetValue( structureDefId, out var cached ) && cached.IsValid() )
			return cached;

		if ( !ThornsPlaceableFurnitureCatalog.TryGet( structureDefId, out var entry ) )
		{
			Log.Warning( $"[Thorns] PlaceableFurnitureModel: unknown def {structureDefId}" );
			return Model.Load( "models/dev/box.vmdl" );
		}

		var mdl = Model.Load( entry.ModelPath );
		if ( !mdl.IsValid() || mdl.IsError )
		{
			Log.Warning( $"[Thorns] {entry.ModelPath} failed — dev box for {structureDefId}" );
			mdl = Model.Load( "models/dev/box.vmdl" );
		}

		_placeableFurnitureModelsByDefId[structureDefId] = mdl;
		return mdl;
	}

	public static bool UsesOffsetKitVisualChild( string structureDefId ) =>
		structureDefId is "storage_chest" or "bed";

	/// <summary>Interior / world radio table — 2× prior placeable extents on all axes.</summary>
	public static Vector3 RadioPlaceableLocalScale =>
		ThornsBuildingModule.ScaleBoxToWorldAxes(
			ThornsLootCrate.ProceduralCrateWorldExtent * 2.2f,
			ThornsLootCrate.ProceduralCrateWorldExtent * 1.6f,
			ThornsLootCrate.ProceduralCrateWorldExtent * 2.08f ) * PlaceableStructureWorldScale;

	/// <summary>World-space lift for kit meshes on a child (chest / bed) — collision stays on the root.</summary>
	public const float PlaceableKitVisualWorldUpOffset = 5f;

	public static float PlaceableKitVisualLocalUpOffset( in Vector3 parentLocalScale ) =>
		PlaceableKitVisualWorldUpOffset / MathF.Max( parentLocalScale.z, 0.001f );

	static void EnsureOffsetKitVisualChildLocalTransform( GameObject structureRoot, string childName )
	{
		if ( structureRoot is null || !structureRoot.IsValid() )
			return;

		var localUp = Vector3.Up * PlaceableKitVisualLocalUpOffset( structureRoot.LocalScale );
		foreach ( var ch in structureRoot.Children )
		{
			if ( !ch.IsValid() || ch.Name != childName )
				continue;

			ch.LocalPosition = localUp;
			ch.LocalRotation = Rotation.Identity;
			ch.LocalScale = Vector3.One;
			return;
		}
	}

	/// <summary>Reapply child local transform for <see cref="StorageChestVisualChildName"/> (fixes stale offsets after tuning constants).</summary>
	public static void EnsureStorageChestVisualChildLocalTransform( GameObject structureRoot ) =>
		EnsureOffsetKitVisualChildLocalTransform( structureRoot, StorageChestVisualChildName );

	public static void EnsureBedVisualChildLocalTransform( GameObject structureRoot ) =>
		EnsureOffsetKitVisualChildLocalTransform( structureRoot, BedVisualChildName );

	/// <summary>Child object name for <c>storage_chest</c> mesh (offset from root pivot / collision).</summary>
	public const string StorageChestVisualChildName = "ThornsStorageChestVisual";

	public const string BedVisualChildName = "ThornsBedVisual";

	/// <summary>
	/// Offset kit mesh on a child so art can sit above the placement pivot without moving physics.
	/// </summary>
	public static ModelRenderer GetOrCreateOffsetKitModelRenderer( GameObject structureRoot, string childName )
	{
		if ( structureRoot is null || !structureRoot.IsValid() )
			return default;

		var localUp = Vector3.Up * PlaceableKitVisualLocalUpOffset( structureRoot.LocalScale );

		foreach ( var ch in structureRoot.Children )
		{
			if ( !ch.IsValid() || ch.Name != childName )
				continue;

			ch.LocalPosition = localUp;
			ch.LocalRotation = Rotation.Identity;
			ch.LocalScale = Vector3.One;

			var existing = ch.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			if ( existing.IsValid() )
			{
				existing.Enabled = true;
				return existing;
			}

			var created = ch.Components.Create<ModelRenderer>();
			created.Enabled = true;
			return created;
		}

		var orphanRootMr = structureRoot.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
		if ( orphanRootMr.IsValid() )
			orphanRootMr.Destroy();

		var v = new GameObject( true, childName );
		v.SetParent( structureRoot );
		v.LocalPosition = localUp;
		v.LocalRotation = Rotation.Identity;
		v.LocalScale = Vector3.One;
		var rootMrNew = v.Components.Create<ModelRenderer>();
		rootMrNew.Enabled = true;
		return rootMrNew;
	}

	/// <summary>Offset <c>storage_chest</c> mesh on a child.</summary>
	public static ModelRenderer GetOrCreateStorageChestOffsetModelRenderer( GameObject structureRoot ) =>
		GetOrCreateOffsetKitModelRenderer( structureRoot, StorageChestVisualChildName );

	/// <summary>Offset <c>bed</c> mesh on a child.</summary>
	public static ModelRenderer GetOrCreateBedOffsetModelRenderer( GameObject structureRoot ) =>
		GetOrCreateOffsetKitModelRenderer( structureRoot, BedVisualChildName );

	static ModelRenderer TryResolveOffsetKitModelRenderer( GameObject structureRoot, string childName )
	{
		if ( structureRoot is null || !structureRoot.IsValid() )
			return default;

		foreach ( var ch in structureRoot.Children )
		{
			if ( !ch.IsValid() || ch.Name != childName )
				continue;

			var cmr = ch.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			if ( cmr.IsValid() )
				return cmr;
		}

		return default;
	}

	public static ModelRenderer TryResolveStorageChestModelRenderer( GameObject structureRoot ) =>
		TryResolveOffsetKitModelRenderer( structureRoot, StorageChestVisualChildName )
		?? (structureRoot.IsValid() ? structureRoot.Components.Get<ModelRenderer>( FindMode.EnabledInSelf ) : default);

	public static ModelRenderer TryResolveBedModelRenderer( GameObject structureRoot ) =>
		TryResolveOffsetKitModelRenderer( structureRoot, BedVisualChildName )
		?? (structureRoot.IsValid() ? structureRoot.Components.Get<ModelRenderer>( FindMode.EnabledInSelf ) : default);

	/// <summary>Remove the offset kit visual child (e.g. build ghost switching piece type).</summary>
	public static void DestroyOffsetKitVisualChildIfPresent( GameObject structureRoot, string childName )
	{
		if ( structureRoot is null || !structureRoot.IsValid() )
			return;

		var kill = new List<GameObject>();
		foreach ( var ch in structureRoot.Children )
		{
			if ( ch.IsValid() && ch.Name == childName )
				kill.Add( ch );
		}

		foreach ( var ch in kill )
			ch.Destroy();
	}

	public static void DestroyStorageChestOffsetChildIfPresent( GameObject structureRoot ) =>
		DestroyOffsetKitVisualChildIfPresent( structureRoot, StorageChestVisualChildName );

	public static void DestroyBedOffsetChildIfPresent( GameObject structureRoot ) =>
		DestroyOffsetKitVisualChildIfPresent( structureRoot, BedVisualChildName );

	/// <summary>Player storage chest — <c>models/placeables/chest.vmdl</c>.</summary>
	public static void ApplyStorageChestVisual( ModelRenderer mr, Color? tint = null )
	{
		if ( mr is null || !mr.IsValid() )
			return;

		mr.Enabled = true;

		var mdl = PlaceableChestModel();
		if ( mdl.IsValid() )
			mr.Model = mdl;

		mr.MaterialOverride = default;
		mr.Tint = tint ?? Color.White;
		ApplyPlaceableMeshUvScale( mr, "models/placeables/chest.vmdl" );
	}

	/// <summary>Player bed — <c>models/placeables/bed.vmdl</c>.</summary>
	public static void ApplyBedVisual( ModelRenderer mr, Color? tint = null )
	{
		if ( mr is null || !mr.IsValid() )
			return;

		mr.Enabled = true;

		var mdl = PlaceableBedModel();
		if ( mdl.IsValid() )
			mr.Model = mdl;

		mr.MaterialOverride = default;
		mr.Tint = tint ?? Color.White;
		ApplyPlaceableMeshUvScale( mr, "models/placeables/bed.vmdl" );
	}

	/// <summary>Repeat albedo UVs for scaled Tripo / placeable vmdls (uses mesh local scale).</summary>
	public static void ApplyPlaceableMeshUvScale( ModelRenderer mr, string modelAssetPath, Material sourceMaterial = default )
	{
		if ( mr is null || !mr.IsValid() )
			return;

		var scale = mr.GameObject.IsValid() ? mr.GameObject.WorldScale : Vector3.One;
		ThornsModelMaterialUvScale.ApplyForPlaceableFurniture( mr, mr.Model, modelAssetPath, scale, sourceMaterial );
	}

	/// <summary>Proc building piece — facade or interior-floor vmats tiled to <see cref="GameObject.WorldScale"/>.</summary>
	public static void ApplyProcBuildingPieceUvScale(
		ModelRenderer mr,
		GameObject piece,
		string materialSlug,
		string structureDefId = null )
	{
		if ( mr is null || !mr.IsValid() || piece is null || !piece.IsValid() )
			return;

		if ( string.IsNullOrEmpty( structureDefId ) )
			ThornsProcBuildingPieceFixup.TryParsePieceName( piece.Name, out _, out structureDefId );

		ThornsModelMaterialUvScale.ApplyForGameObject(
			mr,
			piece,
			sourceMaterial: ProcPieceMaterial( structureDefId ?? "", materialSlug ) );
	}

	public static void TryApplyStorageChestFromStructureRoot( GameObject structureRoot )
	{
		if ( structureRoot is null || !structureRoot.IsValid() )
			return;

		EnsureStorageChestVisualChildLocalTransform( structureRoot );

		foreach ( var ch in structureRoot.Children )
		{
			if ( !ch.IsValid() || ch.Name != StorageChestVisualChildName )
				continue;

			var cmr = ch.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			if ( cmr.IsValid() )
			{
				ApplyStorageChestVisual( cmr );
				return;
			}
		}

		var rootMr = structureRoot.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
		if ( rootMr.IsValid() )
		{
			ApplyStorageChestVisual( rootMr );
			return;
		}

		foreach ( var ch in structureRoot.Children )
		{
			if ( !ch.IsValid() || ch.Name != "StructureVisual" )
				continue;

			var mr = ch.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			ApplyStorageChestVisual( mr );
			return;
		}
	}

	/// <summary>Procedural world-gen pieces — per-building facade slug (brick, glass, etc.).</summary>
	public static Model StructureModel( string structureDefId, string materialSlug )
	{
		if ( ThornsPlaceableFurnitureCatalog.IsPortableKitStructureId( structureDefId ) )
			return StructureModel( structureDefId, 0 );

		InvalidateProcBuildingCachesIfStale();
		var key = ProcModelCacheKey( materialSlug, structureDefId );
		if ( _procModelsBySlugAndDef.TryGetValue( key, out var cached ) && cached.IsValid() )
			return cached;

		var built = BuildProcStructureModel( structureDefId, materialSlug );
		_procModelsBySlugAndDef[key] = built;
		return built;
	}

	static Model BuildProcStructureModel( string structureDefId, string materialSlug ) =>
		structureDefId switch
		{
			"wood_foundation" => BuildFoundationPanelModel( materialSlug ),
			"wood_perimeter_trim" => BuildPerimeterTrimPanelModel( materialSlug ),
			"wood_wall" => BuildWallPanelModel( materialSlug ),
			"wood_door" => BuildDoorPanelModel( materialSlug ),
			"wood_window" => BuildFrameWallModel(
				$"thorns/building/wood_window_{materialSlug}",
				ThornsBuildingModule.WindowHoleSize,
				ThornsBuildingModule.WindowHoleSize,
				holeCenterZ: 0f,
				materialSlug ),
			"wood_doorframe" => BuildDoorFrameModel( materialSlug ),
			"wood_ramp" => BuildRampModel( materialSlug ),
			_ => UnitCubeModel( materialSlug )
		};

	public static Model UnitCubeModel( string materialSlug ) =>
		BuildBoxPartsModelWithMaterial(
			$"thorns/building/unit_cube_{materialSlug}",
			new List<(Vector3 center, Vector3 size)> { (Vector3.Zero, ThornsBuildingModule.DevReferenceSize * Vector3.One) },
			FacadeMaterial( materialSlug ) );

	public static Model StructureModel( string structureDefId, int materialTier = (int)ThornsBuildingMaterialTier.Wood )
	{
		InvalidateProcBuildingCachesIfStale();
		var idx = MaterialTierIndexClamped( materialTier );

		return structureDefId switch
		{
			"wood_foundation" => _foundationModelByTier[idx].IsValid()
				? _foundationModelByTier[idx]
				: _foundationModelByTier[idx] = BuildFoundationPanelModel( materialTier ),
			"wood_wall" => _wallModelByTier[idx].IsValid() ? _wallModelByTier[idx] : _wallModelByTier[idx] = BuildWallPanelModel( materialTier ),
			"wood_door" => _doorModelByTier[idx].IsValid()
				? _doorModelByTier[idx]
				: _doorModelByTier[idx] = BuildDoorPanelModel( materialTier ),
			"wood_window" => _windowModelByTier[idx].IsValid() ? _windowModelByTier[idx] : _windowModelByTier[idx] = BuildFrameWallModel(
				$"thorns/building/wood_window_t{idx}",
				ThornsBuildingModule.WindowHoleSize,
				ThornsBuildingModule.WindowHoleSize,
				holeCenterZ: 0f,
				materialTier ),
			"wood_doorframe" => _doorFrameModelByTier[idx].IsValid() ? _doorFrameModelByTier[idx] : _doorFrameModelByTier[idx] = BuildDoorFrameModel( materialTier ),
			"wood_ramp" => _rampModelByTier[idx].IsValid() ? _rampModelByTier[idx] : _rampModelByTier[idx] = BuildRampModel( materialTier ),
			"storage_chest" => PlaceableChestModel(),
			"campfire" => PlaceableCampfireModel(),
			"workbench" => PlaceableWorkbenchModel(),
			"bed" => PlaceableBedModel(),
			_ => ThornsPlaceableFurnitureCatalog.IsPortableKitStructureId( structureDefId )
				? PlaceableFurnitureModel( structureDefId )
				: DevBoxModel( materialTier )
		};
	}

	public static bool UsesProceduralStructureModel( string structureDefId ) =>
		structureDefId is "wood_window" or "wood_doorframe" or "wood_ramp"
			or "wood_foundation" or "wood_perimeter_trim" or "wood_wall" or "wood_door"
			or "base_core"
			|| ThornsPlaceableFurnitureCatalog.IsPortableKitStructureId( structureDefId );

	/// <summary>Campfire Z extent = half of storage chest cube edge (<see cref="ThornsLootCrate.ProceduralCrateWorldExtent"/>).</summary>
	public static float CampfireWorldHeight => ThornsLootCrate.ProceduralCrateWorldExtent * 0.5f;

	/// <summary>
	/// Square XY edge length so campfire box total surface area matches the chest cube (6 S²): 2(x² + 2xz) = 6S² with z = S/2 → x = S(√13 − 1)/2.
	/// </summary>
	public static float CampfireWorldFootprintEdge =>
		ThornsLootCrate.ProceduralCrateWorldExtent * ( (MathF.Sqrt( 13f ) - 1f ) * 0.5f );

	/// <summary>Corner / seam trim scale — world inches → mesh local scale (single <see cref="ThornsBuildingModule.ScaleBoxToWorldAxes"/> pass).</summary>
	public static Vector3 PerimeterTrimFoundationScale( float widthWorld, float depthWorld, float heightWorld ) =>
		ThornsBuildingModule.ScaleBoxToWorldAxes(
			MathF.Max( widthWorld, 8f ),
			MathF.Max( depthWorld, 8f ),
			MathF.Max( heightWorld, Wh ) );

	/// <summary>Horizontal band trim — allows thin Z; do not clamp to <see cref="Wh"/>.</summary>
	public static Vector3 PerimeterBandTrimScale( float runWorld, float depthWorld, float heightWorld ) =>
		ThornsBuildingModule.ScaleBoxToWorldAxes(
			MathF.Max( runWorld, 8f ),
			MathF.Max( depthWorld, 4f ),
			MathF.Max( heightWorld, 4f ) );

	/// <summary>Swap run/depth on local XY so the shallow axis protrudes past the wall after <paramref name="yawDegrees"/>.</summary>
	public static Vector3 AlignPerimeterTrimFoundationScaleToYaw( Vector3 trimScale, float yawDegrees )
	{
		var q = ( (int)MathF.Round( yawDegrees / 90f ) % 4 + 4 ) % 4;
		return q is 1 or 3
			? trimScale with { x = trimScale.y, y = trimScale.x }
			: trimScale;
	}

	/// <summary>Legacy wall-based trim scale (prefer <see cref="PerimeterTrimFoundationScale"/>).</summary>
	public static Vector3 PerimeterTrimLocalScale( float trimWidthWorld, float trimDepthWorld, float fullShellSpanWorld ) =>
		PerimeterTrimFoundationScale( trimWidthWorld, trimDepthWorld, fullShellSpanWorld );

	/// <summary>Legacy alias — prefer <see cref="AlignPerimeterTrimFoundationScaleToYaw"/>.</summary>
	public static Vector3 AlignPerimeterTrimScaleToYaw( Vector3 trimScale, float yawDegrees ) =>
		AlignPerimeterTrimFoundationScaleToYaw( trimScale, yawDegrees );

	/// <summary>
	/// Stretch scaled perimeter panels (<see cref="StructureLocalScale"/> thin×long×tall) along the wall run
	/// so adjacent edge pieces overlap at building corners (closes see-through seams).
	/// </summary>

	public static Vector3 PerimeterShellWallLocalScale( Vector3 baseScale, float yawDegrees )
	{
		if ( baseScale.LengthSquared < 1e-8f )
			return baseScale;

		var runFactor = ThornsBuildingModule.ProcPerimeterWallRunWorld / Cs;
		if ( MathF.Abs( runFactor - 1f ) < 1e-4f )
			return baseScale;

		var q = ( (int)MathF.Round( yawDegrees / 90f ) % 4 + 4 ) % 4;
		return q switch
		{
			0 or 2 => baseScale with { y = baseScale.y * runFactor },
			1 or 3 => baseScale with { x = baseScale.x * runFactor },
			_ => baseScale
		};
	}

	public static Vector3 StructureLocalScale( string structureDefId ) =>
		structureDefId switch
		{
			"wood_foundation" => ThornsBuildingModule.ScaleBoxToWorldAxes( Cs, Cs, Ft ),
			"wood_wall" => ThornsBuildingModule.ScaleBoxToWorldAxes( T, Cs, Wh ),
			"wood_window" or "wood_doorframe" or "wood_ramp" => Vector3.One,
			"wood_door" => Vector3.One,
			// Same world size as <see cref="ThornsLootCrate"/> procedural crates (dev box × ProceduralCrateUniformScale).
			"storage_chest" => ThornsBuildingModule.ScaleBoxToWorldAxes(
				ThornsLootCrate.ProceduralCrateWorldExtent,
				ThornsLootCrate.ProceduralCrateWorldExtent,
				ThornsLootCrate.ProceduralCrateWorldExtent ) * PlaceableStructureWorldScale,
			"campfire" => ThornsBuildingModule.ScaleBoxToWorldAxes(
				CampfireWorldFootprintEdge,
				CampfireWorldFootprintEdge,
				CampfireWorldHeight ) * PlaceableStructureWorldScale,
			"workbench" => PlaceableStructureLocalScale( "workbench" ),
			"bed" => PlaceableStructureLocalScale( "bed" ),
			"base_core" => ThornsBuildingModule.ScaleBoxToWorldAxes( 9f, 9f, 12f ),
			_ => ThornsPlaceableFurnitureCatalog.IsPortableKitStructureId( structureDefId )
				? PlaceableStructureLocalScale( structureDefId )
				: ThornsBuildingModule.ScaleBoxToWorldAxes( Cs, Cs, Ft )
		};

	static Model DevBoxModel( int materialTier ) => UnitCubeModel( materialTier );

	static Model BuildFoundationPanelModel( string materialSlug )
	{
		var r = ThornsBuildingModule.DevReferenceSize;
		var floorSlug = ThornsProcBuildingMaterialPalette.InteriorFloorSlugForFacade( materialSlug );
		return BuildBoxPartsModelWithMaterial(
			$"thorns/building/foundation_panel_{materialSlug}_floor_{floorSlug}",
			new List<(Vector3 center, Vector3 size)> { (Vector3.Zero, new Vector3( r, r, r )) },
			InteriorFloorMaterial( materialSlug ),
			BuildingUvMode.Floor );
	}

	static Model BuildFoundationPanelModel( int materialTier ) =>
		BuildFoundationPanelModel( LegacySlugFromTier( MaterialTierIndexClamped( materialTier ) ) );

	/// <summary>Exterior corner/seam posts — same interior floor vmat as slabs; planar UV per face (Generic x/z smears thin sides).</summary>
	static Model BuildPerimeterTrimPanelModel( string materialSlug )
	{
		var r = ThornsBuildingModule.DevReferenceSize;
		var floorSlug = ThornsProcBuildingMaterialPalette.InteriorFloorSlugForFacade( materialSlug );
		return BuildBoxPartsModelWithMaterial(
			$"thorns/building/perimeter_trim_{materialSlug}_floor_{floorSlug}",
			new List<(Vector3 center, Vector3 size)> { (Vector3.Zero, new Vector3( r, r, r )) },
			InteriorFloorMaterial( materialSlug ),
			BuildingUvMode.Floor );
	}

	static Model BuildWallPanelModel( string materialSlug )
	{
		var r = ThornsBuildingModule.DevReferenceSize;
		return BuildBoxPartsModelWithMaterial(
			$"thorns/building/wall_panel_{materialSlug}",
			new List<(Vector3 center, Vector3 size)> { (Vector3.Zero, new Vector3( r, r, r )) },
			FacadeMaterial( materialSlug ),
			BuildingUvMode.Wall );
	}

	static Model BuildWallPanelModel( int materialTier ) =>
		BuildWallPanelModel( LegacySlugFromTier( MaterialTierIndexClamped( materialTier ) ) );

	static Model BuildDoorPanelModel( string materialSlug )
	{
		var size = new Vector3(
			ThornsBuildingModule.WallThickness,
			ThornsBuildingModule.DoorWidth,
			ThornsBuildingModule.DoorHeight );
		return BuildBoxPartsModelWithMaterial(
			$"thorns/building/door_panel_{materialSlug}",
			new List<(Vector3 center, Vector3 size)> { (Vector3.Zero, size) },
			FacadeMaterial( materialSlug ),
			BuildingUvMode.Wall );
	}

	static Model BuildDoorPanelModel( int materialTier ) =>
		BuildDoorPanelModel( LegacySlugFromTier( MaterialTierIndexClamped( materialTier ) ) );

	static Model BuildDoorFrameModel( string materialSlug )
	{
		var halfW = Cs * 0.5f;
		var halfH = Wh * 0.5f;
		var halfHoleW = MathF.Min( ThornsBuildingModule.DoorWidth * 0.5f, halfW - 4f );
		var holeBottom = -halfH;
		var holeTop = MathF.Min( halfH, holeBottom + ThornsBuildingModule.DoorHeight );
		return BuildFrameWallContinuousBackingModel(
			$"thorns/building/wood_doorframe_{materialSlug}",
			halfW,
			halfH,
			halfHoleW,
			holeBottom,
			holeTop,
			materialSlug,
			omitHoleCeilingCollision: true );
	}

	static Model BuildDoorFrameModel( int materialTier ) =>
		BuildDoorFrameModel( LegacySlugFromTier( MaterialTierIndexClamped( materialTier ) ) );

	static Model BuildFrameWallModel( string name, float holeWidth, float holeHeight, float holeCenterZ, string materialSlug, bool omitHoleCeilingCollision = false )
	{
		var halfW = Cs * 0.5f;
		var halfH = Wh * 0.5f;
		var halfHoleW = MathF.Min( holeWidth * 0.5f, halfW - 4f );
		var halfHoleH = MathF.Min( holeHeight * 0.5f, halfH - 4f );
		var holeBottom = MathF.Max( -halfH, holeCenterZ - halfHoleH );
		var holeTop = MathF.Min( halfH, holeCenterZ + halfHoleH );
		return BuildFrameWallContinuousBackingModel( name, halfW, halfH, halfHoleW, holeBottom, holeTop, materialSlug, omitHoleCeilingCollision );
	}

	static Model BuildFrameWallModel( string name, float holeWidth, float holeHeight, float holeCenterZ, int materialTier, bool omitHoleCeilingCollision = false ) =>
		BuildFrameWallModel( name, holeWidth, holeHeight, holeCenterZ, LegacySlugFromTier( MaterialTierIndexClamped( materialTier ) ), omitHoleCeilingCollision );

	static Model BuildFrameWallContinuousBackingModel(
		string name,
		float halfW,
		float halfH,
		float halfHoleW,
		float holeBottom,
		float holeTop,
		string materialSlug,
		bool omitHoleCeilingCollision = false )
	{
		var mat = FacadeMaterial( materialSlug );
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		var xf = T * 0.5f;
		var xb = -T * 0.5f;
		// Doorframes: collision on the hole-top lintel strips + horizontal ceiling plane blocks standing capsule; keep render mesh, drop those tris from collision only.
		var holeTopLintelCollision = !omitHoleCeilingCollision;

		void AddWallQuad( Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool outwardPositiveX, bool addCollision = true )
		{
			var n = UnitNormalCrossEdges( b - a, c - a );
			var uva = UvForMode( a, n, BuildingUvMode.Wall );
			var uvb = UvForMode( b, n, BuildingUvMode.Wall );
			var uvc = UvForMode( c, n, BuildingUvMode.Wall );
			var uvd = UvForMode( d, n, BuildingUvMode.Wall );
			if ( outwardPositiveX )
			{
				AddTri( vb, collVerts, collIdx, a, b, c, uva, uvb, uvc, addCollision );
				AddTri( vb, collVerts, collIdx, a, c, d, uva, uvc, uvd, addCollision );
			}
			else
			{
				AddTri( vb, collVerts, collIdx, a, c, b, uva, uvc, uvb, addCollision );
				AddTri( vb, collVerts, collIdx, a, d, c, uva, uvd, uvc, addCollision );
			}
		}

		void AddWallMappedQuad( Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool addCollision = true )
		{
			var n = UnitNormalCrossEdges( b - a, c - a );
			var uva = UvForMode( a, n, BuildingUvMode.Wall );
			var uvb = UvForMode( b, n, BuildingUvMode.Wall );
			var uvc = UvForMode( c, n, BuildingUvMode.Wall );
			var uvd = UvForMode( d, n, BuildingUvMode.Wall );
			AddTri( vb, collVerts, collIdx, a, b, c, uva, uvb, uvc, addCollision );
			AddTri( vb, collVerts, collIdx, a, c, d, uva, uvc, uvd, addCollision );
		}

		// Backing wall strips share one wall-space UV frame (y/z), so no per-strip rotation artifacts.
		AddWallQuad( new Vector3( xf, -halfW, -halfH ), new Vector3( xf, -halfHoleW, -halfH ), new Vector3( xf, -halfHoleW, halfH ), new Vector3( xf, -halfW, halfH ), true );
		AddWallQuad( new Vector3( xf, halfHoleW, -halfH ), new Vector3( xf, halfW, -halfH ), new Vector3( xf, halfW, halfH ), new Vector3( xf, halfHoleW, halfH ), true );
		AddWallQuad( new Vector3( xf, -halfHoleW, -halfH ), new Vector3( xf, halfHoleW, -halfH ), new Vector3( xf, halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeBottom ), true );
		AddWallQuad( new Vector3( xf, -halfHoleW, holeTop ), new Vector3( xf, halfHoleW, holeTop ), new Vector3( xf, halfHoleW, halfH ), new Vector3( xf, -halfHoleW, halfH ), true, holeTopLintelCollision );

		AddWallQuad( new Vector3( xb, -halfW, -halfH ), new Vector3( xb, -halfHoleW, -halfH ), new Vector3( xb, -halfHoleW, halfH ), new Vector3( xb, -halfW, halfH ), false );
		AddWallQuad( new Vector3( xb, halfHoleW, -halfH ), new Vector3( xb, halfW, -halfH ), new Vector3( xb, halfW, halfH ), new Vector3( xb, halfHoleW, halfH ), false );
		AddWallQuad( new Vector3( xb, -halfHoleW, -halfH ), new Vector3( xb, halfHoleW, -halfH ), new Vector3( xb, halfHoleW, holeBottom ), new Vector3( xb, -halfHoleW, holeBottom ), false );
		AddWallQuad( new Vector3( xb, -halfHoleW, holeTop ), new Vector3( xb, halfHoleW, holeTop ), new Vector3( xb, halfHoleW, halfH ), new Vector3( xb, -halfHoleW, halfH ), false, holeTopLintelCollision );

		// Hole inner thickness + outer border thickness.
		AddWallMappedQuad( new Vector3( xb, -halfHoleW, -halfH ), new Vector3( xf, -halfHoleW, -halfH ), new Vector3( xf, halfHoleW, -halfH ), new Vector3( xb, halfHoleW, -halfH ) );
		AddWallMappedQuad( new Vector3( xb, -halfHoleW, halfH ), new Vector3( xf, -halfHoleW, halfH ), new Vector3( xf, halfHoleW, halfH ), new Vector3( xb, halfHoleW, halfH ) );
		AddWallMappedQuad( new Vector3( xb, -halfW, -halfH ), new Vector3( xf, -halfW, -halfH ), new Vector3( xf, -halfW, halfH ), new Vector3( xb, -halfW, halfH ) );
		AddWallMappedQuad( new Vector3( xb, halfW, -halfH ), new Vector3( xf, halfW, -halfH ), new Vector3( xf, halfW, halfH ), new Vector3( xb, halfW, halfH ) );
		AddWallMappedQuad( new Vector3( xb, -halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeBottom ), new Vector3( xf, halfHoleW, holeBottom ), new Vector3( xb, halfHoleW, holeBottom ) );
		AddWallMappedQuad( new Vector3( xb, -halfHoleW, holeTop ), new Vector3( xf, -halfHoleW, holeTop ), new Vector3( xf, halfHoleW, holeTop ), new Vector3( xb, halfHoleW, holeTop ), holeTopLintelCollision );
		AddWallMappedQuad( new Vector3( xb, -halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeBottom ), new Vector3( xf, -halfHoleW, holeTop ), new Vector3( xb, -halfHoleW, holeTop ) );
		AddWallMappedQuad( new Vector3( xb, halfHoleW, holeBottom ), new Vector3( xf, halfHoleW, holeBottom ), new Vector3( xf, halfHoleW, holeTop ), new Vector3( xb, halfHoleW, holeTop ) );

		var mesh = new Mesh( mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( name );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		mb.AddCollisionMesh( collVerts.ToArray(), collIdx.ToArray() );
		return mb.Create();
	}

static Model BuildBoxPartsModel(
	string name,
	IReadOnlyList<(Vector3 center, Vector3 size)> parts,
	int materialTier,
	BuildingUvMode uvMode = BuildingUvMode.Generic )
	{
		return BuildBoxPartsModelWithMaterial( name, parts, BuildingMaterial( materialTier ), uvMode );
	}

	static Model BuildBoxPartsModelWithMaterial(
		string name,
		IReadOnlyList<(Vector3 center, Vector3 size)> parts,
	Material mat,
	BuildingUvMode uvMode = BuildingUvMode.Generic )
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		foreach ( var p in parts )
			AddBox( vb, collVerts, collIdx, p.center, p.size, uvMode, addCollision: true );

		var mesh = new Mesh( mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( name );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		mb.AddCollisionMesh( collVerts.ToArray(), collIdx.ToArray() );
		return mb.Create();
	}

	static Model BuildRampModel( string materialSlug )
	{
		var floorSlug = ThornsProcBuildingMaterialPalette.InteriorFloorSlugForFacade( materialSlug );
		var mat = InteriorFloorMaterial( materialSlug );
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var collVerts = new List<Vector3>();
		var collIdx = new List<int>();

		var x0 = -Cs * 0.5f;
		var x1 = Cs * 0.5f;
		var y0 = -Cs * 0.5f;
		var y1 = Cs * 0.5f;
		var z0 = -Wh * 0.5f;
		var z1 = Wh * 0.5f;

		// Sloped walk deck only (flat incline), not a solid triangular wedge under the tread.
		var a = new Vector3( x0, y0, z0 );
		var c = new Vector3( x1, y0, z1 );
		var d = new Vector3( x0, y1, z0 );
		var f = new Vector3( x1, y1, z1 );

		var rise = c - a;
		var run = d - a;
		var deckNormal = Vector3.Cross( rise, run );
		if ( deckNormal.LengthSquared < 1e-8f )
			deckNormal = Vector3.Up;
		else
			deckNormal = deckNormal.Normal;
		if ( deckNormal.y < 0f )
			deckNormal = -deckNormal;

		var thickness = MathF.Max( Ft, 4f );
		var inset = deckNormal * thickness;
		var a2 = a - inset;
		var c2 = c - inset;
		var d2 = d - inset;
		var f2 = f - inset;

		// Collision: smooth walk deck (unchanged ramp physics). Render: sides + discrete treads.
		AddQuad( vb, collVerts, collIdx, a, c, f, d, BuildingUvMode.Ramp, addVisual: false );
		AddQuad( vb, collVerts, collIdx, a2, d2, f2, c2, BuildingUvMode.Ramp );
		AddQuad( vb, collVerts, collIdx, a, d, d2, a2, BuildingUvMode.Ramp );
		AddQuad( vb, collVerts, collIdx, c, f, f2, c2, BuildingUvMode.Ramp );
		AddQuad( vb, collVerts, collIdx, a, c, c2, a2, BuildingUvMode.Ramp );
		AddQuad( vb, collVerts, collIdx, d, f, f2, d2, BuildingUvMode.Ramp );
		AppendRampStairVisuals( vb, x0, x1, y0, y1, z0, z1 );

		var mesh = new Mesh( mat, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var mb = new ModelBuilder();
		mb.WithName( $"thorns/building/wood_ramp_{materialSlug}_floor_{floorSlug}" );
		mb.WithMass( 0f );
		mb.WithSurface( "default" );
		mb.AddMesh( mesh );
		mb.AddCollisionMesh( collVerts.ToArray(), collIdx.ToArray() );
		return mb.Create();
	}

	static Model BuildRampModel( int materialTier ) =>
		BuildRampModel( LegacySlugFromTier( MaterialTierIndexClamped( materialTier ) ) );

	/// <summary>Visual treads/risers on the ramp cell — collision stays the smooth sloped deck above.</summary>
	static void AppendRampStairVisuals(
		VertexBuffer vb,
		float x0,
		float x1,
		float y0,
		float y1,
		float z0,
		float z1 )
	{
		var rise = z1 - z0;
		var run = x1 - x0;
		if ( rise < 1f || run < 1f )
			return;

		var stepCount = Math.Clamp( (int)MathF.Round( rise / 11f ), 7, 12 );
		var risePerStep = rise / stepCount;
		var treadDepth = run / stepCount;
		var treadThickness = MathF.Max( Ft * 0.85f, 4f );
		var riserThickness = MathF.Max( T * 0.65f, 3f );
		var treadSpanY = ( y1 - y0 ) * 0.94f;
		var yMid = ( y0 + y1 ) * 0.5f;

		for ( var i = 0; i < stepCount; i++ )
		{
			var zBottom = z0 + i * risePerStep;
			var zTop = z0 + ( i + 1 ) * risePerStep;
			var xFront = x0 + i * treadDepth;
			var xBack = x0 + ( i + 1 ) * treadDepth;
			var xMid = ( xFront + xBack ) * 0.5f;

			AddBox(
				vb,
				null,
				null,
				new Vector3( xMid, yMid, zTop - treadThickness * 0.5f ),
				new Vector3( treadDepth * 0.96f, treadSpanY, treadThickness ),
				BuildingUvMode.Floor,
				addCollision: false );

			if ( i > 0 )
			{
				AddBox(
					vb,
					null,
					null,
					new Vector3( xFront, yMid, ( zBottom + zTop ) * 0.5f ),
					new Vector3( riserThickness, treadSpanY, risePerStep * 0.98f ),
					BuildingUvMode.Wall,
					addCollision: false );
			}
		}

		// Low kick plate at the bottom of the flight.
		AddBox(
			vb,
			null,
			null,
			new Vector3( x0 + riserThickness * 0.5f, yMid, z0 + risePerStep * 0.45f ),
			new Vector3( riserThickness, treadSpanY, risePerStep * 0.9f ),
			BuildingUvMode.Wall,
			addCollision: false );
	}

static void AddBox(
	VertexBuffer vb,
	List<Vector3> collVerts,
	List<int> collIdx,
	Vector3 center,
	Vector3 size,
	BuildingUvMode uvMode = BuildingUvMode.Generic,
	bool addCollision = true )
	{
		var h = size * 0.5f;
		var x0 = center.x - h.x;
		var x1 = center.x + h.x;
		var y0 = center.y - h.y;
		var y1 = center.y + h.y;
		var z0 = center.z - h.z;
		var z1 = center.z + h.z;

		var p000 = new Vector3( x0, y0, z0 );
		var p001 = new Vector3( x0, y0, z1 );
		var p010 = new Vector3( x0, y1, z0 );
		var p011 = new Vector3( x0, y1, z1 );
		var p100 = new Vector3( x1, y0, z0 );
		var p101 = new Vector3( x1, y0, z1 );
		var p110 = new Vector3( x1, y1, z0 );
		var p111 = new Vector3( x1, y1, z1 );

		AddQuad( vb, collVerts, collIdx, p000, p001, p011, p010, uvMode, addVisual: true, addCollision );
		AddQuad( vb, collVerts, collIdx, p100, p110, p111, p101, uvMode, addVisual: true, addCollision );
		AddQuad( vb, collVerts, collIdx, p000, p100, p101, p001, uvMode, addVisual: true, addCollision );
		AddQuad( vb, collVerts, collIdx, p010, p011, p111, p110, uvMode, addVisual: true, addCollision );
		AddQuad( vb, collVerts, collIdx, p001, p101, p111, p011, uvMode, addVisual: true, addCollision );
		AddQuad( vb, collVerts, collIdx, p000, p010, p110, p100, uvMode, addVisual: true, addCollision );
	}

	static void AddQuad(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector3 d,
		BuildingUvMode uvMode = BuildingUvMode.Generic,
		bool addVisual = true,
		bool addCollision = true )
	{
		if ( uvMode == BuildingUvMode.Floor )
		{
			var n = UnitNormalCrossEdges( b - a, c - a );
			if ( IsDominantFloorPlaneNormal( n ) )
			{
				if ( DebugLogFloorUv && !_loggedFloorUv )
				{
					_loggedFloorUv = true;
					var uvaComputed = UvForMode( a, n, BuildingUvMode.Floor );
					var uvbComputed = UvForMode( b, n, BuildingUvMode.Floor );
					var uvcComputed = UvForMode( c, n, BuildingUvMode.Floor );
					var uvdComputed = UvForMode( d, n, BuildingUvMode.Floor );
					Log.Info(
						$"[Thorns][UV] Floor quad vertices/uvs a={a} uv={uvaComputed} b={b} uv={uvbComputed} c={c} uv={uvcComputed} d={d} uv={uvdComputed} n={n}" );
				}
			}
		}

		AddTri( vb, collVerts, collIdx, a, b, c, uvMode, addVisual, addCollision );
		AddTri( vb, collVerts, collIdx, a, c, d, uvMode, addVisual, addCollision );
	}

	static void AddTri(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		BuildingUvMode uvMode = BuildingUvMode.Generic,
		bool addVisual = true,
		bool addCollision = true )
	{
		var n = UnitNormalCrossEdges( b - a, c - a );
		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: UnitNormalCrossEdges( Vector3.Up, n );
		var uva = UvForMode( a, n, uvMode );
		var uvb = UvForMode( b, n, uvMode );
		var uvc = UvForMode( c, n, uvMode );

		if ( addVisual && vb is not null )
		{
			vb.Add( new Vertex( a, n, tan, new Vector4( uva.x, uva.y, 0f, 0f ) ) );
			vb.Add( new Vertex( b, n, tan, new Vector4( uvb.x, uvb.y, 0f, 0f ) ) );
			vb.Add( new Vertex( c, n, tan, new Vector4( uvc.x, uvc.y, 0f, 0f ) ) );
		}

		if ( !addCollision || collVerts is null || collIdx is null )
			return;

		var start = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( start );
		collIdx.Add( start + 1 );
		collIdx.Add( start + 2 );
	}

	static void AddTri(
		VertexBuffer vb,
		List<Vector3> collVerts,
		List<int> collIdx,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector2 uva,
		Vector2 uvb,
		Vector2 uvc,
		bool addCollision = true )
	{
		var n = UnitNormalCrossEdges( b - a, c - a );
		var tan = MathF.Abs( Vector3.Dot( n, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: UnitNormalCrossEdges( Vector3.Up, n );

		vb.Add( new Vertex( a, n, tan, new Vector4( uva.x, uva.y, 0f, 0f ) ) );
		vb.Add( new Vertex( b, n, tan, new Vector4( uvb.x, uvb.y, 0f, 0f ) ) );
		vb.Add( new Vertex( c, n, tan, new Vector4( uvc.x, uvc.y, 0f, 0f ) ) );

		if ( !addCollision )
			return;

		var start = collVerts.Count;
		collVerts.Add( a );
		collVerts.Add( b );
		collVerts.Add( c );
		collIdx.Add( start );
		collIdx.Add( start + 1 );
		collIdx.Add( start + 2 );
	}

	static Vector3 UnitNormalCrossEdges( Vector3 edgeA, Vector3 edgeB )
	{
		var cross = Vector3.Cross( edgeA, edgeB );
		var lenSq = cross.LengthSquared;
		if ( lenSq < 1e-14f )
			return Vector3.Up;

		return cross / MathF.Sqrt( lenSq );
	}

	static bool IsDominantFloorPlaneNormal( Vector3 n )
	{
		var nx = MathF.Abs( n.x );
		var ny = MathF.Abs( n.y );
		var nz = MathF.Abs( n.z );
		return nz >= nx && nz >= ny;
	}

	/// <summary>
	/// Wall / frame / window jambs: primary face is ±X with (y,z) UVs. Thin ±Y and ±Z faces must use (x,z) and (x,y)
	/// or one axis collapses and the face smears (corner posts, hole sides).
	/// </summary>
	static Vector2 UvWallPlanar( Vector3 p, Vector3 n )
	{
		var nx = MathF.Abs( n.x );
		var ny = MathF.Abs( n.y );
		var nz = MathF.Abs( n.z );
		if ( nx >= ny && nx >= nz )
			return new Vector2( p.y / WallUvTileWorldUnits, p.z / WallUvTileWorldUnits );
		if ( ny >= nx && ny >= nz )
			return new Vector2( p.x / WallUvTileWorldUnits, p.z / WallUvTileWorldUnits );
		return new Vector2( p.x / WallUvTileWorldUnits, p.y / WallUvTileWorldUnits );
	}

	/// <summary>
	/// Slab/foundation top & bottom lie in local XY (normal ±Z). Using (x,z) UVs there makes V constant → streaks; pick plane from dominant normal like wall handling.
	/// </summary>
	static Vector2 UvFloorPlanar( Vector3 p, Vector3 n )
	{
		var nx = MathF.Abs( n.x );
		var ny = MathF.Abs( n.y );
		var nz = MathF.Abs( n.z );
		if ( nz >= nx && nz >= ny )
			return new Vector2( p.x / FloorUvTileWorldUnits, p.y / FloorUvTileWorldUnits );
		if ( nx >= ny && nx >= nz )
			return new Vector2( p.y / FloorUvTileWorldUnits, p.z / FloorUvTileWorldUnits );
		return new Vector2( p.x / FloorUvTileWorldUnits, p.z / FloorUvTileWorldUnits );
	}

	/// <summary>Explicit UV policy per building piece type (floor/wall/ramp) for predictable stylized visuals.</summary>
	static Vector2 UvForMode( Vector3 p, Vector3 n, BuildingUvMode uvMode )
	{
		switch ( uvMode )
		{
			case BuildingUvMode.Floor:
				return UvFloorPlanar( p, n );
			case BuildingUvMode.Wall:
				return UvWallPlanar( p, n );
			case BuildingUvMode.Ramp:
			{
				// Thin ramp slab: sloped deck, underside, and four edge bands (no solid triangular wedge fill).
				var nx = MathF.Abs( n.x );
				var ny = MathF.Abs( n.y );
				var nz = MathF.Abs( n.z );

				// Narrow ±Y end caps: same grain as walls — horizontal runs along world X, vertical is Z.
				if ( ny >= nx && ny >= nz )
					return new Vector2( p.x / WallUvTileWorldUnits, p.z / WallUvTileWorldUnits );

				// ±X vertical faces: identical to building walls (horizontal along Y, vertical Z).
				if ( nx >= ny && nx >= nz )
					return new Vector2( p.y / WallUvTileWorldUnits, p.z / WallUvTileWorldUnits );

				// Flat ±Z (underside / top lip): horizontal along X, cross-run Y (like a ceiling/floor board layout).
				if ( nz >= nx && nz >= ny )
					return new Vector2( p.x / WallUvTileWorldUnits, p.y / WallUvTileWorldUnits );

				// Sloped walk surface: width across ramp (Y) × distance up the incline (XZ diagonal).
				var slopeDir = new Vector3( 1f, 0f, 1f );
				var lenSq = slopeDir.LengthSquared;
				var alongSlope = lenSq >= 1e-12f ? slopeDir / MathF.Sqrt( lenSq ) : Vector3.Forward;
				return new Vector2(
					p.y / RampUvTileWorldUnits,
					Vector3.Dot( p, alongSlope ) / RampUvTileWorldUnits );
			}
			default:
				return new Vector2( p.x / FloorUvTileWorldUnits, p.z / FloorUvTileWorldUnits );
		}
	}
}
