namespace Terraingen.Buildings;

using Terraingen;

/// <summary>Authoritative proc-interior furniture sizes, models, and placement tuning (ported from thorns).</summary>
public static class ThornsPlaceableFurnitureCatalog
{
	public readonly record struct InteriorPlacementTune(
		float BuildingLocalYawDegrees,
		Vector3 OffsetFromCellCenterBuildingLocal,
		Vector3? LocalScaleOverride = null );

	static readonly Vector3 CabinetInteriorLocalScale = new( 97.836f, 74.805f, 89.366f );

	public static Vector3 GetWorldSizeInches( string structureDefId ) => Normalize( structureDefId ) switch
	{
		"chair" => new Vector3( 50f, 50f, 50f ),
		"couch" => new Vector3( 140f, 140f, 60f ),
		"cabinet" => new Vector3( 120f, 40f, 50f ),
		"fridge" => new Vector3( 60f, 60f, 85f ),
		"kitchen_fridge" => new Vector3( 175f, 50f, 100f ),
		"bed" => new Vector3( 80f, 100f, 50f ),
		"workbench" => new Vector3( 100f, 70f, 80f ),
		"desk" => new Vector3( 100f, 70f, 70f ),
		"dining_table" => new Vector3( 100f, 100f, 50f ),
		"conference" => new Vector3( 100f, 100f, 50f ),
		"pallets" => new Vector3( 120f, 120f, 100f ),
		"retail" => new Vector3( 125f, 24f, 100f ),
		"radio" => new Vector3( 100f, 70f, 80f ),
		"military_supply" => new Vector3( 100f, 100f, 100f ),
		"research" => new Vector3( 100f, 50f, 60f ),
		"storage_chest" => new Vector3( 80f, 45f, 50f ),
		"campfire" => new Vector3( 56f, 56f, 28f ),
		_ => new Vector3( 100f, 100f, 100f )
	};

	static readonly HashSet<string> CatalogFurnitureIds = new( StringComparer.OrdinalIgnoreCase )
	{
		"chair", "couch", "cabinet", "fridge", "kitchen_fridge", "bed", "workbench", "desk",
		"dining_table", "conference", "pallets", "retail", "radio", "military_supply", "research",
		"storage_chest", "campfire"
	};

	/// <summary>Player-placed and proc-interior furniture that share catalog VMDL sizing.</summary>
	public static bool UsesCatalogPresentation( string structureDefId ) =>
		CatalogFurnitureIds.Contains( Normalize( structureDefId ) );

	public static string GetModelPath( string structureDefId ) => Normalize( structureDefId ) switch
	{
		"chair" => ThornsPlaceableModels.Chair,
		"couch" => ThornsPlaceableModels.Couch,
		"cabinet" => ThornsPlaceableModels.Cabinet,
		"fridge" => ThornsPlaceableModels.Fridge,
		"kitchen_fridge" => ThornsPlaceableModels.KitchenFridge,
		"bed" => ThornsPlaceableModels.Bed,
		"workbench" => ThornsPlaceableModels.Workbench,
		"desk" => ThornsPlaceableModels.Desk,
		"dining_table" => ThornsPlaceableModels.DiningTable,
		"conference" => ThornsPlaceableModels.Conference,
		"pallets" => ThornsPlaceableModels.Pallets,
		"retail" => ThornsPlaceableModels.Retail,
		"radio" => ThornsPlaceableModels.Radio,
		"military_supply" => ThornsPlaceableModels.MilitarySupply,
		"research" => ThornsPlaceableModels.Research,
		"storage_chest" => ThornsPlaceableModels.Chest,
		"campfire" => ThornsPlaceableModels.Campfire,
		_ => ThornsPlaceableModels.Chest
	};

	public static float GetInteriorDecorYawOffsetDegrees( string structureDefId ) => Normalize( structureDefId ) switch
	{
		"couch" => -90f,
		"kitchen_fridge" => 90f,
		"retail" => 90f,
		_ => 0f
	};

	public static Vector3 GetInteriorPlacementLocalOffsetInches( string structureDefId ) => Normalize( structureDefId ) switch
	{
		"couch" => Planar( 25f, 25f ),
		"pallets" => Planar( 25f, 25f ),
		"conference" => Planar( 25f, 25f ),
		"kitchen_fridge" => Planar( 20f, -50f ),
		"bed" => Planar( 0f, -5f ),
		"military_supply" => Planar( 14f, 14f ),
		"dining_table" => Planar( 8f, 8f ),
		"desk" => Planar( 8f, 8f ),
		"cabinet" => Planar( -20f, 20f ),
		_ => Vector3.Zero
	};

