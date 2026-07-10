using System.Collections.Generic;
using System.Text;

namespace Sandbox;

/// <summary>
/// 3×3 ASCII interiors: furniture only on footprint corners. Story 0 fills every corner except the ramp;
/// ground-floor kitchen/fridge on NE; chair on NW (door side); couch/pallets/conference on SW (back).
/// Upper storeys fill corners with walkable floor and support below, excluding ramp/shaft/headroom openings.
/// Top storey (level 3) uses fixed corners per furniture id (same grid cell on every building).
/// </summary>
public static class ThornsInteriorFurnitureCanonicalSlots
{
	public const int GridCells = 3;

	/// <summary>Bump when corner coordinates change — forces slot table rebuild (hotload-safe).</summary>
	public const int SlotTableRevision = 19;

	/// <summary>Default settlement ramp corner (SE) when no <see cref="ThornsProcBuildingLayout"/> is available.</summary>
	public const int RampGridX = 2;
	public const int RampGridY = 0;
	public const char RampChar = 'R';

	public readonly record struct Slot( int Story, int GridX, int GridY, char AsciiChar );

	static int _slotTableBuiltRevision = -1;
	static bool _loggedSlotProof;

	static void EnsureSlotTableReady()
	{
		if ( _slotTableBuiltRevision == SlotTableRevision )
			return;

		_slotTableBuiltRevision = SlotTableRevision;
		_loggedSlotProof = false;
	}

	static void LogSlotProofOnce()
	{
		if ( _loggedSlotProof )
			return;

		_loggedSlotProof = true;
		if ( TryResolveCornerSlot( "kitchen_fridge", 0, out var kitchen )
		     && TryResolveCornerSlot( "couch", 0, out var couch )
		     && TryResolveCornerSlot( "bed", 1, out var bed ) )
		{
			Log.Info(
				$"[Thorns CanonicalSlots] rev={SlotTableRevision} proof: "
				+ $"kitchen=({kitchen.GridX},{kitchen.GridY}) couch=({couch.GridX},{couch.GridY}) bed=({bed.GridX},{bed.GridY})" );
		}
	}

	/// <summary>Maps catalog ids to upper-storey keys when they share a corner character.</summary>
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

	/// <summary>Corner cell for a furniture id (explicit coordinates — not a static dictionary).</summary>
	public static bool TryResolveCornerSlot( string structureDefId, int storyIndex, out Slot slot )
	{
		slot = default;
		EnsureSlotTableReady();
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
			case "cabinet": slot = new Slot( 1, 2, 2, 'K' ); return true;
			case "bunk": slot = new Slot( 1, 2, 2, 'b' ); return true;
			case "chair_upper":
			case "chair": slot = new Slot( 1, 2, 2, 'h' ); return true;
			case "pallets_upper":
			case "pallets": slot = new Slot( 1, 2, 2, 'P' ); return true;
		}

		if ( storyIndex >= 2 )
		{
			// Same X/Y as storey 2 where possible; cabinet on SE (NE is F1 shaft on level 3).
			switch ( id.ToLowerInvariant() )
			{
				case "desk_top":
				case "desk": slot = new Slot( 2, 0, 0, 'd' ); return true;
				case "workbench": slot = new Slot( 2, 0, 0, 'w' ); return true;
				case "military_supply": slot = new Slot( 2, 0, 0, 'M' ); return true;
				case "bed_top":
				case "bed": slot = new Slot( 2, 0, 2, 'B' ); return true;
				case "bunk": slot = new Slot( 2, 0, 2, 'b' ); return true;
				case "cabinet_top":
				case "cabinet": slot = new Slot( 2, 2, 0, 'K' ); return true;
				case "chair": slot = new Slot( 2, 2, 0, 'h' ); return true;
				case "pallets": slot = new Slot( 2, 2, 0, 'P' ); return true;
			}
		}

