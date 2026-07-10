namespace Terraingen.Buildings;

/// <summary>Corner slot mapping for 3×3 ASCII proc interiors (ported from thorns).</summary>
public static class ThornsInteriorFurnitureCanonicalSlots
{
	public const int GridCells = 3;
	public const int RampGridX = 2;
	public const int RampGridY = 0;

	public readonly record struct Slot( int Story, int GridX, int GridY, char AsciiChar );

	public static string ResolveSlotId( string structureDefId, int storyIndex )
	{
		if ( storyIndex <= 0 )
			return structureDefId;

		if ( string.Equals( structureDefId, "desk", StringComparison.OrdinalIgnoreCase ) )
			return "desk_upper";
		if ( string.Equals( structureDefId, "workbench", StringComparison.OrdinalIgnoreCase ) )
			return "workbench_upper";
		if ( string.Equals( structureDefId, "military_supply", StringComparison.OrdinalIgnoreCase ) )
			return "military_supply_upper";
		if ( string.Equals( structureDefId, "chair", StringComparison.OrdinalIgnoreCase ) )
			return "chair_upper";
		if ( string.Equals( structureDefId, "pallets", StringComparison.OrdinalIgnoreCase ) )
			return "pallets_upper";
		if ( storyIndex >= 2 && string.Equals( structureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
			return "bed_top";
		if ( storyIndex >= 2 && string.Equals( structureDefId, "cabinet", StringComparison.OrdinalIgnoreCase ) )
			return "cabinet_top";
		if ( storyIndex >= 2 && string.Equals( structureDefId, "desk", StringComparison.OrdinalIgnoreCase ) )
			return "desk_top";

		return structureDefId;
	}

	public static bool TryResolveCornerSlot( string structureDefId, int storyIndex, out Slot slot )
	{
		slot = default;
		if ( string.IsNullOrWhiteSpace( structureDefId ) )
			return false;

		var id = ResolveSlotId( structureDefId, storyIndex );
		if ( storyIndex <= 0 )
		{
			switch ( id.ToLowerInvariant() )
			{
				case "kitchen_fridge": slot = new Slot( 0, 2, 2, 'k' ); return true;
				case "fridge": slot = new Slot( 0, 2, 2, 'F' ); return true;
				case "pallets": slot = new Slot( 0, 0, 0, 'P' ); return true;
				case "conference": slot = new Slot( 0, 0, 0, 'c' ); return true;
				case "couch": slot = new Slot( 0, 0, 0, 'C' ); return true;
				case "desk": slot = new Slot( 0, 0, 2, 'd' ); return true;
				case "retail": slot = new Slot( 0, 0, 2, 'r' ); return true;
				case "radio": slot = new Slot( 0, 0, 2, 'A' ); return true;
				case "military_supply": slot = new Slot( 0, 0, 2, 'M' ); return true;
				case "workbench": slot = new Slot( 0, 0, 2, 'w' ); return true;
				case "dining_table": slot = new Slot( 0, 0, 2, 'T' ); return true;
				case "chair": slot = new Slot( 0, 0, 2, 'h' ); return true;
			}

			return false;
		}

		switch ( id.ToLowerInvariant() )
		{
			case "desk_upper":
			case "desk": slot = new Slot( 1, 0, 0, 'd' ); return true;
			case "workbench_upper":
			case "workbench": slot = new Slot( 1, 0, 0, 'w' ); return true;
			case "military_supply_upper":
			case "military_supply": slot = new Slot( 1, 0, 0, 'M' ); return true;
			case "bed": slot = new Slot( 1, 0, 2, 'B' ); return true;
			// SW corner — opposite bed NW along the west wall (F0 ramp stays SE).
			case "cabinet": slot = new Slot( 1, 0, 0, 'K' ); return true;
			case "chair_upper":
			case "chair": slot = new Slot( 1, 0, 0, 'h' ); return true;
			case "pallets_upper":
			case "pallets": slot = new Slot( 1, 0, 0, 'P' ); return true;
		}

		if ( storyIndex >= 2 )
		{
			switch ( id.ToLowerInvariant() )
			{
				case "desk_top":
				case "desk": slot = new Slot( 2, 0, 0, 'd' ); return true;
				case "workbench": slot = new Slot( 2, 0, 0, 'w' ); return true;
				case "military_supply": slot = new Slot( 2, 0, 0, 'M' ); return true;
				case "bed_top":
				case "bed": slot = new Slot( 2, 0, 2, 'B' ); return true;
				case "cabinet_top":
				case "cabinet": slot = new Slot( 2, 2, 0, 'K' ); return true;
				case "chair": slot = new Slot( 2, 2, 0, 'h' ); return true;
				case "pallets": slot = new Slot( 2, 2, 0, 'P' ); return true;
			}
		}

		return false;
	}

	public static bool TryGetScriptedCornerYaw(
		string structureDefId,
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		out float buildingLocalYawDegrees )
	{
		buildingLocalYawDegrees = 0f;
		if ( storyIndex != 0 || widthCells < 1 || depthCells < 1 || string.IsNullOrWhiteSpace( structureDefId ) )
			return false;

		if ( !TryResolveCornerSlot( structureDefId, 0, out var slot )
		     || !FootprintCellMatchesCanonical( gridX, gridY, slot.GridX, slot.GridY, widthCells, depthCells ) )
			return false;

		var maxX = widthCells - 1;
		var maxY = depthCells - 1;
		var id = structureDefId.ToLowerInvariant();
		if ( gridX == maxX && gridY == maxY && ( id == "kitchen_fridge" || id == "fridge" ) )
		{
			buildingLocalYawDegrees = 90f;
			return true;
		}

		return false;
	}

	public static int CollectCornerPlacements(
		int storyIndex,
		IReadOnlyList<string> furnitureIds,
		int widthCells,
		int depthCells,
		List<ThornsProcBuildingInterior.CellPlacement> into,
		int stories = 3,
		bool[,,] skipFloor = null )
	{
		if ( furnitureIds is null || into is null )
			return 0;

		GetRampCorner( widthCells, depthCells, out var rampStory, out var rampX, out var rampY );
		var placedCells = new HashSet<(int x, int y)>();
		var added = 0;

		for ( var i = 0; i < furnitureIds.Count; i++ )
		{
			var id = furnitureIds[i];
			if ( string.IsNullOrWhiteSpace( id ) )
				continue;

			if ( !TryResolveCornerSlot( id, storyIndex, out var slot ) )
				continue;

			var (footprintX, footprintY) = MapCanonicalCellToFootprint(
				slot.GridX, slot.GridY, widthCells, depthCells );

			if ( !IsFootprintCorner( footprintX, footprintY, widthCells, depthCells ) )
				continue;

			if ( !IsCornerEligible(
				     storyIndex,
				     footprintX,
				     footprintY,
				     widthCells,
				     depthCells,
				     rampStory,
				     rampX,
				     rampY,
				     stories,
				     skipFloor ) )
				continue;

			if ( !placedCells.Add( (footprintX, footprintY) ) )
				continue;

			into.Add( new ThornsProcBuildingInterior.CellPlacement( storyIndex, footprintX, footprintY, id ) );
			added++;
		}

		return added;
	}

	public static bool IsFootprintCorner( int gridX, int gridY, int widthCells, int depthCells ) =>
		( gridX == 0 || gridX == widthCells - 1 ) && ( gridY == 0 || gridY == depthCells - 1 );

	public static (int GridX, int GridY) MapCanonicalCellToFootprint(
		int canonicalX,
		int canonicalY,
		int widthCells,
		int depthCells )
	{
		if ( widthCells <= 0 || depthCells <= 0 )
			return (0, 0);

		if ( widthCells <= GridCells && depthCells <= GridCells )
		{
			return (
				Math.Clamp( canonicalX, 0, widthCells - 1 ),
				Math.Clamp( canonicalY, 0, depthCells - 1 ) );
		}

		static int MapAxis( int canonical, int cells ) =>
			canonical <= 0 ? 0 : canonical >= GridCells - 1 ? cells - 1 : Math.Clamp( canonical, 0, cells - 1 );

		return ( MapAxis( canonicalX, widthCells ), MapAxis( canonicalY, depthCells ) );
	}

	public static bool FootprintCellMatchesCanonical(
		int gridX,
		int gridY,
		int canonicalX,
		int canonicalY,
		int widthCells,
		int depthCells )
	{
		var mapped = MapCanonicalCellToFootprint( canonicalX, canonicalY, widthCells, depthCells );
		return mapped.GridX == gridX && mapped.GridY == gridY;
	}

	public static void GetRampCorner( int widthCells, int depthCells, out int rampStory, out int rampX, out int rampY )
	{
		rampStory = 0;
		( rampX, rampY ) = MapCanonicalCellToFootprint( RampGridX, RampGridY, widthCells, depthCells );
	}

	static bool IsCornerEligible(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int rampStory,
		int rampX,
		int rampY,
		int stories,
		bool[,,] skipFloor = null )
	{
		if ( !IsFootprintCorner( gridX, gridY, widthCells, depthCells ) )
			return false;

		return !IsFurnitureCellBlocked(
			storyIndex,
			gridX,
			gridY,
			widthCells,
			depthCells,
			rampStory,
			rampX,
			rampY,
			stories,
			skipFloor );
	}

	/// <summary>Door lane, ramp corner, switchback anchor, stair opening, and missing floor tiles.</summary>
	public static bool IsFurnitureCellBlocked(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int stories,
		bool[,,] skipFloor = null )
	{
		GetRampCorner( widthCells, depthCells, out var rampStory, out var rampX, out var rampY );
		return IsFurnitureCellBlocked(
			storyIndex,
			gridX,
			gridY,
			widthCells,
			depthCells,
			rampStory,
			rampX,
			rampY,
			stories,
			skipFloor );
	}

	/// <summary>Door lane, ramp corner, switchback anchor, and upper shaft cells.</summary>
	public static bool IsFurnitureCellBlocked(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int rampStory,
		int rampX,
		int rampY,
		int stories,
		bool[,,] skipFloor = null )
	{
		if ( !ThornsProcTileRampHeadroom.HasInteriorFloorTile( skipFloor, storyIndex, gridX, gridY ) )
			return true;

		return IsFurnitureCellBlocked(
			storyIndex,
			gridX,
			gridY,
			widthCells,
			depthCells,
			rampStory,
			rampX,
			rampY,
			stories );
	}

	static bool IsFurnitureCellBlocked(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		int rampStory,
		int rampX,
		int rampY,
		int stories )
	{
		if ( widthCells < 1 || depthCells < 1 )
			return true;

		if ( gridX < 0 || gridX >= widthCells || gridY < 0 || gridY >= depthCells )
			return true;

		// Ground floor keeps wall furniture even under upper-storey ramp openings.
		if ( storyIndex <= 0 )
			return ThornsProcBuildingInterior.IsDoorThresholdCell( gridX, gridY, widthCells, depthCells );

		if ( storyIndex == rampStory && gridX == rampX && gridY == rampY )
			return true;

		if ( TryGetRampAnchorOnStory( storyIndex, widthCells, depthCells, stories, out var anchorX, out var anchorY )
		     && gridX == anchorX
		     && gridY == anchorY )
			return true;

		if ( TryGetUpperRampOpeningOnStory(
			     storyIndex,
			     widthCells,
			     depthCells,
			     stories,
			     out var shaftX,
			     out var shaftY,
			     out var headX,
			     out var headY )
		     && ((gridX == shaftX && gridY == shaftY) || (gridX == headX && gridY == headY)) )
			return true;

		if ( ThornsProcBuildingInterior.IsDoorThresholdCell( gridX, gridY, widthCells, depthCells ) )
			return true;

		return false;
	}

	/// <summary>Switchback ramp anchors that block furniture on their landing storey (not ground floor).</summary>
	static bool TryGetRampAnchorOnStory(
		int storyIndex,
		int widthCells,
		int depthCells,
		int stories,
		out int gridX,
		out int gridY )
	{
		gridX = 0;
		gridY = 0;
		if ( stories <= 1 || storyIndex <= 0 || storyIndex >= stories - 1 )
			return false;

		var (anchorX, anchorY) = ThornsProcBuildingRampPlanner.ResolveSwitchbackAnchor( storyIndex );
		( gridX, gridY ) = MapCanonicalCellToFootprint( anchorX, anchorY, widthCells, depthCells );
		return true;
	}

	static bool TryGetUpperRampOpeningOnStory(
		int storyIndex,
		int widthCells,
		int depthCells,
		int stories,
		out int shaftX,
		out int shaftY,
		out int headX,
		out int headY )
	{
		shaftX = 0;
		shaftY = 0;
		headX = 0;
		headY = 0;
		if ( storyIndex <= 0 || storyIndex >= stories )
			return false;

		var rampStory = storyIndex - 1;
		if ( rampStory < 0 || rampStory >= stories - 1 )
			return false;

		var (anchorX, anchorY) = ThornsProcBuildingRampPlanner.ResolveSwitchbackAnchor( rampStory );
		var (rampX, rampY) = MapCanonicalCellToFootprint( anchorX, anchorY, widthCells, depthCells );

		var ramp = new ThornsProcRampSpec
		{
			Story = rampStory,
			X = rampX,
			Y = rampY,
			Direction = ThornsProcRampDirection.West
		};

		return ThornsProcTileRampHeadroom.TryGetUpperOpeningCells(
			ramp,
			widthCells,
			depthCells,
			out shaftX,
			out shaftY,
			out headX,
			out headY );
	}
}