	public static bool TryGetInteriorPlacementLocalScale( string structureDefId, out Vector3 localScale )
	{
		localScale = Vector3.One;
		return Normalize( structureDefId ) switch
		{
			"desk" => Set( out localScale, new Vector3( 80f, 100f, 75f ) ),
			"bed" => Set( out localScale, new Vector3( 112.33f, 102.024f, 108.163f ) ),
			"cabinet" => Set( out localScale, CabinetInteriorLocalScale ),
			"military_supply" => Set( out localScale, new Vector3( 80f, 80f, 110f ) ),
			_ => false
		};
	}

	public static Vector3 BuildInteriorFurnitureLocalPosition(
		int storyIndex,
		float cellCenterLocalX,
		float cellCenterLocalY,
		Vector3 offsetFromCellCenterBuildingLocal ) =>
		new(
			cellCenterLocalX + offsetFromCellCenterBuildingLocal.x,
			cellCenterLocalY + offsetFromCellCenterBuildingLocal.y,
			ThornsProcBuildingInterior.InteriorFloorWalkLocalZ( storyIndex ) );

	public const float RetailBuildingLocalX = -110f;
	public const float RetailBuildingLocalY = 70f;
	public const float RetailBuildingLocalYawDegrees = -90f;

	public static bool TryBuildRetailInteriorPlacementTune(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		out InteriorPlacementTune tune )
	{
		tune = default;
		if ( widthCells < 1 || depthCells < 1 )
			return false;

		if ( !ThornsProcBuildingInterior.TryGridCellCenterLocal( gridX, gridY, widthCells, depthCells, out var center ) )
			return false;

		tune = new InteriorPlacementTune(
			RetailBuildingLocalYawDegrees,
			Planar( RetailBuildingLocalX - center.x, RetailBuildingLocalY - center.y ) );
		return true;
	}

	public static bool TryGetInteriorPlacementTune(
		string structureDefId,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		out InteriorPlacementTune tune )
	{
		tune = default;
		var id = Normalize( structureDefId );
		if ( string.IsNullOrEmpty( id ) || widthCells < 1 || depthCells < 1 )
			return false;

		var maxX = widthCells - 1;
		var maxY = depthCells - 1;

		if ( storyIndex >= 2 && id == "cabinet" )
		{
			var offset = gridX == 0 && gridY == 0
				? Planar( -30f, 20f )
				: Planar( -20f, -20f );
			tune = new InteriorPlacementTune( 180f, offset, CabinetInteriorLocalScale );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == maxY && id == "radio" )
		{
			tune = new InteriorPlacementTune( 270f, Vector3.Zero );
			return true;
		}

		if ( !ThornsInteriorFurnitureCanonicalSlots.TryResolveCornerSlot( id, storyIndex, out var slot )
		     || slot.GridX != gridX
		     || slot.GridY != gridY )
			return false;

		if ( id == "retail" && TryBuildRetailInteriorPlacementTune( gridX, gridY, widthCells, depthCells, out tune ) )
			return true;

		if ( storyIndex == 0 && gridX == maxX && gridY == maxY && id == "kitchen_fridge" )
		{
			tune = new InteriorPlacementTune( 90f, Planar( 20f, -50f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == 0 && id == "pallets" )
		{
			tune = new InteriorPlacementTune( -90f, Planar( 25f, 25f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == 0 && id == "conference" )
		{
			tune = new InteriorPlacementTune( -90f, Planar( 25f, 25f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == 0 && id == "couch" )
		{
			tune = new InteriorPlacementTune( -90f, Planar( 25f, 25f ) );
			return true;
		}

		if ( storyIndex == 0 && gridX == 0 && gridY == maxY && id == "chair" )
		{
			tune = new InteriorPlacementTune( 180f, Planar( 8f, 8f ) );
			return true;
		}

		if ( gridX == 0 && gridY == 0 && storyIndex >= 1 && id == "desk" )
		{
			tune = new InteriorPlacementTune( 0f, Planar( 8f, 8f ), new Vector3( 80f, 100f, 75f ) );
			return true;
		}

		if ( gridX == 0 && gridY == maxY && storyIndex >= 1 && id == "bed" )
		{
			tune = new InteriorPlacementTune( -180f, Planar( 0f, -5f ), new Vector3( 112.33f, 102.024f, 108.163f ) );
			return true;
		}

		if ( storyIndex == 1 && gridX == 0 && gridY == 0 && id == "cabinet" )
		{
			tune = new InteriorPlacementTune( 270f, Planar( -30f, 20f ), CabinetInteriorLocalScale );
			return true;
		}

		return false;
	}

	static bool Set( out Vector3 localScale, Vector3 value )
	{
		localScale = value;
		return true;
	}

	static Vector3 Planar( float x, float y ) => new( x, y, 0f );

	static string Normalize( string structureDefId ) =>
		string.IsNullOrWhiteSpace( structureDefId )
			? ""
			: structureDefId.Trim().ToLowerInvariant();
}
