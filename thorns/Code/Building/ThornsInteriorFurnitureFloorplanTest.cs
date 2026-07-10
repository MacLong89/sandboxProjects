using System.Text;

namespace Sandbox;

/// <summary>
/// Dev scene: proc buildings + interior furniture for WYSIWYG tuning.
/// Open <c>Assets/scenes/thorns_interior_furniture_floorplan_test.scene</c> and press Play.
/// With <see cref="EnableLiveTuning"/>, each prop gets <see cref="ThornsFloorplanFurnitureTuneItem"/> —
/// parented under the building; edit local position/rotation and <see cref="ThornsFloorplanFurnitureTuneItem.WorldSizeInches"/>
/// in the inspector (or move with the scene gizmo). Press <see cref="ExportHotkey"/> or use Export to log integration snippets.
/// Exterior corner pillars and horizontal band trims (base / floor lines / roofline) spawn via <see cref="ThornsProcBuildingSceneSpawner.RefreshPerimeterTrims"/>.
/// </summary>
[Title( "Thorns — Interior Furniture Floorplan Test" )]
[Category( "Thorns/Dev" )]
[Icon( "home" )]
public sealed class ThornsInteriorFurnitureFloorplanTest : Component
{
	[Property, Group( "Floor" )] public string FloorModelPath { get; set; } = "models/dev/box.vmdl";

	[Property, Group( "Floor" )] public Vector3 FloorScale { get; set; } = new( 2200f, 1800f, 4f );

	[Property, Group( "Gallery" )] public Vector3 GalleryOrigin { get; set; } = new( 0f, 0f, 64f );

	[Property, Group( "Gallery" )] public float BuildingSpacing { get; set; } = 380f;

	[Property, Group( "Gallery" )] public int GridColumns { get; set; } = 4;

	[Property, Group( "Gallery" )] public int LayoutVariantIndex { get; set; }

	[Property, Group( "Gallery" )] public bool UseArchetypeMaterials { get; set; } = true;

	[Property, Group( "Gallery" )] public bool ApplySettlementMaterialBias { get; set; }

	[Property, Group( "Gallery" )] public ThornsWorldSettlementKind GallerySettlement { get; set; } =
		ThornsWorldSettlementKind.Isolated;

	[Property, Group( "Gallery" )] public ThornsWorldCityRing GalleryCityRing { get; set; } =
		ThornsWorldCityRing.Core;

	[Property, Group( "Gallery" )] public bool UseDebugTypeColors { get; set; }

	[Property, Group( "Gallery" )] public Vector3 PlayerSpawnOffset { get; set; } = new( -420f, -720f, 80f );

	[Property] public bool RebuildOnStart { get; set; } = true;

	[Property] public bool LogAsciiOnRebuild { get; set; } = true;

	[Property, Group( "Live tuning" )]
	public bool EnableLiveTuning { get; set; } = true;

	/// <summary>When true, only <see cref="WorkbenchBuildingType"/> is spawned (faster tuning). Default: full type gallery.</summary>
	[Property, Group( "Live tuning" )]
	public bool SingleBuildingWorkbench { get; set; }

	[Property, Group( "Live tuning" )]
	public ThornsProcBuildingType WorkbenchBuildingType { get; set; } = ThornsProcBuildingType.Store;

	/// <summary>Skip random fill — only ASCII-scripted cells (matches settlement 3×3 layouts).</summary>
	[Property, Group( "Live tuning" )]
	public bool ScriptedPlacementsOnly { get; set; } = true;

	[Property, Group( "Live tuning" )]
	public string ExportHotkey { get; set; } = "f9";

	GameObject _floor;
	GameObject _spawn;
	GameObject _galleryRoot;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		EnsureFloorAndSpawn();

		if ( RebuildOnStart )
			RebuildGallery();

		if ( EnableLiveTuning )
		{
			Log.Info(
				"[Thorns FloorplanTest] Live tuning ON — select props under the building in Hierarchy, "
				+ "edit Local Position/Rotation (building space) and World Size Inches, then F9 or Export Tuning To Log." );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !EnableLiveTuning )
			return;