		return false;
	}

	public static bool TryGetSlot( string structureDefId, int storyIndex, out Slot slot ) =>
		TryResolveCornerSlot( structureDefId, storyIndex, out slot );

	public static bool TryGetSlot( string structureDefId, out Slot slot ) =>
		TryResolveCornerSlot( structureDefId, 0, out slot )
		|| TryResolveCornerSlot( structureDefId, 1, out slot )
		|| TryResolveCornerSlot( structureDefId, 2, out slot );

	/// <summary>
	/// Authoritative building-local yaw for scripted 3×3 corner cells (catalog mesh forward varies).
	/// Returns false to fall back to <see cref="ThornsProcBuildingInteriorSample.TryGetInteriorWallDeskYawPublic"/>.
	/// </summary>
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

		if ( gridX == maxX && gridY == maxY && (id == "kitchen_fridge" || id == "fridge") )
		{
			buildingLocalYawDegrees = 90f;
			return true;
		}

		return false;
	}

	/// <summary>Place catalog furniture ids at corner slots (optionally filtered by proc layout).</summary>
	public static int CollectCornerPlacements(
		int storyIndex,
		IReadOnlyList<string> furnitureIds,
		int widthCells,
		int depthCells,
		ThornsProcBuildingLayout layout,
		List<ThornsInteriorFurnitureFloorplanAscii.CellPlacement> into )
	{
		if ( furnitureIds is null || into is null )
			return 0;

		EnsureSlotTableReady();
		LogSlotProofOnce();

		GetRampCorner( layout, widthCells, depthCells, out var rampStory, out var rampX, out var rampY );
		var placedCells = new HashSet<(int x, int y)>();
		var added = 0;

		for ( var i = 0; i < furnitureIds.Count; i++ )
		{
			var id = furnitureIds[i];
			if ( string.IsNullOrWhiteSpace( id ) )
				continue;

			if ( !TryResolveCornerSlot( id, storyIndex, out var slot ) )
			{
				Log.Warning(
					$"[Thorns CanonicalSlots] No corner slot for '{id}' story={storyIndex} — skipped." );
				continue;
			}

			var (footprintX, footprintY) = MapCanonicalCellToFootprint(
				slot.GridX,
				slot.GridY,
				widthCells,
				depthCells );

			if ( !IsFootprintCorner( footprintX, footprintY, widthCells, depthCells ) )
			{
				Log.Warning(
					$"[Thorns CanonicalSlots] '{id}' maps to non-corner ({footprintX},{footprintY}) — skipped." );
				continue;
			}

			if ( !IsCornerEligible(
				     storyIndex,
				     footprintX,
				     footprintY,
				     widthCells,
				     depthCells,
				     layout,
				     rampStory,
				     rampX,
				     rampY ) )
			{
				Log.Warning(
					$"[Thorns CanonicalSlots] '{id}' corner ({footprintX},{footprintY}) story={storyIndex} "
					+ "not eligible (ramp/shaft/missing floor) — skipped." );
				continue;
			}

			if ( !placedCells.Add( (footprintX, footprintY) ) )
				continue;

			into.Add( new ThornsInteriorFurnitureFloorplanAscii.CellPlacement
			{
				Story = storyIndex,
				GridX = footprintX,
				GridY = footprintY,
				StructureDefId = id
			} );
			added++;
		}

		return added;
	}

	public static void GetFootprintCorners( int widthCells, int depthCells, List<(int x, int y)> into )
	{
		into.Clear();
		into.Add( (0, 0) );
		into.Add( (widthCells - 1, 0) );
		into.Add( (0, depthCells - 1) );
		into.Add( (widthCells - 1, depthCells - 1 ) );
	}

	public static bool IsFootprintCorner( int gridX, int gridY, int widthCells, int depthCells ) =>
		(gridX == 0 || gridX == widthCells - 1) && (gridY == 0 || gridY == depthCells - 1);

	/// <summary>Maps 3×3 canonical corner/ramp cells onto any rectangular footprint (SW/SE/NW/NE).</summary>
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

	public static void GetRampCorner(
		ThornsProcBuildingLayout layout,
		int widthCells,
		int depthCells,
		out int rampStory,
		out int rampX,
		out int rampY )
	{
		rampStory = 0;
		var canonicalRampX = RampGridX;
		var canonicalRampY = RampGridY;
		if ( layout is not null && layout.Stories > 1 )
		{
			var ramps = layout.GetRampsOnStory( 0 );
			if ( ramps.Count > 0 )
			{
				canonicalRampX = ramps[0].X;
				canonicalRampY = ramps[0].Y;
			}
		}

		( rampX, rampY ) = MapCanonicalCellToFootprint(
			canonicalRampX,
			canonicalRampY,
			widthCells,
			depthCells );
	}

	public static void GetEligibleFurnitureCorners(
		int storyIndex,
		int widthCells,
		int depthCells,
		ThornsProcBuildingLayout layout,
		List<(int x, int y)> into )
	{
		into.Clear();
		GetRampCorner( layout, widthCells, depthCells, out var rampStory, out var rampX, out var rampY );

		var corners = new List<(int x, int y)>( 4 );
		GetFootprintCorners( widthCells, depthCells, corners );

		for ( var i = 0; i < corners.Count; i++ )
		{
			var (x, y) = corners[i];
			if ( !IsCornerEligible( storyIndex, x, y, widthCells, depthCells, layout, rampStory, rampX, rampY ) )
				continue;

			into.Add( (x, y) );
		}
	}

	public static bool IsCornerEligible(
		int storyIndex,
		int gridX,
		int gridY,
		int widthCells,
		int depthCells,
		ThornsProcBuildingLayout layout,
		int rampStory,
		int rampX,
		int rampY )
	{
		if ( !IsFootprintCorner( gridX, gridY, widthCells, depthCells ) )
			return false;

		if ( storyIndex == rampStory && gridX == rampX && gridY == rampY )
			return false;

		if ( layout is not null )
		{
			if ( !layout.HasWalkableFloorAt( storyIndex, gridX, gridY ) )
				return false;

			if ( storyIndex > 0 && !layout.IsCellOccupied( storyIndex - 1, gridX, gridY ) )
				return false;

			foreach ( var ramp in layout.GetRampsOnStory( storyIndex ) )
			{
				if ( ramp.X == gridX && ramp.Y == gridY )
					return false;
			}

			if ( layout.IsOpeningCell( storyIndex, gridX, gridY ) )
				return false;

			return true;
		}

		if ( storyIndex > 0 && gridX == rampX && gridY == rampY )
			return false;

		return true;
	}

	public static int ExpectedFurnitureCountForStory(
		int storyIndex,
		int widthCells,
		int depthCells,
		ThornsProcBuildingLayout layout )
	{
		var corners = new List<(int x, int y)>( 4 );
		GetEligibleFurnitureCorners( storyIndex, widthCells, depthCells, layout, corners );
		return corners.Count;
	}

	public static bool TryBuildFloorRows(
		int storyIndex,
		IReadOnlyList<string> furnitureIds,
		out string[] rows,
		ThornsProcBuildingLayout layout = null,
		int widthCells = GridCells,
		int depthCells = GridCells )
	{
		rows = null;
		if ( furnitureIds is null )
			return false;

		GetRampCorner( layout, widthCells, depthCells, out var rampStory, out var rampX, out var rampY );

		var cells = new char[widthCells, depthCells];
		for ( var gx = 0; gx < widthCells; gx++ )
		for ( var gy = 0; gy < depthCells; gy++ )
			cells[gx, gy] = '.';

		var scratch = new List<ThornsInteriorFurnitureFloorplanAscii.CellPlacement>( 4 );
		CollectCornerPlacements( storyIndex, furnitureIds, widthCells, depthCells, layout, scratch );
		for ( var i = 0; i < scratch.Count; i++ )
		{
			var p = scratch[i];
			if ( !TryResolveCornerSlot( p.StructureDefId, storyIndex, out var slot ) )
				continue;

			cells[p.GridX, p.GridY] = slot.AsciiChar;
		}

		if ( storyIndex == rampStory )
			cells[rampX, rampY] = RampChar;

		rows = new string[depthCells];
		for ( var row = 0; row < depthCells; row++ )
		{
			var gy = depthCells - 1 - row;
			var sb = new StringBuilder( widthCells );
			for ( var gx = 0; gx < widthCells; gx++ )
				sb.Append( cells[gx, gy] );
			rows[row] = sb.ToString();
		}

		return ThornsInteriorFurnitureAsciiLayouts.ValidateRows( rows, widthCells, depthCells );
	}

	public static string FormatSlotLegend()
	{
		EnsureSlotTableReady();
		var sb = new StringBuilder();
		sb.AppendLine(
			$"Corner furniture slots rev={SlotTableRevision} (grid X west→east, Y south→north); "
			+ "F0 ramp SE (2,0), door north — kitchen NE, chair NW, couch SW; "
			+ "F2 desk SW, bed NW, cabinet SE; F3 same X/Y (cabinet SE, NE shaft):" );

		void Line( string id, int story )
		{
			if ( !TryResolveCornerSlot( id, story, out var s ) )
				return;

			sb.AppendLine( $"  {id,-22} story={story} corner=({s.GridX},{s.GridY}) '{s.AsciiChar}'" );
		}

		Line( "couch", 0 );
		Line( "kitchen_fridge", 0 );
		Line( "chair", 0 );
		Line( "desk", 0 );
		Line( "bed", 1 );
		Line( "cabinet", 1 );
		Line( "workbench", 1 );
		Line( "bed", 2 );
		Line( "cabinet", 2 );
		Line( "desk", 2 );
		return sb.ToString();
	}
}
