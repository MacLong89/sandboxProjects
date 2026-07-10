namespace Terraingen.Buildings;

/// <summary>Interior grid placement helpers for 3×3 proc buildings (building-local inches).</summary>
public static class ThornsProcBuildingInterior
{
	public const int GridCells = 3;
	public const int DoorSide = 2;
	public const int DoorIndex = 1;

	public readonly record struct CellPlacement( int Story, int GridX, int GridY, string StructureDefId );

	public static bool TryGridCellCenterLocal(
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		out Vector3 localCenter )
	{
		localCenter = default;
		if ( widthCells < 1 || depthCells < 1 )
			return false;

		if ( gridX < 0 || gridX >= widthCells || gridY < 0 || gridY >= depthCells )
			return false;

		localCenter = new Vector3(
			GridAxisLocal( gridX, widthCells, ThornsBuildingModule.Cell ),
			GridAxisLocal( gridY, depthCells, ThornsBuildingModule.Cell ),
			0f );
		return true;
	}

	public static float InteriorFloorWalkLocalZ( int storyIndex ) =>
		storyIndex * ThornsBuildingModule.StoryHeight + ThornsBuildingModule.FloorThickness;

	public static float ResolvePlacementYawDegrees(
		string structureDefId,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells )
	{
		if ( ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
			    structureDefId, storyIndex, gridX, gridY, widthCells, depthCells, out var tune ) )
			return tune.BuildingLocalYawDegrees;

		if ( ThornsInteriorFurnitureCanonicalSlots.TryGetScriptedCornerYaw(
			    structureDefId, storyIndex, gridX, gridY, widthCells, depthCells, out var scriptedYaw ) )
			return scriptedYaw;

		if ( TryGetInteriorWallDeskYaw( widthCells, depthCells, storyIndex, gridX, gridY, out var wallYaw ) )
			return wallYaw;

		return ThornsPlaceableFurnitureCatalog.GetInteriorDecorYawOffsetDegrees( structureDefId );
	}

	public static Vector3 ResolvePlacementLocalOffset(
		string structureDefId,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells )
	{
		if ( ThornsPlaceableFurnitureCatalog.TryGetInteriorPlacementTune(
			    structureDefId, storyIndex, gridX, gridY, widthCells, depthCells, out var tune ) )
			return tune.OffsetFromCellCenterBuildingLocal;

		return ThornsPlaceableFurnitureCatalog.GetInteriorPlacementLocalOffsetInches( structureDefId );
	}

	public static Vector3 ResolveLocalPosition(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		string structureDefId,
		Vector3 worldSize,
		out Rotation localRotation )
	{
		localRotation = Rotation.Identity;
		if ( !TryGridCellCenterLocal( gridX, gridY, widthCells, depthCells, out var center ) )
			return default;

		var yaw = ResolvePlacementYawDegrees( structureDefId, storyIndex, gridX, gridY, widthCells, depthCells );
		localRotation = Rotation.FromYaw( yaw );
		center += ResolvePlacementLocalOffset(
			structureDefId, storyIndex, gridX, gridY, widthCells, depthCells );
		center.z = InteriorFloorWalkLocalZ( storyIndex ) + worldSize.z * 0.5f;
		return center;
	}

	public static float GridAxisLocal( int index, int count, float cell ) =>
		( index - ( count - 1 ) * 0.5f ) * cell;

	public static void GetPerimeterWallExtents(
		int widthCells,
		int depthCells,
		out float westX,
		out float eastX,
		out float southY,
		out float northY )
	{
		var cell = ThornsBuildingModule.Cell;
		var half = cell * 0.5f;
		westX = -half * (widthCells - 1) - half;
		eastX = half * (widthCells - 1) + half;
		southY = -half * (depthCells - 1) - half;
		northY = half * (depthCells - 1) + half;
	}

	public static int DoorCellForWidth( int widthCells ) =>
		Math.Max( 0, (widthCells - 1) / 2 );

	/// <summary>North-wall door threshold and interior approach — not whole-wall corners.</summary>
	public static bool IsDoorThresholdCell( int gridX, int gridY, int widthCells, int depthCells )
	{
		if ( widthCells < 1 || depthCells < 1 )
			return false;

		if ( gridX < 0 || gridX >= widthCells || gridY < 0 || gridY >= depthCells )
			return false;

		var doorX = DoorCellForWidth( widthCells );
		var doorY = depthCells - 1;

		if ( gridX == doorX && gridY == doorY )
			return true;

		// One tile south of the door — keep the entry lane clear on multi-cell depths.
		return depthCells > 2 && gridX == doorX && gridY == doorY - 1;
	}

	/// <summary>Floor cell on the outer ring, touching an exterior wall.</summary>
	public static bool IsWallAdjacentFloorCell( int gridX, int gridY, int widthCells, int depthCells )
	{
		if ( widthCells < 1 || depthCells < 1 )
			return false;

		if ( gridX < 0 || gridX >= widthCells || gridY < 0 || gridY >= depthCells )
			return false;

		return gridX == 0
		       || gridX == widthCells - 1
		       || gridY == 0
		       || gridY == depthCells - 1;
	}

	public static bool TryGetInteriorWallDeskYaw(
		int widthCells,
		int depthCells,
		int storyIndex,
		int gridX,
		int gridY,
		out float yawDegrees )
	{
		yawDegrees = 0f;
		if ( widthCells < 1 || depthCells < 1 )
			return false;

		var maxX = widthCells - 1;
		var maxY = depthCells - 1;

		// Orthogonal to perimeter walls — 45°/135° diagonals were for desk-in-corner only.
		if ( gridX == 0 && gridY == 0 )
		{
			yawDegrees = 0f;
			return true;
		}

		if ( gridX == maxX && gridY == 0 )
		{
			yawDegrees = 180f;
			return true;
		}

		if ( gridX == 0 && gridY == maxY )
		{
			yawDegrees = 0f;
			return true;
		}

		if ( gridX == maxX && gridY == maxY )
		{
			yawDegrees = 180f;
			return true;
		}

		_ = storyIndex;
		return false;
	}
}