		if ( Input.Keyboard.Pressed( ExportHotkey ) )
			ExportTuningToLog();
	}

	[Button( "Export Tuning To Log" )]
	public void ExportTuningToLog()
	{
		var items = Scene.GetAllComponents<ThornsFloorplanFurnitureTuneItem>()
			.Where( t => t.IsValid() )
			.OrderBy( t => t.StructureDefId, StringComparer.OrdinalIgnoreCase )
			.ThenBy( t => t.Story )
			.ThenBy( t => t.GridX )
			.ThenBy( t => t.GridY )
			.ToList();

		if ( items.Count == 0 )
		{
			Log.Warning( "[Thorns FloorplanTest] Export: no ThornsFloorplanFurnitureTuneItem in scene — enable Live Tuning and Rebuild Gallery." );
			return;
		}

		var sb = new StringBuilder();
		sb.AppendLine( "======== Thorns floorplan furniture tuning export ========" );
		sb.AppendLine( "// Paste into chat / ticket — agent integrates into ThornsPlaceableFurnitureCatalog + placement." );
		sb.AppendLine( $"// CatalogRevision bump + recompile required after size edits." );
		sb.AppendLine();

		foreach ( var item in items )
			sb.AppendLine( item.BuildExportSnippet( WorkbenchBuildingType.ToString() ) );

		sb.AppendLine( "======== end export ========" );
		Log.Info( sb.ToString() );
	}

	[Button( "Rebuild Gallery" )]
	public void RebuildGallery()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() )
			return;

		EnsureFloorAndSpawn();
		DestroyGallery();

		_galleryRoot = new GameObject( true, "FloorplanTest_Gallery" );
		_galleryRoot.Parent = GameObject;
		_galleryRoot.WorldPosition = GalleryOrigin;

		var wasTuning = ThornsInteriorFurnitureScatter.FloorplanLiveTuningEnabled;
		ThornsInteriorFurnitureScatter.FloorplanLiveTuningEnabled = EnableLiveTuning;

		var placementRng = new Random( 42069 );
		var batch = new ThornsInteriorFurniturePlacement.Batch();
		var types = SingleBuildingWorkbench
			? [WorkbenchBuildingType]
			: Enum.GetValues<ThornsProcBuildingType>();
		var totalProps = 0;
		var built = 0;

		if ( LogAsciiOnRebuild )
		{
			Log.Info(
				$"[Thorns FloorplanTest] {ThornsInteriorFurnitureFloorplanAscii.FormatLegend()} "
				+ $"layoutRev={ThornsInteriorFurnitureAsciiLayouts.LayoutCatalogRevision}" );
			Log.Info(
				$"[Thorns FloorplanTest] slotRev={ThornsInteriorFurnitureCanonicalSlots.SlotTableRevision} "
				+ $"layoutCatalogRev={ThornsInteriorFurnitureAsciiLayouts.LayoutCatalogRevision}" );
			Log.Info( ThornsInteriorFurnitureCanonicalSlots.FormatSlotLegend() );
		}

		for ( var i = 0; i < types.Length; i++ )
		{
			var type = types[i];
			if ( !ThornsInteriorFurnitureAsciiLayouts.SupportsBuildingType( type ) )
				continue;

			try
			{
				var col = built % Math.Max( 1, GridColumns );
				var row = built / Math.Max( 1, GridColumns );
				var offset = new Vector3( col * BuildingSpacing, -row * BuildingSpacing, 0f );
				var worldPos = GalleryOrigin + offset;

				var layout = ThornsInteriorFurnitureFloorplanAscii.CreateBuildingTypeLayout(
					type,
					LayoutVariantIndex,
					out _ );

				var w = layout.WidthCells;
				var d = layout.DepthCells;
				var stories = layout.Stories;

				var materialSlug = UseArchetypeMaterials
					? ThornsProcBuildingMaterialAffinity.PickRepresentativeMaterialSlug(
						type,
						ApplySettlementMaterialBias,
						GallerySettlement,
						ApplySettlementMaterialBias ? GalleryCityRing : null )
					: "wood";
				var tier = (int)ThornsProcBuildingMaterialPalette.DurabilityTierFromSlug( materialSlug );

				var root = ThornsProcBuildingSceneSpawner.SpawnStandalone(
					placementRng,
					worldPos,
					Rotation.Identity,
					layout,
					tier: tier,
					destroyed: false,
					materialSlug: materialSlug,
					buildingIndex: built,
					debugBuildingTypeColors: UseDebugTypeColors );

				if ( !root.IsValid() )
				{
					Log.Warning( $"[Thorns FloorplanTest] Spawn failed for {type}." );
					continue;
				}

				root.Name = $"FloorplanTest_{type}";
				root.SetParent( _galleryRoot );

				var hints = new ThornsProcBuildingInteriorSample.InteriorPlacementHints
				{
					DoorSide = layout.DoorSide,
					DoorIndex = layout.DoorIndex,
					ScriptedFloorplanExactExclusions = true
				};

				var props = ThornsInteriorFurnitureScatter.ScatterBuilding(
					Scene,
					placementRng,
					root,
					w,
					d,
					stories,
					type,
					hints,
					batch,
					layoutVariantOverride: LayoutVariantIndex,
					scriptedPlacementsOnly: true );

				totalProps += props;
				built++;

				if ( LogAsciiOnRebuild )
				{
					Log.Info(
						$"[Thorns FloorplanTest] === {type} v{LayoutVariantIndex} mat={materialSlug} @ {worldPos} props={props} ===" );
					Log.Info( ThornsInteriorFurnitureAsciiLayouts.FormatVariant( type, LayoutVariantIndex ) );
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"[Thorns FloorplanTest] Failed {type} v{LayoutVariantIndex}: {ex.Message}" );
			}
		}

		ThornsInteriorFurnitureScatter.FloorplanLiveTuningEnabled = wasTuning;

		Log.Info(
			$"[Thorns FloorplanTest] Gallery: {built} buildings, {totalProps} props, "
			+ $"variant={LayoutVariantIndex}, spacing={BuildingSpacing}, cols={GridColumns}, "
			+ $"liveTuning={EnableLiveTuning}, workbench={SingleBuildingWorkbench}" );
	}

	void DestroyGallery()
	{
		if ( _galleryRoot.IsValid() )
			_galleryRoot.Destroy();
		_galleryRoot = null;
	}

	void EnsureFloorAndSpawn()
	{
		if ( !_floor.IsValid() )
			CreateFloor();

		if ( !_spawn.IsValid() )
			CreateSpawnPoint();
	}

	void CreateFloor()
	{
		var model = Model.Load( FloorModelPath );
		if ( !model.IsValid() || model.IsError )
			model = ThornsAnchoredWorldPhysics.DevBoxCollisionModel;

		var planar = new Vector3(
			MathF.Max( 8f, FloorScale.x ),
			MathF.Max( 8f, FloorScale.y ),
			MathF.Max( 2f, FloorScale.z ) );

		_floor = new GameObject( true, "FloorplanTest_Floor" );
		_floor.Parent = GameObject;
		_floor.LocalScale = planar;

		var bb = model.Bounds;
		var topLocalZ = bb.Maxs.z;
		if ( topLocalZ < 1e-4f )
			topLocalZ = bb.Size.z * 0.5f + bb.Mins.z;

		_floor.WorldPosition = new Vector3(
			GalleryOrigin.x,
			GalleryOrigin.y,
			GalleryOrigin.z - topLocalZ * planar.z );

		var mr = _floor.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.Tint = new Color( 0.32f, 0.36f, 0.42f );

		ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( _floor, model );
		ThornsCollisionTags.EnsureWorldSolidTriplet( _floor );
		_floor.Tags.Add( ThornsCollisionTags.FurnitureGalleryFloor );
	}

	void CreateSpawnPoint()
	{
		_spawn = new GameObject( true, "FloorplanTest_Spawn" );
		_spawn.Parent = GameObject;
		_spawn.WorldPosition = GalleryOrigin + PlayerSpawnOffset;
		_spawn.Components.Create<SpawnPoint>();
	}
}
